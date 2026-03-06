using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using POS.Application.DTOs;
using POS.Infrastructure.Services;

namespace POS.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class PaisesController : ControllerBase
{
    private readonly GeoService _geoService;
    private readonly ILogger<PaisesController> _logger;

    public PaisesController(GeoService geoService, ILogger<PaisesController> logger)
    {
        _geoService = geoService;
        _logger = logger;
    }

    /// <summary>
    /// Obtener lista de todos los países
    /// </summary>
    [HttpGet]
    [OutputCache(PolicyName = "Catalogo1h")]
    public async Task<ActionResult<List<PaisDto>>> ObtenerPaises()
    {
        var paises = await _geoService.ObtenerPaises();
        return Ok(paises);
    }

    /// <summary>
    /// Obtener ciudades de un país específico
    /// </summary>
    [HttpGet("{codigoPais}/ciudades")]
    [OutputCache(PolicyName = "Catalogo1h")]
    public async Task<ActionResult<List<CiudadDto>>> ObtenerCiudades(string codigoPais)
    {
        if (string.IsNullOrWhiteSpace(codigoPais))
            return BadRequest(new { error = "El código de país es requerido" });

        var ciudades = await _geoService.ObtenerCiudadesPorPais(codigoPais);
        return Ok(ciudades);
    }
}
