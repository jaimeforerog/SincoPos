using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using POS.Application.DTOs;
using POS.Application.Services;
using POS.Domain.Aggregates;
using POS.Infrastructure.Data;
using POS.Infrastructure.Data.Entities;
using POS.Infrastructure.Services.Erp;

namespace POS.Infrastructure.Services;

/// <summary>
/// Gestiona las devoluciones parciales de ventas: valida cantidades, restaura inventario
/// (Event Sourcing + costeo), ajusta caja y genera nota crédito contable en el ERP Outbox.
/// </summary>
public class VentaDevolucionService
{
    private readonly AppDbContext _context;
    private readonly global::Marten.IDocumentSession _session;
    private readonly global::Marten.IDocumentStore _store;
    private readonly CosteoService _costeoService;
    private readonly ILogger<VentaDevolucionService> _logger;
    private readonly IActivityLogService _activityLogService;
    private readonly ErpSincoOptions _erpOptions;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public VentaDevolucionService(
        AppDbContext context,
        global::Marten.IDocumentSession session,
        global::Marten.IDocumentStore store,
        CosteoService costeoService,
        ILogger<VentaDevolucionService> logger,
        IActivityLogService activityLogService,
        IOptions<ErpSincoOptions> erpOptions,
        IHttpContextAccessor httpContextAccessor)
    {
        _context = context;
        _session = session;
        _store = store;
        _costeoService = costeoService;
        _logger = logger;
        _activityLogService = activityLogService;
        _erpOptions = erpOptions.Value;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task<(DevolucionVentaDto? devolucion, string? error)> CrearDevolucionParcialAsync(
        int ventaId, CrearDevolucionParcialDto dto, string? emailUsuario)
    {
        var venta = await _context.Ventas
            .Include(v => v.Detalles)
            .FirstOrDefaultAsync(v => v.Id == ventaId);

        if (venta == null) return (null, "NOT_FOUND");
        if (venta.Estado != EstadoVenta.Completada)
            return (null, "Solo se pueden hacer devoluciones de ventas completadas.");

        var diasTranscurridos = (DateTime.UtcNow - venta.FechaVenta).TotalDays;
        if (diasTranscurridos > 30)
            return (null, $"La venta tiene {Math.Floor(diasTranscurridos)} días. Solo se permiten devoluciones dentro de 30 días.");

        var devolucionesAnteriores = await _context.DevolucionesVenta
            .Include(d => d.Detalles)
            .Where(d => d.VentaId == ventaId)
            .ToListAsync();

        // Validar y construir detalle de devolución
        var detallesDevolucion = new List<DetalleDevolucion>();
        decimal totalDevuelto = 0;

        foreach (var linea in dto.Lineas)
        {
            var detalleOriginal = venta.Detalles.FirstOrDefault(d => d.ProductoId == linea.ProductoId);
            if (detalleOriginal == null)
                return (null, $"El producto {linea.ProductoId} no está en la venta original.");

            var cantidadYaDevuelta = devolucionesAnteriores
                .SelectMany(d => d.Detalles)
                .Where(dd => dd.ProductoId == linea.ProductoId)
                .Sum(dd => dd.CantidadDevuelta);

            var disponible = detalleOriginal.Cantidad - cantidadYaDevuelta;
            if (linea.Cantidad > disponible)
                return (null,
                    $"No se puede devolver {linea.Cantidad} unidades de {detalleOriginal.NombreProducto}. " +
                    $"Vendido: {detalleOriginal.Cantidad}, Ya devuelto: {cantidadYaDevuelta}, Disponible: {disponible}");

            var subtotalDevuelto = detalleOriginal.PrecioUnitario * linea.Cantidad;
            totalDevuelto += subtotalDevuelto;

            detallesDevolucion.Add(new DetalleDevolucion
            {
                ProductoId    = linea.ProductoId,
                NombreProducto = detalleOriginal.NombreProducto,
                CantidadDevuelta = linea.Cantidad,
                PrecioUnitario = detalleOriginal.PrecioUnitario,
                CostoUnitario  = detalleOriginal.CostoUnitario,
                SubtotalDevuelto = subtotalDevuelto,
                LoteInventarioId = detalleOriginal.LoteInventarioId,
                NumeroLote       = detalleOriginal.NumeroLote
            });
        }

        // Consecutivo
        var ultimaDevolucion = await _context.DevolucionesVenta
            .IgnoreQueryFilters()
            .Where(d => d.EmpresaId == venta.EmpresaId)
            .OrderByDescending(d => d.Id)
            .Select(d => d.NumeroDevolucion)
            .FirstOrDefaultAsync();

        var consecutivo = 1;
        if (ultimaDevolucion != null && ultimaDevolucion.Contains('-'))
        {
            int.TryParse(ultimaDevolucion.Split('-').Last(), out consecutivo);
            consecutivo++;
        }
        var numeroDevolucion = $"DEV-{consecutivo:D6}";

        var sucursal = await _context.Sucursales.FindAsync(venta.SucursalId);
        if (sucursal == null) return (null, "Sucursal no encontrada.");

        // Restaurar inventario por cada línea
        var pendingMartenEvents = new List<(Guid StreamId, object Evento)>();
        foreach (var dd in detallesDevolucion)
        {
            var detalleOrig = venta.Detalles.First(d => d.ProductoId == dd.ProductoId);

            var streamId = InventarioAggregate.GenerarStreamId(dd.ProductoId, venta.SucursalId);
            var aggregate = await _session.Events.AggregateStreamAsync<InventarioAggregate>(streamId);
            if (aggregate != null)
            {
                var eventoEntrada = aggregate.AgregarEntrada(
                    dd.CantidadDevuelta, dd.CostoUnitario,
                    null, null, $"Devolución {numeroDevolucion}", dto.Motivo, null);
                pendingMartenEvents.Add((streamId, eventoEntrada));
            }

            var montoImpuestoUnitario = detalleOrig.Cantidad > 0
                ? detalleOrig.MontoImpuesto / detalleOrig.Cantidad : 0;

            if (dd.LoteInventarioId.HasValue)
                await _costeoService.ReintegrarLoteAsync(dd.LoteInventarioId.Value, dd.CantidadDevuelta);
            else
                await _costeoService.RegistrarLoteEntrada(
                    dd.ProductoId, venta.SucursalId, dd.CantidadDevuelta, dd.CostoUnitario,
                    detalleOrig.PorcentajeImpuesto, montoImpuestoUnitario,
                    $"Devolución {numeroDevolucion}", null);

            var stock = await _context.Stock.FirstOrDefaultAsync(
                s => s.ProductoId == dd.ProductoId && s.SucursalId == venta.SucursalId);
            if (stock != null)
                await _costeoService.ActualizarCostoEntrada(
                    stock, dd.CantidadDevuelta, dd.CostoUnitario, sucursal.MetodoCosteo);
        }

        var subUsuario = _httpContextAccessor.HttpContext?.User?.FindFirst("sub")?.Value
            ?? _httpContextAccessor.HttpContext?.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        int? usuarioId = await _context.ResolverUsuarioIdAsync(emailUsuario, subUsuario);

        var devolucion = new DevolucionVenta
        {
            VentaId          = ventaId,
            EmpresaId        = sucursal.EmpresaId,
            NumeroDevolucion = numeroDevolucion,
            Motivo           = dto.Motivo,
            TotalDevuelto    = totalDevuelto,
            FechaDevolucion  = DateTime.UtcNow,
            AutorizadoPorUsuarioId = usuarioId,
            Detalles         = detallesDevolucion
        };
        _context.DevolucionesVenta.Add(devolucion);

        var caja = await _context.Cajas.FindAsync(venta.CajaId);
        if (caja != null) caja.MontoActual -= totalDevuelto;

        // Nota crédito ERP
        var (asientosNC, ncPayload) = await BuildNotaCreditoAsync(
            venta, devolucion, detallesDevolucion, numeroDevolucion, totalDevuelto, sucursal);

        // Transacción atómica
        await _context.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            var npgsqlConn = (Npgsql.NpgsqlConnection)_context.Database.GetDbConnection();
            if (npgsqlConn.State != System.Data.ConnectionState.Open)
                await npgsqlConn.OpenAsync();
            await using var npgsqlTx = await npgsqlConn.BeginTransactionAsync();
            await _context.Database.UseTransactionAsync(npgsqlTx);
            await using var martenTx = _store.LightweightSession(
                global::Marten.Services.SessionOptions.ForTransaction(npgsqlTx));
            foreach (var (sid, evt) in pendingMartenEvents)
                martenTx.Events.Append(sid, evt);
            await martenTx.SaveChangesAsync();
            await _context.SaveChangesAsync(); // genera devolucion.Id

            _context.DocumentosContables.Add(new DocumentoContable
            {
                NumeroSoporte    = numeroDevolucion,
                TipoDocumento    = "NotaCredito",
                TerceroId        = venta.ClienteId,
                SucursalId       = venta.SucursalId,
                FechaCausacion   = DateTime.UtcNow,
                FormaPago        = venta.MetodoPago.ToString(),
                TotalDebito      = asientosNC.Where(a => a.Naturaleza == "Debito").Sum(a => a.Valor),
                TotalCredito     = asientosNC.Where(a => a.Naturaleza == "Credito").Sum(a => a.Valor),
                Detalles         = asientosNC.Select(a => new DetalleDocumentoContable
                {
                    CuentaContable = a.Cuenta,
                    CentroCosto    = a.CentroCosto,
                    Naturaleza     = a.Naturaleza,
                    Valor          = a.Valor,
                    Nota           = a.Nota
                }).ToList()
            });
            _context.ErpOutboxMessages.Add(new ErpOutboxMessage
            {
                TipoDocumento = "NotaCreditoVenta",
                EntidadId     = devolucion.Id,
                Payload       = System.Text.Json.JsonSerializer.Serialize(ncPayload),
                FechaCreacion = DateTime.UtcNow,
                Estado        = EstadoOutbox.Pendiente
            });
            await _context.SaveChangesAsync();
            await npgsqlTx.CommitAsync();
        });

        _logger.LogInformation(
            "Devolución parcial {NumeroDevolucion} creada para venta {NumeroVenta}. Total devuelto: {Total}",
            numeroDevolucion, venta.NumeroVenta, totalDevuelto);

        await _activityLogService.LogActivityAsync(new ActivityLogDto(
            Accion: "DevolucionParcial",
            Tipo: TipoActividad.Venta,
            Descripcion: $"Devolución parcial {numeroDevolucion} de venta {venta.NumeroVenta}. Total devuelto: ${totalDevuelto:N2}, Items: {detallesDevolucion.Count}",
            SucursalId: venta.SucursalId,
            TipoEntidad: "DevolucionVenta",
            EntidadId: devolucion.Id.ToString(),
            EntidadNombre: numeroDevolucion,
            DatosNuevos: new
            {
                NumeroDevolucion = numeroDevolucion,
                VentaId = ventaId,
                NumeroVenta = venta.NumeroVenta,
                Motivo = dto.Motivo,
                TotalDevuelto = totalDevuelto,
                AutorizadoPor = emailUsuario,
                Productos = detallesDevolucion.Select(d => new
                    { d.ProductoId, d.NombreProducto, d.CantidadDevuelta, d.SubtotalDevuelto })
            }));

        return (VentaService.MapDevolucionToDto(devolucion, venta.NumeroVenta, emailUsuario), null);
    }

    private async Task<(List<AsientoContableErp> Asientos, CompraErpPayload Payload)> BuildNotaCreditoAsync(
        Venta venta,
        DevolucionVenta devolucion,
        List<DetalleDevolucion> detalles,
        string numeroDevolucion,
        decimal totalDevuelto,
        Sucursal sucursal)
    {
        var centroCosto = sucursal.CentroCosto ?? string.Empty;
        var productoIds = detalles.Select(d => d.ProductoId).Distinct().ToList();
        var productosBatch = await _context.Productos
            .Include(p => p.Categoria)
            .Include(p => p.Impuesto)
            .Where(p => productoIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id);

        var ingresos = new Dictionary<string, decimal>();
        var ivaAcum  = new Dictionary<string, (string? Cuenta, decimal Total)>();

        foreach (var dd in detalles)
        {
            var detalleOrig = venta.Detalles.First(d => d.ProductoId == dd.ProductoId);
            var lineaSubtotal  = dd.CantidadDevuelta * dd.PrecioUnitario;
            var lineaImpuesto  = lineaSubtotal * detalleOrig.PorcentajeImpuesto;
            var prod           = productosBatch.GetValueOrDefault(dd.ProductoId);
            var cuentaIngreso  = prod?.Categoria?.CuentaIngreso ?? "4135";

            ingresos.TryAdd(cuentaIngreso, 0);
            ingresos[cuentaIngreso] += lineaSubtotal;

            if (detalleOrig.PorcentajeImpuesto > 0)
            {
                var nombreImp = $"IVA {detalleOrig.PorcentajeImpuesto * 100:0.##}%";
                var cuentaImp = prod?.Impuesto?.CodigoCuentaContable ?? "2408";
                if (!ivaAcum.ContainsKey(nombreImp)) ivaAcum[nombreImp] = (cuentaImp, 0);
                var actual = ivaAcum[nombreImp];
                ivaAcum[nombreImp] = (actual.Cuenta, actual.Total + lineaImpuesto);
            }
        }

        var asientos = new List<AsientoContableErp>();
        foreach (var ing in ingresos)
            asientos.Add(new(ing.Key, centroCosto, "Debito", ing.Value,
                $"NC - Reversión Ingreso por devolución {numeroDevolucion}"));
        foreach (var item in ivaAcum)
            asientos.Add(new(item.Value.Cuenta ?? "2408", centroCosto, "Debito", item.Value.Total,
                $"NC - Reversión {item.Key} por devolución {numeroDevolucion}"));

        var totalIva     = ivaAcum.Sum(i => i.Value.Total);
        var totalIngreso = ingresos.Sum(i => i.Value);
        asientos.Add(new(_erpOptions.CuentaCaja, centroCosto, "Credito", totalIngreso + totalIva,
            $"NC - Reembolso devolución {numeroDevolucion} de venta {venta.NumeroVenta}"));

        var payload = new CompraErpPayload(
            NumeroOrden: numeroDevolucion,
            NitProveedor: "",
            FormaPago: venta.MetodoPago.ToString(),
            FechaVencimientoErp: DateTime.UtcNow,
            FechaRecepcion: DateTime.UtcNow,
            SucursalId: venta.SucursalId,
            Asientos: asientos,
            TotalOriginalDocumento: totalDevuelto);

        return (asientos, payload);
    }
}
