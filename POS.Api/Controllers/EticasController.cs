using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using POS.Application.Services;

namespace POS.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]")]
[Authorize]
public class EticasController : ControllerBase
{
    private readonly IEthicalGuardService _guard;

    public EticasController(IEthicalGuardService guard) => _guard = guard;

    // ── Reglas CRUD ───────────────────────────────────────────────────────

    [HttpGet]
    [Authorize(Roles = "supervisor,admin")]
    public async Task<IActionResult> GetAll() =>
        Ok(await _guard.ObtenerReglasAsync());

    [HttpGet("{id:int}")]
    [Authorize(Roles = "supervisor,admin")]
    public async Task<IActionResult> GetById(int id)
    {
        var dto = await _guard.ObtenerPorIdAsync(id);
        return dto == null ? NotFound() : Ok(dto);
    }

    [HttpPost]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> Create([FromBody] CrearReglaEticaDto dto)
    {
        var created = await _guard.CrearAsync(dto);
        return CreatedAtAction(nameof(GetById), new { id = created.Id, version = "1" }, created);
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> Update(int id, [FromBody] CrearReglaEticaDto dto)
    {
        var (result, error) = await _guard.ActualizarAsync(id, dto);
        if (error == "NOT_FOUND") return NotFound();
        return Ok(result);
    }

    [HttpDelete("{id:int}")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> Delete(int id)
    {
        var ok = await _guard.EliminarAsync(id);
        return ok ? NoContent() : NotFound();
    }

    // ── Historial de activaciones ─────────────────────────────────────────

    [HttpGet("activaciones")]
    [Authorize(Roles = "supervisor,admin")]
    public async Task<IActionResult> GetActivaciones([FromQuery] int? reglaId, [FromQuery] int take = 50) =>
        Ok(await _guard.ObtenerActivacionesAsync(reglaId, take));
}
