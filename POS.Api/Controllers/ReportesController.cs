using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using POS.Application.DTOs;
using POS.Application.Services;

namespace POS.Api.Controllers;

[Authorize]
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]")]
public class ReportesController : ControllerBase
{
    private readonly IReportesService _reportesService;
    private readonly IDashboardService _dashboardService;
    private readonly IKardexService _kardexService;
    private readonly IActivityLogService _activityLogService;

    public ReportesController(
        IReportesService reportesService,
        IDashboardService dashboardService,
        IKardexService kardexService,
        IActivityLogService activityLogService)
    {
        _reportesService = reportesService;
        _dashboardService = dashboardService;
        _kardexService = kardexService;
        _activityLogService = activityLogService;
    }

    /// <summary>
    /// Reporte de ventas por período: totales, utilidad, ticket promedio, por método de pago y por día.
    /// </summary>
    /// <param name="metodoPago">0 = Efectivo, 1 = Tarjeta, 2 = Transferencia. Null = todos.</param>
    [HttpGet("ventas")]
    [ProducesResponseType(typeof(ReporteVentasDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ReporteVentasDto>> ObtenerReporteVentas(
        [FromQuery] DateTime fechaDesde,
        [FromQuery] DateTime fechaHasta,
        [FromQuery] int? sucursalId = null,
        [FromQuery] int? metodoPago = null)
    {
        if (fechaDesde > fechaHasta)
            return Problem(detail: "La fecha desde no puede ser mayor que la fecha hasta.", statusCode: StatusCodes.Status400BadRequest);

        var reporte = await _reportesService.ObtenerReporteVentasAsync(
            fechaDesde, fechaHasta, sucursalId, metodoPago);

        return Ok(reporte);
    }

    /// <summary>
    /// Reporte de inventario valorizado: costo total, valor de venta y utilidad potencial por producto.
    /// </summary>
    [HttpGet("inventario-valorizado")]
    [ProducesResponseType(typeof(ReporteInventarioValorizadoDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<ReporteInventarioValorizadoDto>> ObtenerInventarioValorizado(
        [FromQuery] int? sucursalId = null,
        [FromQuery] int? categoriaId = null,
        [FromQuery] bool soloConStock = false)
    {
        var reporte = await _reportesService.ObtenerInventarioValorizadoAsync(
            sucursalId, categoriaId, soloConStock);

        return Ok(reporte);
    }

    /// <summary>
    /// Reporte de movimientos de caja: ventas por método de pago, totales y cuadre.
    /// Para cajas abiertas, si no se especifican fechas usa desde la apertura hasta ahora.
    /// </summary>
    [HttpGet("caja/{cajaId}")]
    [ProducesResponseType(typeof(ReporteCajaDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ReporteCajaDto>> ObtenerReporteCaja(
        int cajaId,
        [FromQuery] DateTime? fechaDesde = null,
        [FromQuery] DateTime? fechaHasta = null)
    {
        var (reporte, error) = await _reportesService.ObtenerReporteCajaAsync(
            cajaId, fechaDesde, fechaHasta);

        if (error != null)
            return Problem(detail: error, statusCode: StatusCodes.Status404NotFound);

        return Ok(reporte);
    }

    /// <summary>
    /// Dashboard con métricas del día actual (zona horaria Colombia), top 5 productos y alertas de stock.
    /// </summary>
    [HttpGet("dashboard")]
    [ProducesResponseType(typeof(DashboardDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<DashboardDto>> ObtenerDashboard(
        [FromQuery] int? sucursalId = null)
    {
        var dashboard = await _dashboardService.ObtenerDashboardAsync(sucursalId);
        return Ok(dashboard);
    }

    /// <summary>
    /// Top productos más vendidos en un período, ordenados por cantidad vendida.
    /// </summary>
    /// <param name="limite">Número máximo de productos a retornar. Default 10.</param>
    [HttpGet("top-productos")]
    [ProducesResponseType(typeof(List<TopProductoDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<List<TopProductoDto>>> ObtenerTopProductos(
        [FromQuery] DateTime fechaDesde,
        [FromQuery] DateTime fechaHasta,
        [FromQuery] int? sucursalId = null,
        [FromQuery] int limite = 10)
    {
        if (fechaDesde > fechaHasta)
            return Problem(detail: "La fecha desde no puede ser mayor que la fecha hasta.", statusCode: StatusCodes.Status400BadRequest);

        var topProductos = await _dashboardService.ObtenerTopProductosAsync(
            fechaDesde, fechaHasta, sucursalId, limite);

        return Ok(topProductos);
    }
    /// <summary>
    /// Kardex de inventario: historial detallado de movimientos (entradas, salidas, ajustes) y saldos.
    /// </summary>
    [HttpGet("kardex")]
    [ProducesResponseType(typeof(ReporteKardexDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ReporteKardexDto>> ObtenerKardex(
        [FromQuery] Guid productoId,
        [FromQuery] int sucursalId,
        [FromQuery] DateTime fechaDesde,
        [FromQuery] DateTime fechaHasta)
    {
        if (productoId == Guid.Empty)
            return Problem(detail: "El ID del producto es requerido.", statusCode: StatusCodes.Status400BadRequest);

        if (fechaDesde > fechaHasta)
            return Problem(detail: "La fecha desde no puede ser mayor que la fecha hasta.", statusCode: StatusCodes.Status400BadRequest);

        try
        {
            var reporte = await _kardexService.ObtenerKardexAsync(
                productoId, sucursalId, fechaDesde, fechaHasta);

            return Ok(reporte);
        }
        catch (InvalidOperationException ex)
        {
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status404NotFound);
        }
    }

    /// <summary>
    /// Auditoría de compras: KPIs del período y log paginado de eventos sobre órdenes de compra.
    /// </summary>
    [HttpGet("auditoria-compras")]
    [Authorize(Policy = "Supervisor")]
    [ProducesResponseType(typeof(ReporteAuditoriaComprasDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ReporteAuditoriaComprasDto>> ObtenerAuditoriaCompras(
        [FromQuery] DateTime fechaDesde,
        [FromQuery] DateTime fechaHasta,
        [FromQuery] int? sucursalId = null,
        [FromQuery] int? proveedorId = null,
        [FromQuery] string? usuarioEmail = null,
        [FromQuery] string? accion = null,
        [FromQuery] bool? soloErrores = null,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 50)
    {
        if (fechaDesde > fechaHasta)
            return Problem(detail: "La fecha desde no puede ser mayor que la fecha hasta.", statusCode: StatusCodes.Status400BadRequest);

        var query = new ReporteAuditoriaComprasQueryDto(
            fechaDesde, fechaHasta, sucursalId, proveedorId,
            usuarioEmail, accion, soloErrores, pageNumber, pageSize);

        var reporte = await _reportesService.ObtenerAuditoriaComprasAsync(query);
        return Ok(reporte);
    }

    /// <summary>
    /// Auditoría de ventas: KPIs del período y log paginado de eventos sobre ventas.
    /// </summary>
    [HttpGet("auditoria-ventas")]
    [Authorize(Policy = "Supervisor")]
    [ProducesResponseType(typeof(ReporteAuditoriaVentasDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ReporteAuditoriaVentasDto>> ObtenerAuditoriaVentas(
        [FromQuery] DateTime fechaDesde,
        [FromQuery] DateTime fechaHasta,
        [FromQuery] int? sucursalId = null,
        [FromQuery] int? clienteId = null,
        [FromQuery] string? usuarioEmail = null,
        [FromQuery] string? accion = null,
        [FromQuery] bool? soloErrores = null,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 50)
    {
        if (fechaDesde > fechaHasta)
            return Problem(detail: "La fecha desde no puede ser mayor que la fecha hasta.", statusCode: StatusCodes.Status400BadRequest);

        var query = new ReporteAuditoriaVentasQueryDto(
            fechaDesde, fechaHasta, sucursalId, clienteId,
            usuarioEmail, accion, soloErrores, pageNumber, pageSize);

        var reporte = await _reportesService.ObtenerAuditoriaVentasAsync(query);
        return Ok(reporte);
    }

    /// <summary>
    /// Timeline completo de cambios sobre una venta específica.
    /// </summary>
    [HttpGet("auditoria-ventas/venta/{ventaId:int}")]
    [Authorize(Policy = "Supervisor")]
    [ProducesResponseType(typeof(HistorialEntidadDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<HistorialEntidadDto>> ObtenerHistorialVenta(int ventaId)
    {
        var historial = await _activityLogService.GetEntityHistoryAsync("Venta", ventaId.ToString());
        return Ok(historial);
    }

    /// <summary>
    /// Timeline completo de cambios sobre una orden de compra específica.
    /// </summary>
    [HttpGet("auditoria-compras/orden/{ordenId:int}")]
    [Authorize(Policy = "Supervisor")]
    [ProducesResponseType(typeof(HistorialEntidadDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<HistorialEntidadDto>> ObtenerHistorialOrden(int ordenId)
    {
        var historial = await _activityLogService.GetEntityHistoryAsync("OrdenCompra", ordenId.ToString());
        return Ok(historial);
    }
}
