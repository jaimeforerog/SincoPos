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

    public ReportesController(IReportesService reportesService)
    {
        _reportesService = reportesService;
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
            return BadRequest("La fecha desde no puede ser mayor que la fecha hasta.");

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
            return NotFound(new { error });

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
        var dashboard = await _reportesService.ObtenerDashboardAsync(sucursalId);
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
            return BadRequest("La fecha desde no puede ser mayor que la fecha hasta.");

        var topProductos = await _reportesService.ObtenerTopProductosAsync(
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
            return BadRequest("El ID del producto es requerido.");

        if (fechaDesde > fechaHasta)
            return BadRequest("La fecha desde no puede ser mayor que la fecha hasta.");

        try
        {
            var reporte = await _reportesService.ObtenerKardexAsync(
                productoId, sucursalId, fechaDesde, fechaHasta);

            return Ok(reporte);
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }
}
