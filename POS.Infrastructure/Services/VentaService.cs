using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using POS.Application.DTOs;
using POS.Application.Services;
using POS.Domain.Aggregates;
using POS.Domain.Events.Venta;
using POS.Infrastructure.Data;
using POS.Infrastructure.Data.Entities;
using POS.Infrastructure.Services.Erp;

namespace POS.Infrastructure.Services;

public class VentaService : IVentaService
{
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
        // Verificar caja abierta
        var caja = await _context.Cajas
            .FirstOrDefaultAsync(c => c.Id == dto.CajaId && c.SucursalId == dto.SucursalId);
        if (caja == null)
            return (null, "Caja no encontrada en esta sucursal.");
        if (caja.Estado != EstadoCaja.Abierta)
            return (null, "La caja no esta abierta.");

        // Obtener sucursal para metodo de costeo
        var sucursal = await _context.Sucursales.FindAsync(dto.SucursalId);
        if (sucursal == null)
            return (null, "Sucursal no encontrada.");

        // Verificar cliente (si aplica)
        string? nombreCliente = null;
        string? nitCliente = null;
        if (dto.ClienteId.HasValue)
        {
            var cliente = await _context.Terceros.FindAsync(dto.ClienteId.Value);
            if (cliente == null) return (null, "Cliente no encontrado.");
            nombreCliente = cliente.Nombre;
            nitCliente = cliente.Identificacion;
        }

        // Generar numero de venta (IgnoreQueryFilters evita colisión de consecutivo entre empresas)
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

        // Cargar reglas de retención activas de la sucursal
        var reglasRetencion = await _context.RetencionesReglas
            .Where(r => r.Activo)
            .ToListAsync();

        var hoyVenta = DateOnly.FromDateTime(DateTime.UtcNow);
        var tramosBebidasAzucaradas = await _context.TramosBebidasAzucaradas
            .Where(t => t.Activo && t.VigenciaDesde <= hoyVenta)
            .OrderBy(t => t.MaxGramosPor100ml)
            .ToListAsync();

        // Perfil del comprador (si aplica)
        string perfilComprador = "REGIMEN_COMUN";
        if (dto.ClienteId.HasValue)
        {
            var cliente = await _context.Terceros.FindAsync(dto.ClienteId.Value);
            if (cliente != null) perfilComprador = cliente.PerfilTributario;
        }

        // Pre-cargar productos y stocks en lote (elimina N+1)
        var productoIds = dto.Lineas.Select(l => l.ProductoId).Distinct().ToList();

        var productosMap = await _context.Productos
            .Include(p => p.ConceptoRetencion)
            .Include(p => p.Categoria)
            .Where(p => productoIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id);

        // Cargar impuestos con IgnoreQueryFilters para acceder a registros globales (EmpresaId = null)
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

        // Fecha efectiva de la venta (usuario la puede editar; no puede ser futura)
        var fechaVentaEfectiva = dto.FechaVenta.HasValue
            ? DateTime.SpecifyKind(dto.FechaVenta.Value, DateTimeKind.Utc)
            : DateTime.UtcNow;

        // Resolver usuarioId ANTES del loop — primero por email, fallback por ExternalId (WorkOS sub)
        var emailCajero = _httpContextAccessor.HttpContext?.User?.FindFirst("email")?.Value
            ?? _httpContextAccessor.HttpContext?.User?.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value
            ?? _httpContextAccessor.HttpContext?.User?.FindFirst("preferred_username")?.Value;
        var subCajero = _httpContextAccessor.HttpContext?.User?.FindFirst("sub")?.Value
            ?? _httpContextAccessor.HttpContext?.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        int? usuarioIdVenta = await _context.ResolverUsuarioIdAsync(emailCajero, subCajero);

        // Procesar cada linea con el TaxEngine
        var detalles = new List<DetalleVenta>();
        var stocksVerificar = new List<(Stock stock, string nombre)>();
        var pendingMartenEvents = new List<(Guid StreamId, object Evento)>();
        decimal subtotal = 0;
        decimal descuentoTotal = 0;
        decimal totalImpuestos = 0;
        bool requiereFacturaElectronica = false;

        foreach (var linea in dto.Lineas)
        {
            // Obtener producto con su impuesto (desde cache en memoria)
            if (!productosMap.TryGetValue(linea.ProductoId, out var producto))
                return (null, $"Producto {linea.ProductoId} no encontrado.");
            if (!producto.Activo)
                return (null, $"Producto {producto.Nombre} esta inactivo.");

            // Verificar stock (desde cache en memoria)
            if (!stocksMap.TryGetValue(linea.ProductoId, out var stock) || stock.Cantidad < linea.Cantidad)
                return (null, $"Stock insuficiente para {producto.Nombre}. " +
                    $"Disponible: {stock?.Cantidad ?? 0}, Solicitado: {linea.Cantidad}");

            // Resolver precio
            decimal precioUnitario;
            if (linea.PrecioUnitario.HasValue)
            {
                var (valido, errorPrecio) = await _precioService.ValidarPrecio(
                    linea.ProductoId, dto.SucursalId, linea.PrecioUnitario.Value);
                if (!valido) return (null, errorPrecio);
                precioUnitario = linea.PrecioUnitario.Value;
            }
            else
            {
                var precio = await _precioService.ResolverPrecio(linea.ProductoId, dto.SucursalId);
                precioUnitario = precio.PrecioVenta;
            }

            // ── Calcular impuestos con el TaxEngine ────────────────────────────
            var taxResult = _taxEngine.Calcular(new TaxRequest(
                ProductoId: linea.ProductoId,
                Cantidad: linea.Cantidad,
                PrecioUnitario: precioUnitario,
                Impuesto: producto.Impuesto,
                EsAlimentoUltraprocesado: producto.EsAlimentoUltraprocesado,
                GramosAzucarPor100ml: producto.GramosAzucarPor100ml,
                PerfilVendedor: sucursal.PerfilTributario,
                PerfilComprador: perfilComprador,
                CodigoMunicipio: sucursal.CodigoMunicipio ?? string.Empty,
                ConceptoRetencionId: producto.ConceptoRetencionId,
                ValorUVT: sucursal.ValorUVT,
                ReglasRetencion: reglasRetencion,
                TramosBebidasAzucaradas: tramosBebidasAzucaradas
            ));

            // Acumular flag de factura electrónica (si cualquier línea lo requiere)
            if (taxResult.RequiereFacturaElectronica)
                requiereFacturaElectronica = true;

            // Usar el primer impuesto aplicado para compatibilidad con DetalleVenta
            var primerImpuesto = taxResult.Impuestos.FirstOrDefault();
            decimal porcentajeImpuesto = primerImpuesto?.Porcentaje ?? 0;
            decimal montoImpuesto = taxResult.TotalImpuestos;
            totalImpuestos += montoImpuesto;

            // Consumir inventario via Event Sourcing
            var streamId = InventarioAggregate.GenerarStreamId(linea.ProductoId, dto.SucursalId);
            var aggregate = await _session.Events.AggregateStreamAsync<InventarioAggregate>(streamId);
            if (aggregate == null)
                return (null, $"No hay registro de inventario para {producto.Nombre}.");

            var eventoVenta = aggregate.RegistrarSalidaVenta(
                linea.Cantidad, precioUnitario, porcentajeImpuesto, montoImpuesto, numeroVenta,
                usuarioIdVenta, fechaMovimiento: fechaVentaEfectiva);
            pendingMartenEvents.Add((streamId, eventoVenta));

            // Consumir inventario con la estrategia de costeo (delegada a IVentaCosteoService)
            var (costoUnitario, loteId, numeroLoteSnapshot) = await _ventaCosteoService.ConsumirAsync(
                linea.ProductoId, dto.SucursalId, linea.Cantidad,
                sucursal.MetodoCosteo, producto.ManejaLotes);

            // Actualizar stock en EF Core
            stock.Cantidad -= linea.Cantidad;
            stock.UltimaActualizacion = DateTime.UtcNow;
            stocksVerificar.Add((stock, producto.Nombre));

            // Crear detalle
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
                Subtotal = lineaSubtotal
            };
            detalles.Add(detalle);

            subtotal += precioUnitario * linea.Cantidad;
            descuentoTotal += linea.Descuento;
        }

        var total = subtotal - descuentoTotal + totalImpuestos;
        var cambio = dto.MontoPagado.HasValue ? dto.MontoPagado.Value - total : (decimal?)null;

        if (dto.MontoPagado.HasValue && dto.MontoPagado.Value < total)
            return (null, $"Monto pagado ({dto.MontoPagado.Value}) es menor al total ({total}).");

        // ── Capa 12: Supervisión Ética ────────────────────────────────────
        var lineasEtica = detalles.Select(d =>
        {
            var prod = productosMap[d.ProductoId];
            return new LineaVentaEticaDto(d.ProductoId, d.PrecioUnitario, prod.PrecioVenta, d.Descuento, d.Cantidad);
        }).ToList();

        var guardDto = new EvaluarVentaEticaDto(
            dto.SucursalId, null, subtotal, descuentoTotal, detalles.Count, lineasEtica);
        var (permitido, errorGuard) = await _ethicalGuard.EvaluarVentaAsync(guardDto);
        if (!permitido) return (null, errorGuard);

        var asientosVenta = ConstruirAsientosErp(
            detalles, productosMap, total, numeroVenta, dto.MetodoPago, sucursal.CentroCosto ?? string.Empty);

        // Crear venta
        var venta = new Venta
        {
            NumeroVenta = numeroVenta,
            EmpresaId = sucursal.EmpresaId,
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
            FechaVenta = fechaVentaEfectiva,
            RequiereFacturaElectronica = requiereFacturaElectronica,
            Detalles = detalles
        };
        _context.Ventas.Add(venta);

        // Actualizar monto de caja
        caja.MontoActual += total;

        // Capa 5 — Anticipación funcional: obtener externalId del cajero autenticado
        var externalId = _httpContextAccessor.HttpContext?.User?.FindFirst("oid")?.Value
            ?? _httpContextAccessor.HttpContext?.User?.FindFirst("http://schemas.microsoft.com/identity/claims/objectidentifier")?.Value
            ?? _httpContextAccessor.HttpContext?.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
            ?? _httpContextAccessor.HttpContext?.User?.FindFirst("sub")?.Value;

        await EjecutarTransaccionAtomicaAsync(venta, pendingMartenEvents, externalId, dto, nitCliente, total, asientosVenta, detalles);

        _logger.LogInformation("Venta {NumeroVenta} completada. Total: {Total}, Items: {Items}",
            numeroVenta, total, detalles.Count);

        await NotificarVentaAsync(venta, dto.SucursalId, numeroVenta, total, subtotal,
            descuentoTotal, totalImpuestos, dto.MetodoPago, dto.ClienteId, dto.CajaId,
            detalles, stocksVerificar);

        return (MapToDto(venta, sucursal.Nombre, caja.Nombre, nombreCliente), null);
    }

    /// <summary>Delega en <see cref="VentaAnulacionService"/>.</summary>
    public Task<(bool success, string? error)> AnularVentaAsync(int id, string? motivo)
        => _anulacionService.AnularVentaAsync(id, motivo);


    /// <summary>Delega en <see cref="VentaDevolucionService"/>.</summary>
    public Task<(DevolucionVentaDto? devolucion, string? error)> CrearDevolucionParcialAsync(
        int ventaId, CrearDevolucionParcialDto dto, string? emailUsuario)
        => _devolucionService.CrearDevolucionParcialAsync(ventaId, dto, emailUsuario);


    // ─── Private helpers ──────────────────────────────

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

            if (externalId != null && Guid.TryParse(externalId, out var userStreamId))
            {
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
                    d.Subtotal, margen);
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
