using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using POS.Application.DTOs;
using POS.Application.Services;

namespace POS.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class ProductosController : ControllerBase
{
    private readonly IProductoService _productoService;
    private readonly ILogger<ProductosController> _logger;

    public ProductosController(
        IProductoService productoService,
        ILogger<ProductosController> logger)
    {
        _productoService = productoService;
        _logger = logger;
    }

    /// <summary>
    /// Crear un nuevo producto
    /// </summary>
    [HttpPost]
    [Authorize(Policy = "Supervisor")]
    public async Task<ActionResult<ProductoDto>> CrearProducto(
        CrearProductoDto dto,
        [FromServices] IValidator<CrearProductoDto> validator)
    {
        var validationResult = await validator.ValidateAsync(dto);
        if (!validationResult.IsValid)
        {
            var errors = validationResult.Errors
                .GroupBy(e => e.PropertyName)
                .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray());
            return BadRequest(new { errors });
        }

        var (result, error) = await _productoService.CrearAsync(dto);
        if (error != null)
            return Conflict(new { error });

        _logger.LogInformation("Producto creado: {Id} - {Nombre}", result!.Id, result.Nombre);
        return CreatedAtAction(nameof(ObtenerProducto), new { id = result.Id }, result);
    }

    /// <summary>
    /// Obtener un producto por ID
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ProductoDto>> ObtenerProducto(Guid id)
    {
        var producto = await _productoService.ObtenerPorIdAsync(id);
        if (producto == null)
            return NotFound(new { error = $"Producto {id} no encontrado." });
        return Ok(producto);
    }

    /// <summary>
    /// Buscar producto por codigo de barras
    /// </summary>
    [HttpGet("codigo/{codigoBarras}")]
    public async Task<ActionResult<ProductoDto>> ObtenerPorCodigoBarras(string codigoBarras)
    {
        var producto = await _productoService.ObtenerPorCodigoBarrasAsync(codigoBarras);
        if (producto == null)
            return NotFound(new { error = $"Producto con codigo '{codigoBarras}' no encontrado." });
        return Ok(producto);
    }

    /// <summary>
    /// Listar productos
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<List<ProductoDto>>> ObtenerProductos(
        [FromQuery] string? query = null,
        [FromQuery] int? categoriaId = null,
        [FromQuery] bool incluirInactivos = false)
    {
        var productos = await _productoService.BuscarAsync(query, categoriaId, incluirInactivos);
        return Ok(productos);
    }

    /// <summary>
    /// Actualizar un producto
    /// </summary>
    [HttpPut("{id:guid}")]
    [Authorize(Policy = "Supervisor")]
    public async Task<ActionResult> ActualizarProducto(Guid id, ActualizarProductoDto dto)
    {
        var (success, error) = await _productoService.ActualizarAsync(id, dto);
        if (!success)
            return error!.Contains("no encontrado") ? NotFound(new { error }) : BadRequest(new { error });

        _logger.LogInformation("Producto {Id} actualizado.", id);
        return NoContent();
    }

    /// <summary>
    /// Desactivar un producto (soft delete)
    /// </summary>
    [HttpDelete("{id:guid}")]
    [Authorize(Policy = "Admin")]
    public async Task<ActionResult> DesactivarProducto(Guid id, [FromQuery] string? motivo)
    {
        var (success, error) = await _productoService.DesactivarAsync(id, motivo);
        if (!success)
            return error!.Contains("no encontrado") ? NotFound(new { error }) : BadRequest(new { error });

        _logger.LogInformation("Producto {Id} desactivado.", id);
        return NoContent();
    }
}
