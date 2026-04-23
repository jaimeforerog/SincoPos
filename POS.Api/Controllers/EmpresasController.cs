using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using POS.Application.DTOs;
using POS.Application.Services;

namespace POS.Api.Controllers;

[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]")]
[ApiController]
[Authorize(Policy = "Admin")]
public sealed class EmpresasController : ControllerBase
{
    private readonly IEmpresaService _service;

    public EmpresasController(IEmpresaService service) => _service = service;

    /// <summary>Lista todas las empresas registradas.</summary>
    [HttpGet]
    public async Task<ActionResult<List<EmpresaDto>>> GetAll()
    {
        var result = await _service.ObtenerTodasAsync();
        return Ok(result);
    }

    /// <summary>Obtiene una empresa por ID.</summary>
    [HttpGet("{id:int}")]
    public async Task<ActionResult<EmpresaDto>> GetById(int id)
    {
        var empresa = await _service.ObtenerPorIdAsync(id);
        return empresa is null ? NotFound() : Ok(empresa);
    }

    /// <summary>Crea una nueva empresa.</summary>
    [HttpPost]
    public async Task<ActionResult<EmpresaDto>> Create([FromBody] CrearEmpresaDto dto)
    {
        var (result, error) = await _service.CrearAsync(dto);
        if (error is not null) return BadRequest(new { detail = error });
        return CreatedAtAction(nameof(GetById), new { id = result!.Id }, result);
    }

    /// <summary>Actualiza una empresa existente.</summary>
    [HttpPut("{id:int}")]
    public async Task<ActionResult<EmpresaDto>> Update(int id, [FromBody] ActualizarEmpresaDto dto)
    {
        var (result, error) = await _service.ActualizarAsync(id, dto);
        if (error == "NOT_FOUND") return NotFound();
        if (error is not null) return BadRequest(new { detail = error });
        return Ok(result);
    }
}
