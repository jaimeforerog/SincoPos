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
public class SucursalesController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly ILogger<SucursalesController> _logger;

    public SucursalesController(AppDbContext context, ILogger<SucursalesController> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Crear una nueva sucursal
    /// </summary>
    [HttpPost]
    [Authorize(Policy = "Admin")]
    public async Task<ActionResult<SucursalDto>> CrearSucursal(
        CrearSucursalDto dto,
        [FromServices] IValidator<CrearSucursalDto> validator)
    {
        var validationResult = await validator.ValidateAsync(dto);
        if (!validationResult.IsValid)
        {
            var errors = validationResult.Errors
                .GroupBy(e => e.PropertyName)
                .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray());
            return BadRequest(new { errors });
        }

        var existeNombre = await _context.Sucursales
            .AnyAsync(s => s.Nombre == dto.Nombre);

        if (existeNombre)
            return Conflict(new { error = "Ya existe una sucursal con ese nombre." });

        var metodo = MetodoCosteo.PromedioPonderado;
        if (!string.IsNullOrEmpty(dto.MetodoCosteo) && !Enum.TryParse<MetodoCosteo>(dto.MetodoCosteo, true, out metodo))
            return BadRequest(new { error = $"Metodo de costeo invalido. Valores: {string.Join(", ", Enum.GetNames<MetodoCosteo>())}" });

        var sucursal = new Sucursal
        {
            Nombre = dto.Nombre,
            Direccion = dto.Direccion,
            Ciudad = dto.Ciudad,
            Telefono = dto.Telefono,
            Email = dto.Email,
            MetodoCosteo = metodo,
            Activo = true,
            FechaCreacion = DateTime.UtcNow
        };

        _context.Sucursales.Add(sucursal);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Sucursal creada. Id: {Id}, Nombre: {Nombre}", sucursal.Id, sucursal.Nombre);

        var result = new SucursalDto(
            sucursal.Id, sucursal.Nombre, sucursal.Direccion,
            sucursal.Ciudad, sucursal.Telefono, sucursal.Email,
            sucursal.MetodoCosteo.ToString(), sucursal.Activo, sucursal.FechaCreacion);

        return CreatedAtAction(nameof(ObtenerSucursal), new { id = sucursal.Id }, result);
    }

    /// <summary>
    /// Obtener una sucursal por ID
    /// </summary>
    [HttpGet("{id:int}")]
    public async Task<ActionResult<SucursalDto>> ObtenerSucursal(int id)
    {
        var sucursal = await _context.Sucursales
            .Where(s => s.Id == id)
            .Select(s => new SucursalDto(
                s.Id, s.Nombre, s.Direccion, s.Ciudad,
                s.Telefono, s.Email, s.MetodoCosteo.ToString(), s.Activo, s.FechaCreacion))
            .FirstOrDefaultAsync();

        if (sucursal == null)
            return NotFound(new { error = $"Sucursal {id} no encontrada." });

        return Ok(sucursal);
    }

    /// <summary>
    /// Listar sucursales
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<List<SucursalDto>>> ObtenerSucursales(
        [FromQuery] bool incluirInactivas = false)
    {
        var query = _context.Sucursales.AsQueryable();

        if (!incluirInactivas)
            query = query.Where(s => s.Activo);

        var sucursales = await query
            .OrderBy(s => s.Nombre)
            .Select(s => new SucursalDto(
                s.Id, s.Nombre, s.Direccion, s.Ciudad,
                s.Telefono, s.Email, s.MetodoCosteo.ToString(), s.Activo, s.FechaCreacion))
            .ToListAsync();

        return Ok(sucursales);
    }

    /// <summary>
    /// Actualizar una sucursal
    /// </summary>
    [HttpPut("{id:int}")]
    [Authorize(Policy = "Admin")]
    public async Task<ActionResult> ActualizarSucursal(
        int id,
        ActualizarSucursalDto dto,
        [FromServices] IValidator<ActualizarSucursalDto> validator)
    {
        var validationResult = await validator.ValidateAsync(dto);
        if (!validationResult.IsValid)
        {
            var errors = validationResult.Errors
                .GroupBy(e => e.PropertyName)
                .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray());
            return BadRequest(new { errors });
        }

        var sucursal = await _context.Sucursales.FindAsync(id);
        if (sucursal == null)
            return NotFound(new { error = $"Sucursal {id} no encontrada." });

        var existeNombre = await _context.Sucursales
            .AnyAsync(s => s.Nombre == dto.Nombre && s.Id != id);

        if (existeNombre)
            return Conflict(new { error = "Ya existe otra sucursal con ese nombre." });

        sucursal.Nombre = dto.Nombre;
        sucursal.Direccion = dto.Direccion;
        sucursal.Ciudad = dto.Ciudad;
        sucursal.Telefono = dto.Telefono;
        sucursal.Email = dto.Email;

        if (!string.IsNullOrEmpty(dto.MetodoCosteo))
        {
            if (!Enum.TryParse<MetodoCosteo>(dto.MetodoCosteo, true, out var metodoUpd))
                return BadRequest(new { error = $"Metodo de costeo invalido. Valores: {string.Join(", ", Enum.GetNames<MetodoCosteo>())}" });
            sucursal.MetodoCosteo = metodoUpd;
        }

        await _context.SaveChangesAsync();

        _logger.LogInformation("Sucursal {Id} actualizada.", id);
        return NoContent();
    }

    /// <summary>
    /// Desactivar una sucursal (soft delete)
    /// </summary>
    [HttpDelete("{id:int}")]
    [Authorize(Policy = "Admin")]
    public async Task<ActionResult> DesactivarSucursal(int id)
    {
        var sucursal = await _context.Sucursales.FindAsync(id);
        if (sucursal == null)
            return NotFound(new { error = $"Sucursal {id} no encontrada." });

        sucursal.Activo = false;
        await _context.SaveChangesAsync();

        _logger.LogInformation("Sucursal {Id} desactivada.", id);
        return NoContent();
    }
}
