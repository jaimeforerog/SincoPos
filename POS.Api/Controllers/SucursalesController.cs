using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.EntityFrameworkCore;
using POS.Application.DTOs;
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
            Activo = true,
            FechaCreacion = DateTime.UtcNow
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
                s.Telefono, s.Email, s.CentroCosto, s.MetodoCosteo.ToString(), s.Activo, s.FechaCreacion))
            .FirstOrDefaultAsync();

        if (sucursal == null)
            return Problem(detail: $"Sucursal {id} no encontrada.", statusCode: StatusCodes.Status404NotFound);

        return Ok(sucursal);
    }

    /// <summary>
    /// Listar sucursales
    /// </summary>
    [HttpGet]
    [OutputCache(PolicyName = "Catalogo5m", VaryByQueryKeys = ["incluirInactivas"])]
    public async Task<ActionResult<List<SucursalDto>>> ObtenerSucursales(
        [FromQuery] bool incluirInactivas = false)
    {
        var query = _context.Sucursales.AsQueryable();

        if (incluirInactivas)
            query = query.IgnoreQueryFilters();

        var sucursales = await query
            .OrderBy(s => s.Nombre)
            .Select(s => new SucursalDto(
                s.Id, s.Nombre, s.Direccion, s.CodigoPais, s.NombrePais, s.Ciudad,
                s.Telefono, s.Email, s.CentroCosto, s.MetodoCosteo.ToString(), s.Activo, s.FechaCreacion))
            .ToListAsync();

        return Ok(sucursales);
    }

    /// <summary>
    /// Endpoint de prueba con SQL directo
    /// </summary>
    [HttpGet("test-raw")]
    public async Task<ActionResult> TestRawSql()
    {
        var connection = _context.Database.GetDbConnection();
        await connection.OpenAsync();

        // Verificar base de datos actual
        using var dbCommand = connection.CreateCommand();
        dbCommand.CommandText = "SELECT current_database(), current_schema(), version()";
        var dbInfo = new {
            Database = "",
            Schema = "",
            Version = ""
        };

        using (var dbReader = await dbCommand.ExecuteReaderAsync())
        {
            if (await dbReader.ReadAsync())
            {
                dbInfo = new {
                    Database = dbReader.GetString(0),
                    Schema = dbReader.GetString(1),
                    Version = dbReader.GetString(2)
                };
            }
        }

        // Query sucursales
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT \"Id\", nombre, activo FROM public.sucursales ORDER BY \"Id\"";

        var results = new List<object>();
        using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            results.Add(new {
                Id = reader.GetInt32(0),
                Nombre = reader.GetString(1),
                Activo = reader.GetBoolean(2)
            });
        }

        return Ok(new {
            ConnectionInfo = dbInfo,
            ConnectionString = _context.Database.GetConnectionString(),
            Sucursales = results
        });
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
}
