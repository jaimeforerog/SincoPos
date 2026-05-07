using System.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using POS.Application.DTOs;
using POS.Application.Services;
using POS.Infrastructure.Data;
using POS.Infrastructure.Data.Entities;
using POS.Infrastructure.Services.Erp;

namespace POS.Infrastructure.Services;

// VentaService está dividido en partial classes por responsabilidad:
//   - VentaService.cs              → ctor, campos, orquestación CrearVenta, delegates
//   - VentaService.Contexto.cs     → carga de contexto y procesamiento por línea
//   - VentaService.Erp.cs          → asientos contables y transacción atómica (Marten + EF + ERP)
//   - VentaService.Notificaciones  → SignalR + activity log + facturación electrónica
//   - VentaService.Mappers.cs      → mapeo entidad → DTO (estáticos, usados por controllers)
public sealed partial class VentaService : IVentaService
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

    public Task<(bool success, string? error)> AnularVentaAsync(int id, string? motivo)
        => _anulacionService.AnularVentaAsync(id, motivo);

    public Task<(DevolucionVentaDto? devolucion, string? error)> CrearDevolucionParcialAsync(
        int ventaId, CrearDevolucionParcialDto dto, string? emailUsuario)
        => _devolucionService.CrearDevolucionParcialAsync(ventaId, dto, emailUsuario);
}
