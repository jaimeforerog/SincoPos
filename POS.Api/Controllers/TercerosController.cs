using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using POS.Application.DTOs;
using POS.Application.Services;
using POS.Infrastructure.Services;

namespace POS.Api.Controllers;

[Authorize]
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]")]
public class TercerosController : ControllerBase
{
    private readonly ITerceroService _service;
    private readonly ILogger<TercerosController> _logger;

    public TercerosController(ITerceroService service, ILogger<TercerosController> logger)
    {
        _service = service;
        _logger = logger;
    }

    /// <summary>Calcular el Dígito de Verificación (DV) de un NIT (módulo 11 DIAN)</summary>
    [HttpGet("calcular-dv")]
    [AllowAnonymous]
    public ActionResult CalcularDV([FromQuery] string nit)
    {
        if (string.IsNullOrWhiteSpace(nit) || !nit.Any(char.IsDigit))
            return BadRequest(new { error = "NIT inválido." });

        var dv = TerceroLocalService.CalcularDV(nit);
        return Ok(new { dv });
    }

    /// <summary>Crear un nuevo tercero (solo modo Local)</summary>
    [HttpPost]
    [Authorize(Policy = "Cajero")]
    public async Task<ActionResult<TerceroDto>> CrearTercero(
        CrearTerceroDto dto,
        [FromServices] IValidator<CrearTerceroDto> validator)
    {
        var validationResult = await validator.ValidateAsync(dto);
        if (!validationResult.IsValid)
        {
            var errors = validationResult.Errors
                .GroupBy(e => e.PropertyName)
                .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray());
            return BadRequest(new { errors });
        }

        var (result, error) = await _service.CrearAsync(dto);
        if (error != null)
            return Conflict(new { error });

        _logger.LogInformation("Tercero creado. Id: {Id}, Nombre: {Nombre}", result!.Id, result.Nombre);
        return CreatedAtAction(nameof(ObtenerTercero), new { id = result.Id }, result);
    }

    /// <summary>Obtener un tercero por ID</summary>
    [HttpGet("{id:int}")]
    public async Task<ActionResult<TerceroDto>> ObtenerTercero(int id)
    {
        var tercero = await _service.ObtenerPorIdAsync(id);
        if (tercero == null)
            return NotFound(new { error = $"Tercero {id} no encontrado." });

        return Ok(tercero);
    }

    /// <summary>Buscar terceros por nombre o identificacion</summary>
    [HttpGet]
    [ProducesResponseType(typeof(PaginatedResult<TerceroDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PaginatedResult<TerceroDto>>> BuscarTerceros(
        [FromQuery] string? q = null,
        [FromQuery] string? tipoTercero = null,
        [FromQuery] bool incluirInactivos = false,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        if (pageSize > 100) pageSize = 100;
        if (page < 1) page = 1;

        var terceros = await _service.BuscarAsync(q, tipoTercero, incluirInactivos, page, pageSize);
        return Ok(terceros);
    }

    /// <summary>Buscar un tercero por numero de identificacion</summary>
    [HttpGet("identificacion/{identificacion}")]
    public async Task<ActionResult<TerceroDto>> BuscarPorIdentificacion(string identificacion)
    {
        var tercero = await _service.ObtenerPorIdentificacionAsync(identificacion);
        if (tercero == null)
            return NotFound(new { error = $"No se encontro un tercero con identificacion {identificacion}." });

        return Ok(tercero);
    }

    /// <summary>Actualizar un tercero</summary>
    [HttpPut("{id:int}")]
    [Authorize(Policy = "Cajero")]
    public async Task<ActionResult> ActualizarTercero(
        int id,
        ActualizarTerceroDto dto,
        [FromServices] IValidator<ActualizarTerceroDto> validator)
    {
        var validationResult = await validator.ValidateAsync(dto);
        if (!validationResult.IsValid)
        {
            var errors = validationResult.Errors
                .GroupBy(e => e.PropertyName)
                .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray());
            return BadRequest(new { errors });
        }

        var (success, error) = await _service.ActualizarAsync(id, dto);
        if (!success)
        {
            if (error!.Contains("no encontrado"))
                return NotFound(new { error });
            return BadRequest(new { error });
        }

        _logger.LogInformation("Tercero {Id} actualizado.", id);
        return NoContent();
    }

    /// <summary>Desactivar un tercero (soft delete)</summary>
    [HttpDelete("{id:int}")]
    [Authorize(Policy = "Supervisor")]
    public async Task<ActionResult> DesactivarTercero(int id)
    {
        var (success, error) = await _service.DesactivarAsync(id);
        if (!success)
            return NotFound(new { error });

        _logger.LogInformation("Tercero {Id} desactivado.", id);
        return NoContent();
    }

    // ── Importación Excel ─────────────────────────────────────────────────────

    /// <summary>Descarga la plantilla Excel para importar terceros</summary>
    [HttpGet("plantilla")]
    [AllowAnonymous]
    public IActionResult DescargarPlantilla()
    {
        var bytes = _service.GenerarPlantillaExcel();
        return File(bytes,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            "plantilla_terceros.xlsx");
    }

    /// <summary>Importa terceros desde un archivo Excel</summary>
    [HttpPost("importar")]
    [Authorize(Policy = "Supervisor")]
    public async Task<ActionResult<ResultadoImportacionTercerosDto>> ImportarDesdeExcel(IFormFile archivo)
    {
        if (archivo == null || archivo.Length == 0)
            return BadRequest(new { error = "Archivo vacío o no adjunto." });

        var ext = Path.GetExtension(archivo.FileName).ToLowerInvariant();
        if (ext is not ".xlsx" and not ".xls")
            return BadRequest(new { error = "Solo se aceptan archivos .xlsx o .xls." });

        using var stream = archivo.OpenReadStream();
        var resultado = await _service.ImportarDesdeExcelAsync(stream);

        _logger.LogInformation(
            "Importación Excel terceros: {Importados} importados, {Omitidos} omitidos, {Errores} errores.",
            resultado.Importados, resultado.Omitidos, resultado.Errores);

        return Ok(resultado);
    }

    // ── Actividades CIIU ──────────────────────────────────────────────────────

    /// <summary>Agregar actividad económica CIIU al tercero</summary>
    [HttpPost("{id:int}/actividades")]
    [Authorize(Policy = "Supervisor")]
    public async Task<ActionResult<TerceroActividadDto>> AgregarActividad(int id, AgregarActividadDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.CodigoCIIU))
            return BadRequest(new { error = "El código CIIU es obligatorio." });
        if (string.IsNullOrWhiteSpace(dto.Descripcion))
            return BadRequest(new { error = "La descripción es obligatoria." });

        var (result, error) = await _service.AgregarActividadAsync(id, dto);
        if (error != null)
        {
            if (error.Contains("no encontrado"))
                return NotFound(new { error });
            return Conflict(new { error });
        }

        _logger.LogInformation("Actividad CIIU {CIIU} agregada al tercero {Id}.", dto.CodigoCIIU, id);
        return Ok(result);
    }

    /// <summary>Eliminar actividad CIIU del tercero</summary>
    [HttpDelete("{id:int}/actividades/{actividadId:int}")]
    [Authorize(Policy = "Supervisor")]
    public async Task<ActionResult> EliminarActividad(int id, int actividadId)
    {
        var (success, error) = await _service.EliminarActividadAsync(id, actividadId);
        if (!success)
            return NotFound(new { error });

        _logger.LogInformation("Actividad {ActividadId} eliminada del tercero {Id}.", actividadId, id);
        return NoContent();
    }

    /// <summary>Establecer actividad CIIU como principal</summary>
    [HttpPatch("{id:int}/actividades/{actividadId:int}/principal")]
    [Authorize(Policy = "Cajero")]
    public async Task<ActionResult> EstablecerPrincipal(int id, int actividadId)
    {
        var (success, error) = await _service.EstablecerPrincipalAsync(id, actividadId);
        if (!success)
            return NotFound(new { error });

        return NoContent();
    }
}
