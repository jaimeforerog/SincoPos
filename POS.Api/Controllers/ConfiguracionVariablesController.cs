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
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/configuracion-variables")]
public class ConfiguracionVariablesController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly ILogger<ConfiguracionVariablesController> _logger;
    private readonly POS.Application.Services.ICurrentEmpresaProvider _empresaProvider;

    public ConfiguracionVariablesController(
        AppDbContext context,
        ILogger<ConfiguracionVariablesController> logger,
        POS.Application.Services.ICurrentEmpresaProvider empresaProvider)
    {
        _context = context;
        _logger = logger;
        _empresaProvider = empresaProvider;
    }

    /// <summary>
    /// Listar variables de configuración
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<List<ConfiguracionVariableDto>>> ObtenerVariables(
        [FromQuery] bool incluirInactivas = false)
    {
        var query = incluirInactivas
            ? _context.ConfiguracionesVariables
                .IgnoreQueryFilters()
                .Where(c => c.EmpresaId == _empresaProvider.EmpresaId)
                .AsQueryable()
            : _context.ConfiguracionesVariables.AsQueryable();

        var variables = await query
            .OrderBy(c => c.Nombre)
            .Select(c => new ConfiguracionVariableDto(
                c.Id, c.Nombre, c.Valor, c.Descripcion, c.Activo, c.FechaCreacion, c.EmpresaId))
            .ToListAsync();

        return Ok(variables);
    }

    /// <summary>
    /// Obtener una variable por ID
    /// </summary>
    [HttpGet("{id:int}")]
    public async Task<ActionResult<ConfiguracionVariableDto>> ObtenerVariable(int id)
    {
        var variable = await _context.ConfiguracionesVariables
            .IgnoreQueryFilters()
            .Where(c => c.Id == id)
            .Select(c => new ConfiguracionVariableDto(
                c.Id, c.Nombre, c.Valor, c.Descripcion, c.Activo, c.FechaCreacion, c.EmpresaId))
            .FirstOrDefaultAsync();

        if (variable == null)
            return Problem(detail: $"Variable {id} no encontrada.", statusCode: StatusCodes.Status404NotFound);

        return Ok(variable);
    }

    /// <summary>
    /// Obtener una variable por nombre (útil para consumo interno)
    /// </summary>
    [HttpGet("nombre/{nombre}")]
    public async Task<ActionResult<ConfiguracionVariableDto>> ObtenerVariablePorNombre(string nombre)
    {
        var variable = await _context.ConfiguracionesVariables
            .Where(c => c.Nombre == nombre)
            .Select(c => new ConfiguracionVariableDto(
                c.Id, c.Nombre, c.Valor, c.Descripcion, c.Activo, c.FechaCreacion, c.EmpresaId))
            .FirstOrDefaultAsync();

        if (variable == null)
            return Problem(detail: $"Variable '{nombre}' no encontrada.", statusCode: StatusCodes.Status404NotFound);

        return Ok(variable);
    }

    /// <summary>
    /// Crear una nueva variable de configuración
    /// </summary>
    [HttpPost]
    [Authorize(Policy = "Admin")]
    public async Task<ActionResult<ConfiguracionVariableDto>> CrearVariable(
        CrearConfiguracionVariableDto dto,
        [FromServices] IValidator<CrearConfiguracionVariableDto> validator)
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

        if (_empresaProvider.EmpresaId == null)
            return Problem(detail: "Se requiere contexto de empresa.", statusCode: StatusCodes.Status400BadRequest);

        var existe = await _context.ConfiguracionesVariables
            .AnyAsync(c => c.Nombre == dto.Nombre && c.EmpresaId == _empresaProvider.EmpresaId);

        if (existe)
            return Problem(detail: $"Ya existe una variable con el nombre '{dto.Nombre}'.", statusCode: StatusCodes.Status409Conflict);

        var variable = new ConfiguracionVariable
        {
            Nombre = dto.Nombre,
            Valor = dto.Valor,
            Descripcion = dto.Descripcion,
            EmpresaId = _empresaProvider.EmpresaId.Value,
        };

        _context.ConfiguracionesVariables.Add(variable);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Variable de configuración creada. Id: {Id}, Nombre: {Nombre}", variable.Id, variable.Nombre);

        var result = new ConfiguracionVariableDto(
            variable.Id, variable.Nombre, variable.Valor, variable.Descripcion,
            variable.Activo, variable.FechaCreacion, variable.EmpresaId);

        return CreatedAtAction(nameof(ObtenerVariable), new { id = variable.Id }, result);
    }

    /// <summary>
    /// Actualizar una variable de configuración
    /// </summary>
    [HttpPut("{id:int}")]
    [Authorize(Policy = "Admin")]
    public async Task<ActionResult> ActualizarVariable(
        int id,
        ActualizarConfiguracionVariableDto dto,
        [FromServices] IValidator<ActualizarConfiguracionVariableDto> validator)
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

        var variable = await _context.ConfiguracionesVariables.FindAsync(id);
        if (variable == null)
            return Problem(detail: $"Variable {id} no encontrada.", statusCode: StatusCodes.Status404NotFound);

        var existe = await _context.ConfiguracionesVariables
            .AnyAsync(c => c.Nombre == dto.Nombre && c.EmpresaId == variable.EmpresaId && c.Id != id);

        if (existe)
            return Problem(detail: $"Ya existe otra variable con el nombre '{dto.Nombre}'.", statusCode: StatusCodes.Status409Conflict);

        variable.Nombre = dto.Nombre;
        variable.Valor = dto.Valor;
        variable.Descripcion = dto.Descripcion;

        await _context.SaveChangesAsync();

        _logger.LogInformation("Variable de configuración {Id} actualizada.", id);
        return NoContent();
    }

    /// <summary>
    /// Desactivar una variable de configuración (soft delete)
    /// </summary>
    [HttpDelete("{id:int}")]
    [Authorize(Policy = "Admin")]
    public async Task<ActionResult> DesactivarVariable(int id)
    {
        var variable = await _context.ConfiguracionesVariables.FindAsync(id);
        if (variable == null)
            return Problem(detail: $"Variable {id} no encontrada.", statusCode: StatusCodes.Status404NotFound);

        variable.Activo = false;
        await _context.SaveChangesAsync();

        _logger.LogInformation("Variable de configuración {Id} desactivada.", id);
        return NoContent();
    }
}
