using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using POS.Application.Services;

namespace POS.Api.Controllers;

/// <summary>
/// Capa 10 — Explicabilidad.
/// Genera sugerencias automáticas con razón, fuente de datos y nivel de confianza.
/// Cada sugerencia incluye "por qué" es relevante y "en qué datos se basa".
/// </summary>
[Authorize(Policy = "Supervisor")]
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]")]
public class SugerenciasController : ControllerBase
{
    private readonly ISugerenciasService _sugerencias;

    public SugerenciasController(ISugerenciasService sugerencias)
    {
        _sugerencias = sugerencias;
    }

    /// <summary>
    /// Sugerencias de reabastecimiento para la sucursal.
    /// Usa velocidad histórica de productos (Capa 9) y stock actual para predecir
    /// qué productos se agotarán en los próximos 14 días.
    /// Retorna lista vacía si no hay suficientes datos (&lt;5 ventas).
    /// </summary>
    [HttpGet("reabastecimiento")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ObtenerReabastecimiento([FromQuery] int sucursalId)
    {
        if (sucursalId <= 0)
            return Problem(detail: "sucursalId es requerido.", statusCode: StatusCodes.Status400BadRequest);

        var sugerencias = await _sugerencias.ObtenerSugerenciasReabastecimientoAsync(sucursalId);
        return Ok(sugerencias);
    }
}
