using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using POS.Api.Extensions;
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
    private readonly IClienteHistorialService _historialService;
    private readonly ILogger<TercerosController> _logger;
    private readonly IActivityLogService _activityLogService;

    public TercerosController(
        ITerceroService service,
        IClienteHistorialService historialService,
        ILogger<TercerosController> logger,
        IActivityLogService activityLogService)
    {
        _service = service;
        _historialService = historialService;
        _logger = logger;
        _activityLogService = activityLogService;
    }

    /// <summary>Calcular el Dígito de Verificación (DV) de un NIT (módulo 11 DIAN)</summary>
    [HttpGet("calcular-dv")]
    [AllowAnonymous]
    public ActionResult CalcularDV([FromQuery] string nit)
    {
        if (string.IsNullOrWhiteSpace(nit) || !nit.Any(char.IsDigit))
            return Problem(detail: "NIT inválido.", statusCode: StatusCodes.Status400BadRequest);

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
            foreach (var (key, messages) in errors)
                foreach (var msg in messages)
                    ModelState.AddModelError(key, msg);
            return ValidationProblem();
        }

        var (result, error) = await _service.CrearAsync(dto);
        if (error != null)
            return Problem(detail: error, statusCode: StatusCodes.Status409Conflict);

        _logger.LogInformation("Tercero creado. Id: {Id}, Nombre: {Nombre}", result!.Id, result.Nombre);
        await _activityLogService.LogActivityAsync(new ActivityLogDto(
            Accion: "CrearTercero",
            Tipo: TipoActividad.Configuracion,
            Descripcion: $"Tercero '{result.Nombre}' ({result.TipoTercero}) creado por {User.GetEmail()}",
            TipoEntidad: "Tercero",
            EntidadId: result.Id.ToString(),
            EntidadNombre: result.Nombre,
            DatosNuevos: new { result.Identificacion, result.Nombre, result.TipoTercero, CreadoPor = User.GetEmail() }
        ));
        return CreatedAtAction(nameof(ObtenerTercero), new { id = result.Id }, result);
    }

    /// <summary>Obtener un tercero por ID</summary>
    [HttpGet("{id:int}")]
    public async Task<ActionResult<TerceroDto>> ObtenerTercero(int id)
    {
        var tercero = await _service.ObtenerPorIdAsync(id);
        if (tercero == null)
            return Problem(detail: $"Tercero {id} no encontrado.", statusCode: StatusCodes.Status404NotFound);

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
            return Problem(detail: $"No se encontro un tercero con identificacion {identificacion}.", statusCode: StatusCodes.Status404NotFound);

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
            foreach (var (key, messages) in errors)
                foreach (var msg in messages)
                    ModelState.AddModelError(key, msg);
            return ValidationProblem();
        }

        var (success, error) = await _service.ActualizarAsync(id, dto);
        if (!success)
        {
            if (error!.Contains("no encontrado"))
                return Problem(detail: error, statusCode: StatusCodes.Status404NotFound);
            return Problem(detail: error, statusCode: StatusCodes.Status400BadRequest);
        }

        _logger.LogInformation("Tercero {Id} actualizado.", id);
        await _activityLogService.LogActivityAsync(new ActivityLogDto(
            Accion: "ActualizarTercero",
            Tipo: TipoActividad.Configuracion,
            Descripcion: $"Tercero {id} actualizado por {User.GetEmail()}",
            TipoEntidad: "Tercero",
            EntidadId: id.ToString(),
            EntidadNombre: dto.Nombre,
            DatosNuevos: new { dto.Nombre, ActualizadoPor = User.GetEmail() }
        ));
        return NoContent();
    }

    /// <summary>Desactivar un tercero (soft delete)</summary>
    [HttpDelete("{id:int}")]
    [Authorize(Policy = "Supervisor")]
    public async Task<ActionResult> DesactivarTercero(int id)
    {
        var (success, error) = await _service.DesactivarAsync(id);
        if (!success)
            return Problem(detail: error, statusCode: StatusCodes.Status404NotFound);

        _logger.LogInformation("Tercero {Id} desactivado.", id);
        await _activityLogService.LogActivityAsync(new ActivityLogDto(
            Accion: "DesactivarTercero",
            Tipo: TipoActividad.Configuracion,
            Descripcion: $"Tercero {id} desactivado por {User.GetEmail()}",
            TipoEntidad: "Tercero",
            EntidadId: id.ToString(),
            DatosNuevos: new { DesactivadoPor = User.GetEmail() }
        ));
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
            return Problem(detail: "Archivo vacío o no adjunto.", statusCode: StatusCodes.Status400BadRequest);

        var ext = Path.GetExtension(archivo.FileName).ToLowerInvariant();
        if (ext is not ".xlsx" and not ".xls")
            return Problem(detail: "Solo se aceptan archivos .xlsx o .xls.", statusCode: StatusCodes.Status400BadRequest);

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
            return Problem(detail: "El código CIIU es obligatorio.", statusCode: StatusCodes.Status400BadRequest);
        if (string.IsNullOrWhiteSpace(dto.Descripcion))
            return Problem(detail: "La descripción es obligatoria.", statusCode: StatusCodes.Status400BadRequest);

        var (result, error) = await _service.AgregarActividadAsync(id, dto);
        if (error != null)
        {
            if (error.Contains("no encontrado"))
                return Problem(detail: error, statusCode: StatusCodes.Status404NotFound);
            return Problem(detail: error, statusCode: StatusCodes.Status409Conflict);
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
            return Problem(detail: error, statusCode: StatusCodes.Status404NotFound);

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
            return Problem(detail: error, statusCode: StatusCodes.Status404NotFound);

        return NoContent();
    }

    // ─── Capa 4 — Dependencias inteligentes ────────────────────────────────

    /// <summary>
    /// Capa 4 — Retorna el historial de compras acumulado de un cliente.
    /// Alimentado automáticamente por ClienteHistorialProjection en cada venta.
    /// </summary>
    [HttpGet("{id:int}/historial")]
    [Authorize(Policy = "Cajero")]
    [ProducesResponseType(typeof(ClienteHistorialDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<ActionResult<ClienteHistorialDto>> ObtenerHistorial(int id)
    {
        var historial = await _historialService.ObtenerHistorialAsync(id);
        if (historial is null) return NoContent();
        return Ok(historial);
    }
}
