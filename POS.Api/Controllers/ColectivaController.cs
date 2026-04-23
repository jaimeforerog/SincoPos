using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using POS.Application.Services;

namespace POS.Api.Controllers;

/// <summary>
/// Capa 13 — Inteligencia colectiva.
/// Combos de productos (cross-selling) + comparación cross-sucursal + estado del módulo global.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]")]
[Authorize]
public sealed class ColectivaController : ControllerBase
{
    private readonly IColectivaService _colectiva;

    public ColectivaController(IColectivaService colectiva) => _colectiva = colectiva;

    /// <summary>
    /// Combos de productos más vendidos juntos en una sucursal.
    /// Útil para cross-selling en el POS y planificación de bundles.
    /// </summary>
    [HttpGet("combos/{sucursalId:int}")]
    [Authorize(Roles = "supervisor,admin")]
    public async Task<IActionResult> GetCombos(int sucursalId, [FromQuery] int top = 15) =>
        Ok(await _colectiva.ObtenerCombosAsync(sucursalId, top));

    /// <summary>
    /// Comparación de velocidad de productos entre sucursales de una empresa.
    /// Permite detectar qué sucursal vende mejor cada SKU.
    /// </summary>
    [HttpGet("comparar/{empresaId:int}")]
    [Authorize(Roles = "supervisor,admin")]
    public async Task<IActionResult> CompararSucursales(int empresaId) =>
        Ok(await _colectiva.CompararSucursalesAsync(empresaId));

    /// <summary>
    /// Estado del servicio central Sinco (propagación global de patrones).
    /// Actualmente en modo local — el servicio central requiere suscripción Sinco Cloud.
    /// </summary>
    [HttpGet("estado-global")]
    [Authorize(Roles = "supervisor,admin")]
    public IActionResult GetEstadoGlobal() =>
        Ok(_colectiva.ObtenerEstadoGlobal());
}
