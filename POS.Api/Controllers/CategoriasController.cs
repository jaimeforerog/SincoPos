using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using POS.Application.DTOs;
using POS.Infrastructure.Data;
using POS.Infrastructure.Data.Entities;

namespace POS.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class CategoriasController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly ILogger<CategoriasController> _logger;

    public CategoriasController(AppDbContext context, ILogger<CategoriasController> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Crear una nueva categoria
    /// </summary>
    [HttpPost]
    [Authorize(Policy = "Supervisor")]
    public async Task<ActionResult<CategoriaDto>> CrearCategoria(
        CrearCategoriaDto dto,
        [FromServices] IValidator<CrearCategoriaDto> validator)
    {
        var validationResult = await validator.ValidateAsync(dto);
        if (!validationResult.IsValid)
        {
            var errors = validationResult.Errors
                .GroupBy(e => e.PropertyName)
                .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray());
            return BadRequest(new { errors });
        }

        // Verificar nombre unico
        var existeNombre = await _context.Categorias
            .AnyAsync(c => c.Nombre == dto.Nombre);

        if (existeNombre)
            return Conflict(new { error = "Ya existe una categoria con ese nombre." });

        var categoria = new Categoria
        {
            Nombre = dto.Nombre,
            Descripcion = dto.Descripcion,
            Activo = true
        };

        _context.Categorias.Add(categoria);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Categoria creada. Id: {Id}, Nombre: {Nombre}", categoria.Id, categoria.Nombre);

        var result = new CategoriaDto(categoria.Id, categoria.Nombre, categoria.Descripcion, categoria.Activo);
        return CreatedAtAction(nameof(ObtenerCategoria), new { id = categoria.Id }, result);
    }

    /// <summary>
    /// Obtener una categoria por ID
    /// </summary>
    [HttpGet("{id:int}")]
    public async Task<ActionResult<CategoriaDto>> ObtenerCategoria(int id)
    {
        var categoria = await _context.Categorias
            .Where(c => c.Id == id)
            .Select(c => new CategoriaDto(c.Id, c.Nombre, c.Descripcion, c.Activo))
            .FirstOrDefaultAsync();

        if (categoria == null)
            return NotFound(new { error = $"Categoria {id} no encontrada." });

        return Ok(categoria);
    }

    /// <summary>
    /// Listar categorias
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<List<CategoriaDto>>> ObtenerCategorias(
        [FromQuery] bool incluirInactivas = false)
    {
        var query = _context.Categorias.AsQueryable();

        if (!incluirInactivas)
            query = query.Where(c => c.Activo);

        var categorias = await query
            .OrderBy(c => c.Nombre)
            .Select(c => new CategoriaDto(c.Id, c.Nombre, c.Descripcion, c.Activo))
            .ToListAsync();

        return Ok(categorias);
    }

    /// <summary>
    /// Actualizar una categoria
    /// </summary>
    [HttpPut("{id:int}")]
    [Authorize(Policy = "Supervisor")]
    public async Task<ActionResult> ActualizarCategoria(
        int id,
        ActualizarCategoriaDto dto,
        [FromServices] IValidator<ActualizarCategoriaDto> validator)
    {
        var validationResult = await validator.ValidateAsync(dto);
        if (!validationResult.IsValid)
        {
            var errors = validationResult.Errors
                .GroupBy(e => e.PropertyName)
                .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray());
            return BadRequest(new { errors });
        }

        var categoria = await _context.Categorias.FindAsync(id);
        if (categoria == null)
            return NotFound(new { error = $"Categoria {id} no encontrada." });

        // Verificar nombre unico (excluyendo la misma categoria)
        var existeNombre = await _context.Categorias
            .AnyAsync(c => c.Nombre == dto.Nombre && c.Id != id);

        if (existeNombre)
            return Conflict(new { error = "Ya existe otra categoria con ese nombre." });

        categoria.Nombre = dto.Nombre;
        categoria.Descripcion = dto.Descripcion;
        await _context.SaveChangesAsync();

        _logger.LogInformation("Categoria {Id} actualizada.", id);
        return NoContent();
    }

    /// <summary>
    /// Desactivar una categoria (soft delete)
    /// </summary>
    [HttpDelete("{id:int}")]
    [Authorize(Policy = "Admin")]
    public async Task<ActionResult> DesactivarCategoria(int id)
    {
        var categoria = await _context.Categorias.FindAsync(id);
        if (categoria == null)
            return NotFound(new { error = $"Categoria {id} no encontrada." });

        // Verificar que no tenga productos activos asociados
        var tieneProductos = await _context.Productos
            .AnyAsync(p => p.CategoriaId == id && p.Activo);

        if (tieneProductos)
            return Conflict(new { error = "No se puede desactivar una categoria con productos activos." });

        categoria.Activo = false;
        await _context.SaveChangesAsync();

        _logger.LogInformation("Categoria {Id} desactivada.", id);
        return NoContent();
    }
}
