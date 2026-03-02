using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using POS.Infrastructure.Data.Entities;
using POS.Infrastructure.Services;

namespace POS.Api.Controllers;

/// <summary>
/// Controlador para consultar el historial de migraciones de base de datos
/// </summary>
[Authorize(Policy = "Admin")]
[ApiController]
[Route("api/[controller]")]
public class MigracionesController : ControllerBase
{
    private readonly MigracionLogService _migracionLogService;
    private readonly ILogger<MigracionesController> _logger;

    public MigracionesController(
        MigracionLogService migracionLogService,
        ILogger<MigracionesController> logger)
    {
        _migracionLogService = migracionLogService;
        _logger = logger;
    }

    /// <summary>
    /// Obtiene el historial de migraciones aplicadas
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<List<MigracionLogDto>>> ObtenerHistorial(
        [FromQuery] int limite = 50)
    {
        try
        {
            var migraciones = await _migracionLogService.ObtenerHistorial(limite);

            var dtos = migraciones.Select(m => new MigracionLogDto
            {
                Id = m.Id,
                MigracionId = m.MigracionId,
                Descripcion = m.Descripcion,
                ProductVersion = m.ProductVersion,
                FechaAplicacion = m.FechaAplicacion,
                AplicadoPor = m.AplicadoPor,
                Estado = m.Estado,
                DuracionMs = m.DuracionMs,
                Notas = m.Notas
            }).ToList();

            return Ok(dtos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al obtener historial de migraciones");
            return StatusCode(500, "Error al obtener el historial de migraciones");
        }
    }

    /// <summary>
    /// Sincroniza el log con las migraciones existentes en __ef_migrations_history.
    /// Útil para registrar migraciones que se aplicaron antes de implementar este sistema.
    /// </summary>
    [HttpPost("sincronizar")]
    public async Task<ActionResult> SincronizarMigraciones()
    {
        try
        {
            await _migracionLogService.SincronizarMigracionesExistentes();
            return Ok(new { mensaje = "Migraciones sincronizadas exitosamente" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al sincronizar migraciones");
            return StatusCode(500, "Error al sincronizar migraciones");
        }
    }

    /// <summary>
    /// Registra manualmente una migración en el log
    /// </summary>
    [HttpPost("registrar")]
    public async Task<ActionResult> RegistrarMigracion(
        [FromBody] RegistrarMigracionDto dto)
    {
        try
        {
            var usuario = User.FindFirst("email")?.Value
                ?? User.FindFirst("preferred_username")?.Value
                ?? "admin";

            await _migracionLogService.RegistrarMigracion(
                dto.MigracionId,
                dto.Descripcion,
                dto.ProductVersion,
                usuario,
                dto.DuracionMs,
                dto.Notas
            );

            return Ok(new { mensaje = "Migración registrada exitosamente" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al registrar migración");
            return StatusCode(500, "Error al registrar la migración");
        }
    }
}

/// <summary>
/// DTO para retornar información de migraciones
/// </summary>
public record MigracionLogDto
{
    public int Id { get; set; }
    public string MigracionId { get; set; } = string.Empty;
    public string Descripcion { get; set; } = string.Empty;
    public string ProductVersion { get; set; } = string.Empty;
    public DateTime FechaAplicacion { get; set; }
    public string AplicadoPor { get; set; } = string.Empty;
    public string Estado { get; set; } = string.Empty;
    public long DuracionMs { get; set; }
    public string? Notas { get; set; }
}

/// <summary>
/// DTO para registrar una migración manualmente
/// </summary>
public record RegistrarMigracionDto
{
    public string MigracionId { get; set; } = string.Empty;
    public string Descripcion { get; set; } = string.Empty;
    public string ProductVersion { get; set; } = string.Empty;
    public long DuracionMs { get; set; }
    public string? Notas { get; set; }
}
