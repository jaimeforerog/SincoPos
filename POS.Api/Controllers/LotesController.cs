using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using POS.Application.DTOs;
using POS.Application.Services;

namespace POS.Api.Controllers;

[Authorize]
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]")]
public class LotesController : ControllerBase
{
    private readonly ILoteService _loteService;

    public LotesController(ILoteService loteService)
    {
        _loteService = loteService;
    }

    /// <summary>
    /// Obtener lotes de un producto en una sucursal.
    /// Ordenados por fecha de vencimiento ASC (FEFO), luego por fecha de entrada.
    /// </summary>
    /// <param name="productoId">Id del producto.</param>
    /// <param name="sucursalId">Id de la sucursal.</param>
    /// <param name="soloVigentes">Si true (default), excluye lotes agotados (cantidad = 0).</param>
    [HttpGet]
    [Authorize(Policy = "Supervisor")]
    [ProducesResponseType(typeof(List<LoteDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<List<LoteDto>>> ObtenerLotes(
        [FromQuery] Guid productoId,
        [FromQuery] int sucursalId,
        [FromQuery] bool soloVigentes = true)
    {
        if (productoId == Guid.Empty)
            return Problem(detail: "productoId es requerido.", statusCode: StatusCodes.Status400BadRequest);
        if (sucursalId <= 0)
            return Problem(detail: "sucursalId es requerido.", statusCode: StatusCodes.Status400BadRequest);

        var lotes = await _loteService.ObtenerLotesAsync(productoId, sucursalId, soloVigentes);
        return Ok(lotes);
    }

    /// <summary>
    /// Lotes próximos a vencer en una sucursal dentro de los próximos N días.
    /// </summary>
    /// <param name="sucursalId">Id de la sucursal.</param>
    /// <param name="diasAnticipacion">Ventana de días hacia adelante. Default: configurado en la sucursal (30).</param>
    [HttpGet("proximos-vencer")]
    [Authorize(Policy = "Supervisor")]
    [ProducesResponseType(typeof(List<AlertaLoteDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<AlertaLoteDto>>> ProximosAVencer(
        [FromQuery] int sucursalId,
        [FromQuery] int diasAnticipacion = 30)
    {
        if (sucursalId <= 0)
            return Problem(detail: "sucursalId es requerido.", statusCode: StatusCodes.Status400BadRequest);

        var alertas = await _loteService.ObtenerProximosAVencerAsync(sucursalId, diasAnticipacion);
        return Ok(alertas);
    }

    /// <summary>
    /// Todas las alertas de vencimiento de todas las sucursales activas,
    /// usando la configuración de días de anticipación de cada una.
    /// Usado por el background service y el dashboard global.
    /// </summary>
    [HttpGet("alertas")]
    [Authorize(Policy = "Supervisor")]
    [ProducesResponseType(typeof(List<AlertaLoteDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<AlertaLoteDto>>> ObtenerAlertas()
    {
        var alertas = await _loteService.ObtenerTodasLasAlertasAsync();
        return Ok(alertas);
    }

    /// <summary>
    /// Actualizar número de lote y/o fecha de vencimiento de un lote existente.
    /// Útil para corregir datos al recibir la documentación del proveedor.
    /// </summary>
    /// <param name="id">Id del lote.</param>
    /// <summary>
    /// Trazabilidad completa de un lote: entrada, ventas, devoluciones y traslados.
    /// </summary>
    [HttpGet("{id:int}/trazabilidad")]
    [Authorize(Policy = "Supervisor")]
    [ProducesResponseType(typeof(TrazabilidadLoteDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TrazabilidadLoteDto>> ObtenerTrazabilidad(int id)
    {
        var (result, error) = await _loteService.ObtenerTrazabilidadAsync(id);
        if (error == "NOT_FOUND")
            return Problem(detail: "Lote no encontrado.", statusCode: StatusCodes.Status404NotFound);
        return Ok(result);
    }

    /// <summary>
    /// Reporte completo de lotes: stock disponible, fechas de vencimiento y estado.
    /// Abarca todos los productos de todas las sucursales en una sola consulta.
    /// </summary>
    [HttpGet("reporte")]
    [Authorize(Policy = "Supervisor")]
    [ProducesResponseType(typeof(ReporteLotesDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<ReporteLotesDto>> ObtenerReporte(
        [FromQuery] int? sucursalId = null,
        [FromQuery] Guid? productoId = null,
        [FromQuery] bool soloConStock = true,
        [FromQuery] string? estadoVencimiento = null)
    {
        var query = new ReporteLotesQueryDto(sucursalId, productoId, soloConStock, estadoVencimiento);
        var reporte = await _loteService.ObtenerReporteAsync(query);
        return Ok(reporte);
    }

    [HttpPut("{id:int}")]
    [Authorize(Policy = "Supervisor")]
    [ProducesResponseType(typeof(LoteDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<LoteDto>> ActualizarLote(int id, ActualizarLoteDto dto)
    {
        var (result, error) = await _loteService.ActualizarLoteAsync(id, dto);
        if (error == "NOT_FOUND")
            return Problem(detail: "Lote no encontrado.", statusCode: StatusCodes.Status404NotFound);
        if (error != null)
            return Problem(detail: error, statusCode: StatusCodes.Status400BadRequest);
        return Ok(result);
    }
}
