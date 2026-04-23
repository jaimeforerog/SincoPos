using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using POS.Api.Extensions;
using POS.Application.Services;
using POS.Domain.Aggregates;

namespace POS.Api.Controllers;

/// <summary>
/// Capa 9 — Aprendizaje continuo.
/// Expone los patrones aprendidos a nivel individual (cajero) y organizacional (tienda).
/// </summary>
[Authorize]
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]")]
public sealed class AprendizajeController : ControllerBase
{
    private readonly IAprendizajeService _aprendizajeService;

    public AprendizajeController(IAprendizajeService aprendizajeService)
    {
        _aprendizajeService = aprendizajeService;
    }

    /// <summary>
    /// Retorna el patrón de ventas del cajero autenticado.
    /// Incluye velocidad de productos, horas pico y días activos.
    /// Retorna 204 si aún no hay datos suficientes (primera sesión).
    /// </summary>
    [HttpGet("mi-patron")]
    [ProducesResponseType(typeof(CashierPattern), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<ActionResult<CashierPattern>> GetMiPatron()
    {
        var externalId = User.GetExternalId();
        if (string.IsNullOrEmpty(externalId))
            return NoContent();

        var pattern = await _aprendizajeService.ObtenerPatronCajero(externalId);
        return pattern is null ? NoContent() : Ok(pattern);
    }

    /// <summary>
    /// Retorna el patrón de ventas de una tienda.
    /// Incluye top productos, horas pico y velocidad agregada.
    /// Uso: Capa 14 — Radar de Negocio.
    /// </summary>
    [HttpGet("tienda/{sucursalId:int}")]
    [Authorize(Policy = "Supervisor")]
    [ProducesResponseType(typeof(StorePattern), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<ActionResult<StorePattern>> GetPatronTienda(int sucursalId)
    {
        var pattern = await _aprendizajeService.ObtenerPatronTienda(sucursalId);
        return pattern is null ? NoContent() : Ok(pattern);
    }
}
