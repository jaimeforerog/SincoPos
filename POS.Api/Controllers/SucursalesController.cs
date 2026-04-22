using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.EntityFrameworkCore;
using POS.Application.DTOs;
using POS.Application.Services;
using POS.Infrastructure.Data;
using POS.Infrastructure.Data.Entities;

namespace POS.Api.Controllers;

[Authorize]
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]")]
public class SucursalesController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly ILogger<SucursalesController> _logger;
    private readonly POS.Application.Services.ICurrentEmpresaProvider _empresaProvider;
    private readonly IActivityLogService _activityLogService;

    public SucursalesController(AppDbContext context, ILogger<SucursalesController> logger, POS.Application.Services.ICurrentEmpresaProvider empresaProvider, IActivityLogService activityLogService)
    {
        _context = context;
        _logger = logger;
        _empresaProvider = empresaProvider;
        _activityLogService = activityLogService;
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
            foreach (var (key, messages) in errors)
                foreach (var msg in messages)
                    ModelState.AddModelError(key, msg);
            return ValidationProblem();
        }

        var existeNombre = await _context.Sucursales
            .AnyAsync(s => s.Nombre == dto.Nombre);

        if (existeNombre)
            return Problem(detail: "Ya existe una sucursal con ese nombre.", statusCode: StatusCodes.Status409Conflict);

        var metodo = MetodoCosteo.PromedioPonderado;
        if (!string.IsNullOrEmpty(dto.MetodoCosteo) && !Enum.TryParse<MetodoCosteo>(dto.MetodoCosteo, true, out metodo))
            return Problem(detail: $"Metodo de costeo invalido. Valores: {string.Join(", ", Enum.GetNames<MetodoCosteo>())}", statusCode: StatusCodes.Status400BadRequest);

        var sucursal = new Sucursal
        {
            Nombre = dto.Nombre,
            Direccion = dto.Direccion,
            CodigoPais = dto.CodigoPais ?? "CO",
            NombrePais = dto.NombrePais ?? "Colombia",
            Ciudad = dto.Ciudad,
            Telefono = dto.Telefono,
            Email = dto.Email,
            CentroCosto = dto.CentroCosto,
            MetodoCosteo = metodo,
            EmpresaId = _empresaProvider.EmpresaId ?? throw new InvalidOperationException("EmpresaId requerido."),
        };

        _context.Sucursales.Add(sucursal);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Sucursal creada. Id: {Id}, Nombre: {Nombre}", sucursal.Id, sucursal.Nombre);

        var result = new SucursalDto(
            sucursal.Id, sucursal.Nombre, sucursal.Direccion,
            sucursal.CodigoPais, sucursal.NombrePais,
            sucursal.Ciudad, sucursal.Telefono, sucursal.Email,
            sucursal.CentroCosto,
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
            .IgnoreQueryFilters() // Permitir ver por ID incluso si está inactiva
            .Where(s => s.Id == id)
            .Select(s => new SucursalDto(
                s.Id, s.Nombre, s.Direccion, s.CodigoPais, s.NombrePais, s.Ciudad,
                s.Telefono, s.Email, s.CentroCosto, s.MetodoCosteo.ToString(), s.Activo, s.FechaCreacion, s.EmpresaId))
            .FirstOrDefaultAsync();

        if (sucursal == null)
            return Problem(detail: $"Sucursal {id} no encontrada.", statusCode: StatusCodes.Status404NotFound);

        return Ok(sucursal);
    }

    /// <summary>
    /// Listar sucursales con paginación opcional
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<PaginatedResult<SucursalDto>>> ObtenerSucursales(
        [FromQuery] bool incluirInactivas = false,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 100)
    {
        // IgnoreQueryFilters omite TODOS los filtros (incluido el de empresa).
        // Para incluir inactivas, aplicamos el filtro de empresa explícitamente.
        var query = incluirInactivas
            ? _context.Sucursales
                .IgnoreQueryFilters()
                .Where(s => _empresaProvider.EmpresaId == null || s.EmpresaId == _empresaProvider.EmpresaId)
                .AsQueryable()
            : _context.Sucursales.AsQueryable();

        query = query.OrderBy(s => s.Nombre);

        var totalCount = await query.CountAsync();
        var sucursales = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(s => new SucursalDto(
                s.Id, s.Nombre, s.Direccion, s.CodigoPais, s.NombrePais, s.Ciudad,
                s.Telefono, s.Email, s.CentroCosto, s.MetodoCosteo.ToString(), s.Activo, s.FechaCreacion, s.EmpresaId))
            .ToListAsync();

        var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);
        return Ok(new PaginatedResult<SucursalDto>(sucursales, totalCount, page, pageSize, totalPages));
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
            foreach (var (key, messages) in errors)
                foreach (var msg in messages)
                    ModelState.AddModelError(key, msg);
            return ValidationProblem();
        }

        var sucursal = await _context.Sucursales.FindAsync(id);
        if (sucursal == null)
            return Problem(detail: $"Sucursal {id} no encontrada.", statusCode: StatusCodes.Status404NotFound);

        var existeNombre = await _context.Sucursales
            .AnyAsync(s => s.Nombre == dto.Nombre && s.Id != id);

        if (existeNombre)
            return Problem(detail: "Ya existe otra sucursal con ese nombre.", statusCode: StatusCodes.Status409Conflict);

        sucursal.Nombre = dto.Nombre;
        sucursal.Direccion = dto.Direccion;
        sucursal.CodigoPais = dto.CodigoPais;
        sucursal.NombrePais = dto.NombrePais;
        sucursal.Ciudad = dto.Ciudad;
        sucursal.Telefono = dto.Telefono;
        sucursal.Email = dto.Email;
        sucursal.CentroCosto = dto.CentroCosto;

        if (!string.IsNullOrEmpty(dto.MetodoCosteo))
        {
            if (!Enum.TryParse<MetodoCosteo>(dto.MetodoCosteo, true, out var metodoUpd))
                return Problem(detail: $"Metodo de costeo invalido. Valores: {string.Join(", ", Enum.GetNames<MetodoCosteo>())}", statusCode: StatusCodes.Status400BadRequest);
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
            return Problem(detail: $"Sucursal {id} no encontrada.", statusCode: StatusCodes.Status404NotFound);

        sucursal.Activo = false;
        await _context.SaveChangesAsync();

        _logger.LogInformation("Sucursal {Id} desactivada.", id);
        return NoContent();
    }

    /// <summary>Reactivar una sucursal previamente desactivada.</summary>
    [HttpPatch("{id:int}/activar")]
    [Authorize(Policy = "Admin")]
    public async Task<ActionResult> ActivarSucursal(int id)
    {
        var sucursal = await _context.Sucursales.IgnoreQueryFilters().FirstOrDefaultAsync(s => s.Id == id);
        if (sucursal == null)
            return Problem(detail: $"Sucursal {id} no encontrada.", statusCode: StatusCodes.Status404NotFound);

        sucursal.Activo = true;
        await _context.SaveChangesAsync();

        _logger.LogInformation("Sucursal {Id} reactivada.", id);
        return NoContent();
    }
}
