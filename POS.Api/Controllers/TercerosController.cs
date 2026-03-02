using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using POS.Application.DTOs;
using POS.Application.Services;

namespace POS.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class TercerosController : ControllerBase
{
    private readonly ITerceroService _service;
    private readonly ILogger<TercerosController> _logger;

    public TercerosController(ITerceroService service, ILogger<TercerosController> logger)
    {
        _service = service;
        _logger = logger;
    }

    /// <summary>
    /// Crear un nuevo tercero (solo modo Local)
    /// </summary>
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

    /// <summary>
    /// Obtener un tercero por ID
    /// </summary>
    [HttpGet("{id:int}")]
    public async Task<ActionResult<TerceroDto>> ObtenerTercero(int id)
    {
        var tercero = await _service.ObtenerPorIdAsync(id);
        if (tercero == null)
            return NotFound(new { error = $"Tercero {id} no encontrado." });

        return Ok(tercero);
    }

    /// <summary>
    /// Buscar terceros por nombre o identificacion
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<List<TerceroDto>>> BuscarTerceros(
        [FromQuery] string? q = null,
        [FromQuery] string? tipoTercero = null,
        [FromQuery] bool incluirInactivos = false)
    {
        var terceros = await _service.BuscarAsync(q, tipoTercero, incluirInactivos);
        return Ok(terceros);
    }

    /// <summary>
    /// Buscar un tercero por numero de identificacion
    /// </summary>
    [HttpGet("identificacion/{identificacion}")]
    public async Task<ActionResult<TerceroDto>> BuscarPorIdentificacion(string identificacion)
    {
        var tercero = await _service.ObtenerPorIdentificacionAsync(identificacion);
        if (tercero == null)
            return NotFound(new { error = $"No se encontro un tercero con identificacion {identificacion}." });

        return Ok(tercero);
    }

    /// <summary>
    /// Actualizar un tercero
    /// </summary>
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

    /// <summary>
    /// Desactivar un tercero (soft delete)
    /// </summary>
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
}
