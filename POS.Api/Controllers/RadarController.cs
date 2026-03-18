using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using POS.Application.Services;

namespace POS.Api.Controllers;

/// <summary>
/// Capa 14 — Radar de Negocio.
/// Métricas en tiempo real + proyección intradiaria + riesgos de ruptura por sucursal.
/// Accesible solo para roles Supervisor y Admin.
/// </summary>
[Authorize(Policy = "Supervisor")]
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]")]
public class RadarController : ControllerBase
{
    private readonly IRadarNegocioService _radarService;

    public RadarController(IRadarNegocioService radarService)
    {
        _radarService = radarService;
    }

    /// <summary>
    /// Radar de negocio de la sucursal: métricas del día, ventas por hora y riesgos de stock.
    /// Mismo shape que BusinessRadarProps del frontend para integración directa.
    /// </summary>
    [HttpGet("sucursal/{sucursalId:int}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ObtenerRadar(int sucursalId)
    {
        var radar = await _radarService.ObtenerRadarAsync(sucursalId);
        if (radar is null) return NoContent();
        return Ok(radar);
    }

    /// <summary>
    /// Documento Marten de la sucursal: velocidad histórica de productos e ingresos por
    /// fecha/hora acumulados desde el primer evento. Útil para forecasting y tendencias.
    /// </summary>
    [HttpGet("sucursal/{sucursalId:int}/patron")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ObtenerPatron(int sucursalId)
    {
        var patron = await _radarService.ObtenerPatronAsync(sucursalId);
        if (patron is null) return NoContent();
        return Ok(patron);
    }
}
