using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using POS.Infrastructure.Data;
using POS.Infrastructure.Data.Entities;

namespace POS.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class ImpuestosController : ControllerBase
{
    private readonly AppDbContext _context;

    public ImpuestosController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<object>>> GetImpuestos()
    {
        var impuestos = await _context.Impuestos
            .Where(i => i.Activo)
            .Select(i => new { i.Id, i.Nombre, i.Porcentaje })
            .ToListAsync();

        return Ok(impuestos);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<object>> GetImpuesto(int id)
    {
        var impuesto = await _context.Impuestos.FindAsync(id);
        if (impuesto == null || !impuesto.Activo) return NotFound("Impuesto no encontrado.");

        return Ok(new { impuesto.Id, impuesto.Nombre, impuesto.Porcentaje });
    }

    [HttpPost]
    [Authorize(Policy = "Admin")]
    public async Task<ActionResult> CrearImpuesto([FromBody] CrearImpuestoDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Nombre)) return BadRequest("Nombre requerido");
        if (dto.Porcentaje < 0 || dto.Porcentaje >= 1) return BadRequest("Porcentaje debe estar entre 0 y 0.9999");

        var impuesto = new Impuesto
        {
            Nombre = dto.Nombre,
            Porcentaje = dto.Porcentaje,
            Activo = true,
            FechaCreacion = DateTime.UtcNow
        };

        _context.Impuestos.Add(impuesto);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetImpuesto), new { id = impuesto.Id }, 
            new { impuesto.Id, impuesto.Nombre, impuesto.Porcentaje });
    }

    [HttpDelete("{id:int}")]
    [Authorize(Policy = "Admin")]
    public async Task<ActionResult> DesactivarImpuesto(int id)
    {
        var impuesto = await _context.Impuestos.FindAsync(id);
        if (impuesto == null || !impuesto.Activo) return NotFound("Impuesto no encontrado.");

        var enUso = await _context.Productos.AnyAsync(p => p.ImpuestoId == id && p.Activo);
        if (enUso) return BadRequest("El impuesto está asignado a productos activos. Desasígnelo primero.");

        impuesto.Activo = false;
        await _context.SaveChangesAsync();
        return NoContent();
    }
}

public record CrearImpuestoDto(string Nombre, decimal Porcentaje);
