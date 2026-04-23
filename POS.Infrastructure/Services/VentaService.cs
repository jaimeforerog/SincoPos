using System.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using POS.Application.DTOs;
using POS.Application.Services;
using POS.Domain.Aggregates;
using POS.Domain.Events.Inventario;
using POS.Domain.Events.Venta;
using POS.Infrastructure.Data;
using POS.Infrastructure.Data.Entities;
using POS.Infrastructure.Services.Erp;

namespace POS.Infrastructure.Services;

public sealed class VentaService : IVentaService
{
    private static readonly ActivitySource _tracer = new("SincoPos.Ventas");

    private readonly AppDbContext _context;
    private readonly global::Marten.IDocumentSession _session;
    private readonly global::Marten.IDocumentStore _store;
    private readonly IPrecioService _precioService;
    private readonly IVentaCosteoService _ventaCosteoService;
    private readonly CosteoService _costeoService;
    private readonly ITaxEngine _taxEngine;
    private readonly ILogger<VentaService> _logger;
    private readonly IActivityLogService _activityLogService;
    private readonly FacturacionBackgroundService _facturacionBackground;
    private readonly INotificationService _notificationService;
    private readonly ErpSincoOptions _erpOptions;
    private readonly IVentaErpService _ventaErpService;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IEthicalGuardService _ethicalGuard;
    private readonly VentaAnulacionService _anulacionService;
    private readonly VentaDevolucionService _devolucionService;

    public VentaService(
        AppDbContext context,
        global::Marten.IDocumentSession session,
        global::Marten.IDocumentStore store,
        IPrecioService precioService,
        IVentaCosteoService ventaCosteoService,
        CosteoService costeoService,
        ITaxEngine taxEngine,
        ILogger<VentaService> logger,
        IActivityLogService activityLogService,
        FacturacionBackgroundService facturacionBackground,
        INotificationService notificationService,
        IOptions<ErpSincoOptions> erpOptions,
        IVentaErpService ventaErpService,
        IHttpContextAccessor httpContextAccessor,
        IEthicalGuardService ethicalGuard,
        VentaAnulacionService anulacionService,
        VentaDevolucionService devolucionService)
    {
        _context = context;
        _session = session;
        _store = store;
        _precioService = precioService;
        _ventaCosteoService = ventaCosteoService;
        _costeoService = costeoService;
        _taxEngine = taxEngine;
        _logger = logger;
        _activityLogService = activityLogService;
        _facturacionBackground = facturacionBackground;
        _notificationService = notificationService;
        _erpOptions = erpOptions.Value;
        _ventaErpService = ventaErpService;
        _httpContextAccessor = httpContextAccessor;
        _ethicalGuard = ethicalGuard;
        _anulacionService = anulacionService;
        _devolucionService = devolucionService;
    }

    public async Task<(VentaDto? venta, string? error)> CrearVentaAsync(CrearVentaDto dto)
    {
        using var span = _tracer.StartActivity("VentaService.CrearVenta");
        span?.SetTag("caja.id", dto.CajaId);
        span?.SetTag("sucursal.id", dto.SucursalId);
        span?.SetTag("lineas.count", dto.Lineas.Count);

        var (ctx, ctxError) = await CargarContextoVentaAsync(dto);
        if (ctx == null) return (null, ctxError);

        var detalles = new List<DetalleVenta>();
        var stocksVerificar = new List<(Stock Stock, string Nombre)>();
        var pendingMartenEvents = new List<(Guid StreamId, object Evento)>();
        decimal subtotal = 0, descuentoTotal = 0, totalImpuestos = 0;
        bool requiereFacturaElectronica = false;

        foreach (var linea in dto.Lineas)
        {
            var (procesada, lineaError) = await ProcesarLineaAsync(linea, ctx, dto.SucursalId);
            if (procesada == null) return (null, lineaError);
            detalles.Add(procesada.Detalle);
            stocksVerificar.Add(procesada.StockVerificar);
            pendingMartenEvents.Add(procesada.MartenEvento);
            subtotal += procesada.Subtotal;
            descuentoTotal += procesada.Descuento;
            totalImpuestos += procesada.TotalImpuestos;
            if (procesada.RequiereFactura) requiereFacturaElectronica = true;
        }

        var total = subtotal - descuentoTotal + totalImpuestos;
        var cambio = dto.MontoPagado.HasValue ? dto.MontoPagado.Value - total : (decimal?)null;

        if (dto.MontoPagado.HasValue && dto.MontoPagado.Value < total)
            return (null, $"Monto pagado ({dto.MontoPagado.Value}) es menor al total ({total}).");

        var lineasEtica = detalles.Select(d =>
        {
            var prod = ctx.ProductosMap[d.ProductoId];
            return new LineaVentaEticaDto(d.ProductoId, d.PrecioUnitario, prod.PrecioVenta, d.Descuento, d.Cantidad);
        }).ToList();
        var guardDto = new EvaluarVentaEticaDto(dto.SucursalId, null, subtotal, descuentoTotal, detalles.Count, lineasEtica);
        var (permitido, errorGuard) = await _ethicalGuard.EvaluarVentaAsync(guardDto);
        if (!permitido) return (null, errorGuard);

        var asientosVenta = ConstruirAsientosErp(
            detalles, ctx.ProductosMap, total, ctx.NumeroVenta, dto.MetodoPago, ctx.Sucursal.CentroCosto ?? string.Empty);

        var venta = new Venta
        {
            NumeroVenta = ctx.NumeroVenta,
            EmpresaId = ctx.Sucursal.EmpresaId,
            SucursalId = dto.SucursalId,
            CajaId = dto.CajaId,
            ClienteId = dto.ClienteId,
            Subtotal = subtotal,
            Descuento = descuentoTotal,
            Impuestos = totalImpuestos,
            Total = total,
            Estado = EstadoVenta.Completada,
            MetodoPago = (MetodoPago)dto.MetodoPago,
            MontoPagado = dto.MontoPagado,
            Cambio = cambio,
            Observaciones = dto.Observaciones,
            FechaVenta = ctx.FechaVentaEfectiva,
            RequiereFacturaElectronica = requiereFacturaElectronica,
            Detalles = detalles
        };
        _context.Ventas.Add(venta);
        ctx.Caja.MontoActual += total;

        var externalId = _httpContextAccessor.HttpContext?.User?.FindFirst("oid")?.Value
            ?? _httpContextAccessor.HttpContext?.User?.FindFirst("http://schemas.microsoft.com/identity/claims/objectidentifier")?.Value
            ?? _httpContextAccessor.HttpContext?.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
            ?? _httpContextAccessor.HttpContext?.User?.FindFirst("sub")?.Value;

        await EjecutarTransaccionAtomicaAsync(venta, pendingMartenEvents, externalId, dto, ctx.NitCliente, total, asientosVenta, detalles);

        span?.SetTag("venta.id", venta.Id);
        span?.SetTag("venta.total", total);
        _logger.LogInformation("Venta {NumeroVenta} completada. Total: {Total}, Items: {Items}",
            ctx.NumeroVenta, total, detalles.Count);

        await NotificarVentaAsync(venta, dto.SucursalId, ctx.NumeroVenta, total, subtotal,
            descuentoTotal, totalImpuestos, dto.MetodoPago, dto.ClienteId, dto.CajaId,
            detalles, stocksVerificar);

        return (MapToDto(venta, ctx.Sucursal.Nombre, ctx.Caja.Nombre, ctx.NombreCliente), null);
    }

    /// <summary>Delega en <see cref="VentaAnulacionService"/>.</summary>
    public Task<(bool success, string? error)> AnularVentaAsync(int id, string? motivo)
        => _anulacionService.AnularVentaAsync(id, motivo);


    /// <summary>Delega en <see cref="VentaDevolucionService"/>.</summary>
    public Task<(DevolucionVentaDto? devolucion, string? error)> CrearDevolucionParcialAsync(
        int ventaId, CrearDevolucionParcialDto dto, string? emailUsuario)
        => _devolucionService.CrearDevolucionParcialAsync(ventaId, dto, emailUsuario);


    // ─── Private helpers ──────────────────────────────

    private sealed record VentaContexto(
        Caja Caja,
        Sucursal Sucursal,
        string? NombreCliente,
        string? NitCliente,
        string NumeroVenta,
        List<RetencionRegla> ReglasRetencion,
        List<TramoBebidasAzucaradas> TramosBebidasAzucaradas,
        string PerfilComprador,
        Dictionary<Guid, Producto> ProductosMap,
        Dictionary<Guid, Stock> StocksMap,
        DateTime FechaVentaEfectiva,
        int? UsuarioIdVenta
    );

    private sealed record LineaProcessada(
        DetalleVenta Detalle,
        (Guid StreamId, object Evento) MartenEvento,
        (Stock Stock, string Nombre) StockVerificar,
        decimal Subtotal,
        decimal Descuento,
        decimal TotalImpuestos,
        bool RequiereFactura
    );

    private async Task<(VentaContexto? ctx, string? error)> CargarContextoVentaAsync(CrearVentaDto dto)
    {
        var caja = await _context.Cajas
            .FirstOrDefaultAsync(c => c.Id == dto.CajaId && c.SucursalId == dto.SucursalId);
        if (caja == null) return (null, "Caja no encontrada en esta sucursal.");
        if (caja.Estado != EstadoCaja.Abierta) return (null, "La caja no esta abierta.");

        var sucursal = await _context.Sucursales.FindAsync(dto.SucursalId);
        if (sucursal == null) return (null, "Sucursal no encontrada.");

        string? nombreCliente = null, nitCliente = null;
        string perfilComprador = "REGIMEN_COMUN";
        if (dto.ClienteId.HasValue)
        {
            var cliente = await _context.Terceros.FindAsync(dto.ClienteId.Value);
            if (cliente == null) return (null, "Cliente no encontrado.");
            nombreCliente = cliente.Nombre;
            nitCliente = cliente.Identificacion;
            perfilComprador = cliente.PerfilTributario;
        }

        var ultimaVenta = await _context.Ventas
            .IgnoreQueryFilters()
            .Where(v => v.SucursalId == dto.SucursalId)
            .OrderByDescending(v => v.Id)
            .Select(v => v.NumeroVenta)
            .FirstOrDefaultAsync();
        var consecutivo = 1;
        if (ultimaVenta != null && ultimaVenta.Contains('-'))
        {
            int.TryParse(ultimaVenta.Split('-').Last(), out consecutivo);
            consecutivo++;
        }
        var numeroVenta = $"V-{consecutivo:D6}";

        var reglasRetencion = await _context.RetencionesReglas.Where(r => r.Activo).ToListAsync();
        var hoyVenta = DateOnly.FromDateTime(DateTime.UtcNow);
        var tramosBebidasAzucaradas = await _context.TramosBebidasAzucaradas
            .Where(t => t.Activo && t.VigenciaDesde <= hoyVenta)
            .OrderBy(t => t.MaxGramosPor100ml)
            .ToListAsync();

        var productoIds = dto.Lineas.Select(l => l.ProductoId).Distinct().ToList();
        var productosMap = await _context.Productos
            .Include(p => p.ConceptoRetencion)
            .Include(p => p.Categoria)
            .Where(p => productoIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id);

        var productoImpuestoIds = productosMap.Values
            .Where(p => p.ImpuestoId.HasValue)
            .Select(p => p.ImpuestoId!.Value)
            .Distinct()
            .ToList();
        if (productoImpuestoIds.Count > 0)
        {
            var impuestosDict = await _context.Impuestos
                .IgnoreQueryFilters()
                .Where(i => productoImpuestoIds.Contains(i.Id))
                .ToDictionaryAsync(i => i.Id);
            foreach (var p in productosMap.Values)
                if (p.ImpuestoId.HasValue && impuestosDict.TryGetValue(p.ImpuestoId.Value, out var imp))
                    p.Impuesto = imp;
        }

        var stocksMap = await _context.Stock
            .Where(s => productoIds.Contains(s.ProductoId) && s.SucursalId == dto.SucursalId)
            .ToDictionaryAsync(s => s.ProductoId);

        var fechaVentaEfectiva = dto.FechaVenta.HasValue
            ? DateTime.SpecifyKind(dto.FechaVenta.Value, DateTimeKind.Utc)
            : DateTime.UtcNow;

        var emailCajero = _httpContextAccessor.HttpContext?.User?.FindFirst("email")?.Value
            ?? _httpContextAccessor.HttpContext?.User?.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value
            ?? _httpContextAccessor.HttpContext?.User?.FindFirst("preferred_username")?.Value;
        var subCajero = _httpContextAccessor.HttpContext?.User?.FindFirst("sub")?.Value
            ?? _httpContextAccessor.HttpContext?.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        int? usuarioIdVenta = await _context.ResolverUsuarioIdAsync(emailCajero, subCajero);

        return (new VentaContexto(
            caja, sucursal, nombreCliente, nitCliente, numeroVenta,
            reglasRetencion, tramosBebidasAzucaradas, perfilComprador,
            productosMap, stocksMap, fechaVentaEfectiva, usuarioIdVenta), null);
    }

    private async Task<(LineaProcessada? resultado, string? error)> ProcesarLineaAsync(
        LineaVentaDto linea, VentaContexto ctx, int sucursalId)
    {
        if (!ctx.ProductosMap.TryGetValue(linea.ProductoId, out var producto))
            return (null, $"Producto {linea.ProductoId} no encontrado.");
        if (!producto.Activo)
            return (null, $"Producto {producto.Nombre} esta inactivo.");

        if (!ctx.StocksMap.TryGetValue(linea.ProductoId, out var stock) || stock.Cantidad < linea.Cantidad)
            return (null, $"Stock insuficiente para {producto.Nombre}. " +
                $"Disponible: {stock?.Cantidad ?? 0}, Solicitado: {linea.Cantidad}");

        decimal precioUnitario;
        if (linea.PrecioUnitario.HasValue)
        {
            var (valido, errorPrecio) = await _precioService.ValidarPrecio(
                linea.ProductoId, sucursalId, linea.PrecioUnitario.Value, producto.Nombre);
            if (!valido) return (null, errorPrecio);
            precioUnitario = linea.PrecioUnitario.Value;
        }
        else
        {
            var precio = await _precioService.ResolverPrecio(linea.ProductoId, sucursalId);
            precioUnitario = precio.PrecioVenta;
        }

        var taxResult = _taxEngine.Calcular(new TaxRequest(
            ProductoId: linea.ProductoId,
            Cantidad: linea.Cantidad,
            PrecioUnitario: precioUnitario,
            Impuesto: producto.Impuesto,
            EsAlimentoUltraprocesado: producto.EsAlimentoUltraprocesado,
            GramosAzucarPor100ml: producto.GramosAzucarPor100ml,
            PerfilVendedor: ctx.Sucursal.PerfilTributario,
            PerfilComprador: ctx.PerfilComprador,
            CodigoMunicipio: ctx.Sucursal.CodigoMunicipio ?? string.Empty,
            ConceptoRetencionId: producto.ConceptoRetencionId,
            ValorUVT: ctx.Sucursal.ValorUVT,
            ReglasRetencion: ctx.ReglasRetencion,
            TramosBebidasAzucaradas: ctx.TramosBebidasAzucaradas
        ));

        var primerImpuesto = taxResult.Impuestos.FirstOrDefault();
        decimal porcentajeImpuesto = primerImpuesto?.Porcentaje ?? 0;
        decimal montoImpuesto = taxResult.TotalImpuestos;

        var streamId = InventarioAggregate.GenerarStreamId(linea.ProductoId, sucursalId);
        var aggregate = await _session.Events.AggregateStreamAsync<InventarioAggregate>(streamId);
        if (aggregate == null)
            return (null, $"No hay registro de inventario para {producto.Nombre}.");

        SalidaVentaRegistrada eventoVenta;
        try
        {
            eventoVenta = aggregate.RegistrarSalidaVenta(
                linea.Cantidad, precioUnitario, porcentajeImpuesto, montoImpuesto, ctx.NumeroVenta,
                ctx.UsuarioIdVenta, fechaMovimiento: ctx.FechaVentaEfectiva);
        }
        catch (InvalidOperationException)
        {
            return (null, $"Stock insuficiente para {producto.Nombre}. " +
                $"Disponible: {aggregate.Cantidad}, Solicitado: {linea.Cantidad}");
        }

        var (costoUnitario, loteId, numeroLoteSnapshot, lotesConsumidos) = await _ventaCosteoService.ConsumirAsync(
            linea.ProductoId, sucursalId, linea.Cantidad,
            ctx.Sucursal.MetodoCosteo, producto.ManejaLotes);

        stock.Cantidad -= linea.Cantidad;
        stock.UltimaActualizacion = DateTime.UtcNow;

        var lineaSubtotal = (precioUnitario * linea.Cantidad) - linea.Descuento;
        var detalle = new DetalleVenta
        {
            ProductoId = linea.ProductoId,
            NombreProducto = producto.Nombre,
            LoteInventarioId = loteId,
            NumeroLote = numeroLoteSnapshot,
            Cantidad = linea.Cantidad,
            PrecioUnitario = precioUnitario,
            CostoUnitario = costoUnitario,
            Descuento = linea.Descuento,
            PorcentajeImpuesto = porcentajeImpuesto,
            MontoImpuesto = montoImpuesto,
            Subtotal = lineaSubtotal,
            Lotes = lotesConsumidos.Select(l => new DetalleVentaLote
            {
                LoteInventarioId = l.LoteId,
                NumeroLote       = l.NumeroLote,
                Cantidad         = l.Cantidad,
                CostoUnitario    = l.CostoUnitario,
            }).ToList()
        };

        return (new LineaProcessada(
            detalle,
            (streamId, eventoVenta),
            (stock, producto.Nombre),
            precioUnitario * linea.Cantidad,
            linea.Descuento,
            montoImpuesto,
            taxResult.RequiereFacturaElectronica
        ), null);
    }

    private List<AsientoContableErp> ConstruirAsientosErp(
        List<DetalleVenta> detalles,
        Dictionary<Guid, Producto> productosMap,
        decimal total,
        string numeroVenta,
        int metodoPago,
        string centroCosto)
    {
        var ingresosErp = new Dictionary<string, decimal>();
        var ivaErp = new Dictionary<string, (string Cuenta, decimal Total)>();

        foreach (var det in detalles)
        {
            var prod = productosMap[det.ProductoId];
            var cuentaIngreso = prod.Categoria?.CuentaIngreso ?? "4135";
            ingresosErp.TryAdd(cuentaIngreso, 0);
            ingresosErp[cuentaIngreso] += det.Subtotal;

            if (det.PorcentajeImpuesto > 0)
            {
                var nombreImp = $"IVA {det.PorcentajeImpuesto * 100:0.##}%";
                var cuentaImp = prod.Impuesto?.CodigoCuentaContable ?? "2408";
                ivaErp.TryGetValue(nombreImp, out var cur);
                ivaErp[nombreImp] = (cuentaImp, cur.Total + det.MontoImpuesto);
            }
        }

        var cuentaDebito = ((MetodoPago)metodoPago) switch
        {
            MetodoPago.Tarjeta       => _erpOptions.CuentaTarjeta,
            MetodoPago.Transferencia => _erpOptions.CuentaTransferencia,
            _                        => _erpOptions.CuentaCaja
        };

        var asientos = new List<AsientoContableErp>();
        asientos.Add(new AsientoContableErp(
            cuentaDebito, centroCosto, "Debito", total,
            $"Cobro venta {numeroVenta} - {((MetodoPago)metodoPago)}"));
        foreach (var ing in ingresosErp)
            asientos.Add(new AsientoContableErp(
                ing.Key, centroCosto, "Credito", ing.Value,
                $"Ingreso venta {numeroVenta}"));
        foreach (var iva in ivaErp)
            asientos.Add(new AsientoContableErp(
                iva.Value.Cuenta, centroCosto, "Credito", iva.Value.Total,
                $"{iva.Key} generado venta {numeroVenta}"));

        return asientos;
    }

    private async Task EjecutarTransaccionAtomicaAsync(
        Venta venta,
        List<(Guid StreamId, object Evento)> pendingMartenEvents,
        string? externalId,
        CrearVentaDto dto,
        string? nitCliente,
        decimal total,
        List<AsientoContableErp> asientosVenta,
        List<DetalleVenta> detalles)
    {
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

            if (externalId != null)
            {
                // WorkOS user IDs son "user_xxx" — no GUIDs. Se deriva un stream key
                // determinístico para que todas las ventas del mismo usuario queden
                // en el mismo stream y las proyecciones acumulen correctamente.
                var userStreamId = new Guid(
                    System.Security.Cryptography.SHA256.HashData(
                        System.Text.Encoding.UTF8.GetBytes(externalId))[..16]);

                var ventaEvt = new VentaCompletadaEvent(
                    ExternalUserId: externalId,
                    SucursalId:     dto.SucursalId,
                    CajaId:         dto.CajaId,
                    HoraDelDia:     DateTime.UtcNow.Hour,
                    DiaSemana:      (int)DateTime.UtcNow.DayOfWeek,
                    Items:          detalles.Select(d => new VentaItemLine(d.ProductoId, d.NombreProducto, d.Cantidad, d.PrecioUnitario)).ToList(),
                    Total:          total,
                    ClienteId:      dto.ClienteId
                );
                martenTx.Events.Append(userStreamId, ventaEvt);
            }

            await martenTx.SaveChangesAsync();
            await _context.SaveChangesAsync();

            var ventaPayload = new VentaErpPayload(
                NumeroVenta: venta.NumeroVenta,
                NitCliente: nitCliente,
                MetodoPago: ((MetodoPago)dto.MetodoPago).ToString(),
                FechaVenta: venta.FechaVenta,
                SucursalId: venta.SucursalId,
                Asientos: asientosVenta,
                TotalOriginalDocumento: total);
            await _ventaErpService.EmitirVentaAsync(venta, asientosVenta, ventaPayload);
            await _context.SaveChangesAsync();

            await npgsqlTx.CommitAsync();
        });
    }

    private async Task NotificarVentaAsync(
        Venta venta,
        int sucursalId,
        string numeroVenta,
        decimal total,
        decimal subtotal,
        decimal descuentoTotal,
        decimal totalImpuestos,
        int metodoPago,
        int? clienteId,
        int cajaId,
        List<DetalleVenta> detalles,
        List<(Stock stock, string nombre)> stocksVerificar)
    {
        if (venta.RequiereFacturaElectronica)
            _facturacionBackground.Encolar(venta.Id);

        await _notificationService.EnviarNotificacionSucursalAsync(sucursalId, new NotificacionDto(
            "venta_completada", "Venta completada",
            $"Venta {numeroVenta} — ${total:N0}", "success", DateTime.UtcNow,
            new { VentaId = venta.Id, NumeroVenta = numeroVenta, Total = total }));

        foreach (var (stockItem, nombre) in stocksVerificar)
            if (stockItem.Cantidad >= 0 && stockItem.Cantidad <= stockItem.StockMinimo)
                await _notificationService.EnviarNotificacionSucursalAsync(sucursalId, new NotificacionDto(
                    "stock_bajo", "Stock bajo",
                    $"{nombre}: quedan {stockItem.Cantidad:F0} unidades", "warning", DateTime.UtcNow,
                    new { stockItem.ProductoId, NombreProducto = nombre, StockActual = stockItem.Cantidad }));

        await _activityLogService.LogActivityAsync(new ActivityLogDto(
            Accion: "CrearVenta",
            Tipo: TipoActividad.Venta,
            Descripcion: $"Venta {numeroVenta} creada. Total: ${total:N2}, Items: {detalles.Count}",
            SucursalId: sucursalId,
            TipoEntidad: "Venta",
            EntidadId: venta.Id.ToString(),
            EntidadNombre: numeroVenta,
            DatosNuevos: new
            {
                NumeroVenta = numeroVenta,
                Total = total,
                Subtotal = subtotal,
                Descuento = descuentoTotal,
                Impuestos = totalImpuestos,
                MetodoPago = ((MetodoPago)metodoPago).ToString(),
                CantidadItems = detalles.Count,
                ClienteId = clienteId,
                CajaId = cajaId,
                Productos = detalles.Select(d => new {
                    d.ProductoId,
                    d.NombreProducto,
                    d.Cantidad,
                    d.PrecioUnitario,
                    d.Subtotal
                })
            }
        ));
    }

    // ─── Mappers ───────────────────────────────────────

    public static VentaDto MapToDto(Venta v, string sucNombre, string cajaNombre, string? clienteNombre) =>
        new(
            v.Id, v.NumeroVenta,
            v.SucursalId, sucNombre,
            v.CajaId, cajaNombre,
            v.ClienteId, clienteNombre,
            v.Subtotal, v.Descuento, v.Impuestos, v.Total,
            v.Estado.ToString(), v.MetodoPago.ToString(),
            v.MontoPagado, v.Cambio,
            v.Observaciones, v.FechaVenta,
            v.Detalles.Select(d =>
            {
                var margen = d.PrecioUnitario > 0
                    ? Math.Round((d.PrecioUnitario - d.CostoUnitario) / d.PrecioUnitario * 100, 2)
                    : 0;
                return new DetalleVentaDto(
                    d.Id, d.ProductoId, d.NombreProducto, d.NumeroLote,
                    d.Cantidad, d.PrecioUnitario, d.CostoUnitario,
                    d.Descuento, d.PorcentajeImpuesto, d.MontoImpuesto,
                    d.Subtotal, margen,
                    d.Lotes.Select(l => new DetalleVentaLoteDto(
                        l.LoteInventarioId, l.NumeroLote, l.Cantidad, l.CostoUnitario)).ToList());
            }).ToList(),
            v.RequiereFacturaElectronica,
            v.SincronizadoErp,
            v.FechaSincronizacionErp,
            v.ErpReferencia,
            v.ErrorSincronizacion
        );

    public static DevolucionVentaDto MapDevolucionToDto(
        DevolucionVenta d, string numeroVenta, string? autorizadoPor) =>
        new(
            d.Id,
            d.VentaId,
            numeroVenta,
            d.NumeroDevolucion,
            d.Motivo,
            d.TotalDevuelto,
            d.FechaDevolucion,
            autorizadoPor,
            d.Detalles.Select(dd => new DetalleDevolucionDto(
                dd.Id,
                dd.ProductoId,
                dd.NombreProducto,
                dd.CantidadDevuelta,
                dd.PrecioUnitario,
                dd.CostoUnitario,
                dd.SubtotalDevuelto
            )).ToList(),
            d.SincronizadoErp,
            d.FechaSincronizacionErp,
            d.ErpReferencia,
            d.ErrorSincronizacion
        );
}
