using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using POS.Application.DTOs;
using POS.Application.Services;

namespace POS.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class FacturacionController : ControllerBase
{
    private readonly IFacturacionService _facturacion;
    private readonly ILogger<FacturacionController> _logger;

    public FacturacionController(IFacturacionService facturacion, ILogger<FacturacionController> logger)
    {
        _facturacion = facturacion;
        _logger = logger;
    }

    /// <summary>Obtener configuración del emisor para una sucursal.</summary>
    [HttpGet("configuracion/{sucursalId:int}")]
    [Authorize(Policy = "Admin")]
    [ProducesResponseType(typeof(ConfiguracionEmisorDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ConfiguracionEmisorDto>> ObtenerConfiguracion(int sucursalId)
    {
        var config = await _facturacion.ObtenerConfiguracionAsync(sucursalId);
        return config == null ? NotFound(new { error = "No hay configuración para esta sucursal." }) : Ok(config);
    }

    /// <summary>Crear o actualizar configuración del emisor para una sucursal.</summary>
    [HttpPut("configuracion/{sucursalId:int}")]
    [Authorize(Policy = "Admin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> ActualizarConfiguracion(
        int sucursalId, [FromBody] ActualizarConfiguracionEmisorDto dto)
    {
        await _facturacion.ActualizarConfiguracionAsync(sucursalId, dto);
        return NoContent();
    }

    /// <summary>Listar documentos electrónicos con filtros y paginación.</summary>
    [HttpGet("documentos")]
    [Authorize(Policy = "Supervisor")]
    [ProducesResponseType(typeof(PaginatedResult<DocumentoElectronicoDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PaginatedResult<DocumentoElectronicoDto>>> Listar(
        [FromQuery] int? sucursalId,
        [FromQuery] DateTime? fechaDesde,
        [FromQuery] DateTime? fechaHasta,
        [FromQuery] string? tipoDocumento,
        [FromQuery] int? estado,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20)
    {
        var filtro = new FiltroDocumentosElectronicosDto(
            sucursalId, fechaDesde, fechaHasta, tipoDocumento, estado, pageNumber, pageSize);
        var resultado = await _facturacion.ListarAsync(filtro);
        return Ok(resultado);
    }

    /// <summary>Obtener detalle de un documento electrónico.</summary>
    [HttpGet("documentos/{id:int}")]
    [Authorize(Policy = "Supervisor")]
    [ProducesResponseType(typeof(DocumentoElectronicoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DocumentoElectronicoDto>> ObtenerDocumento(int id)
    {
        var doc = await _facturacion.ObtenerAsync(id);
        return doc == null ? NotFound() : Ok(doc);
    }

    /// <summary>Descargar el XML UBL firmado de un documento.</summary>
    [HttpGet("documentos/{id:int}/xml")]
    [Authorize(Policy = "Supervisor")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DescargarXml(int id)
    {
        var xml = await _facturacion.ObtenerXmlAsync(id);
        if (xml == null) return NotFound();

        var bytes = System.Text.Encoding.UTF8.GetBytes(xml);
        return File(bytes, "application/xml", $"documento-{id}.xml");
    }

    /// <summary>Reintentar el envío de un documento rechazado a DIAN.</summary>
    [HttpPost("documentos/{id:int}/reintentar")]
    [Authorize(Policy = "Admin")]
    [ProducesResponseType(typeof(DocumentoElectronicoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DocumentoElectronicoDto>> Reintentar(int id)
    {
        var (doc, error) = await _facturacion.ReintentarAsync(id);
        if (error == "NOT_FOUND") return NotFound();
        if (error != null) return BadRequest(new { error });
        return Ok(doc);
    }

    /// <summary>Consultar el estado actual de un documento en DIAN (en vivo).</summary>
    [HttpGet("documentos/{id:int}/estado-dian")]
    [Authorize(Policy = "Supervisor")]
    [ProducesResponseType(typeof(DianRespuesta), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DianRespuesta>> ConsultarEstadoDian(int id)
    {
        var respuesta = await _facturacion.ConsultarEstadoDianAsync(id);
        return respuesta == null ? NotFound() : Ok(respuesta);
    }

    /// <summary>Emitir manualmente la factura de una venta (para ventas que fallaron automáticamente).</summary>
    [HttpPost("documentos/emitir-venta/{ventaId:int}")]
    [Authorize(Policy = "Admin")]
    [ProducesResponseType(typeof(DocumentoElectronicoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<DocumentoElectronicoDto>> EmitirFacturaManual(int ventaId)
    {
        var (doc, error) = await _facturacion.EmitirFacturaVentaAsync(ventaId);
        if (error == "NOT_FOUND") return NotFound();
        if (error != null) return BadRequest(new { error });
        return Ok(doc);
    }
}
