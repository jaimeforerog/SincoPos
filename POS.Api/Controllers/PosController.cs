using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using POS.Application.Services;

namespace POS.Api.Controllers;

/// <summary>
/// Capa 3 — Repetición cero.
/// Provee el contexto precargado del turno: clientes recientes y órdenes pendientes.
/// </summary>
[Authorize]
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]")]
public sealed class PosController : ControllerBase
{
    private readonly IPosContextoService _posContexto;

    public PosController(IPosContextoService posContexto)
    {
        _posContexto = posContexto;
    }

    /// <summary>
    /// Contexto de turno para la sucursal: clientes recientes + órdenes pendientes.
    /// Llamar al seleccionar caja para precargar datos que el cajero necesita sin buscar.
    /// </summary>
    [HttpGet("contexto")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ObtenerContexto([FromQuery] int sucursalId)
    {
        if (sucursalId <= 0)
            return Problem(detail: "sucursalId es requerido.", statusCode: StatusCodes.Status400BadRequest);

        var ctx = await _posContexto.ObtenerContextoAsync(sucursalId);
        return Ok(ctx);
    }
}
