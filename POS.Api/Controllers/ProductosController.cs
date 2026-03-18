using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using POS.Api.Extensions;
using POS.Application.DTOs;
using POS.Application.Services;

namespace POS.Api.Controllers;

[Authorize]
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]")]
public class ProductosController : ControllerBase
{
    private readonly IProductoService _productoService;
    private readonly ILoteService _loteService;
    private readonly IProductoAnticipacionService _anticipacionService;
    private readonly ILogger<ProductosController> _logger;

    public ProductosController(
        IProductoService productoService,
        ILoteService loteService,
        IProductoAnticipacionService anticipacionService,
        ILogger<ProductosController> logger)
    {
        _productoService = productoService;
        _loteService = loteService;
        _anticipacionService = anticipacionService;
        _logger = logger;
    }

    /// <summary>
    /// Crear un nuevo producto.
    /// </summary>
    /// <response code="201">Producto creado.</response>
    /// <response code="400">Validación fallida.</response>
    /// <response code="409">Ya existe un producto con el mismo código de barras.</response>
    [HttpPost]
    [Authorize(Policy = "Supervisor")]
    [ProducesResponseType(typeof(ProductoDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
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
            foreach (var (key, messages) in errors)
                foreach (var msg in messages)
                    ModelState.AddModelError(key, msg);
            return ValidationProblem();
        }

        var (result, error) = await _productoService.CrearAsync(dto);
        if (error != null)
            return Problem(detail: error, statusCode: StatusCodes.Status409Conflict);

        _logger.LogInformation("Producto creado: {Id} - {Nombre}", result!.Id, result.Nombre);
        return CreatedAtAction(nameof(ObtenerProducto), new { id = result.Id }, result);
    }

    /// <summary>
    /// Obtener un producto por ID.
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ProductoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProductoDto>> ObtenerProducto(Guid id)
    {
        var producto = await _productoService.ObtenerPorIdAsync(id);
        if (producto == null)
            return Problem(detail: $"Producto {id} no encontrado.", statusCode: StatusCodes.Status404NotFound);
        return Ok(producto);
    }

    /// <summary>
    /// Buscar producto por código de barras.
    /// </summary>
    [HttpGet("codigo/{codigoBarras}")]
    [ProducesResponseType(typeof(ProductoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProductoDto>> ObtenerPorCodigoBarras(string codigoBarras)
    {
        var producto = await _productoService.ObtenerPorCodigoBarrasAsync(codigoBarras);
        if (producto == null)
            return Problem(detail: $"Producto con codigo '{codigoBarras}' no encontrado.", statusCode: StatusCodes.Status404NotFound);
        return Ok(producto);
    }

    /// <summary>
    /// Listar y buscar productos con filtros opcionales.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(PaginatedResult<ProductoDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PaginatedResult<ProductoDto>>> ObtenerProductos(
        [FromQuery] string? query = null,
        [FromQuery] int? categoriaId = null,
        [FromQuery] bool incluirInactivos = false,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        if (pageSize > 100) pageSize = 100;
        if (page < 1) page = 1;

        var productos = await _productoService.BuscarAsync(query, categoriaId, incluirInactivos, page, pageSize);
        return Ok(productos);
    }

    /// <summary>
    /// Actualizar un producto.
    /// </summary>
    [HttpPut("{id:guid}")]
    [Authorize(Policy = "Supervisor")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> ActualizarProducto(Guid id, ActualizarProductoDto dto)
    {
        var (success, error) = await _productoService.ActualizarAsync(id, dto);
        if (!success)
            return error!.Contains("no encontrado") ? Problem(detail: error, statusCode: StatusCodes.Status404NotFound) : Problem(detail: error, statusCode: StatusCodes.Status400BadRequest);

        _logger.LogInformation("Producto {Id} actualizado.", id);
        return NoContent();
    }

    /// <summary>
    /// Obtener los lotes de un producto en una sucursal específica.
    /// Requiere que el producto tenga ManejaLotes = true.
    /// </summary>
    /// <param name="id">Id del producto.</param>
    /// <param name="sucursalId">Id de la sucursal.</param>
    /// <param name="soloVigentes">Si true (default), excluye lotes agotados.</param>
    [HttpGet("{id:guid}/lotes")]
    [Authorize(Policy = "Supervisor")]
    [ProducesResponseType(typeof(List<LoteDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<List<LoteDto>>> ObtenerLotesProducto(
        Guid id,
        [FromQuery] int sucursalId,
        [FromQuery] bool soloVigentes = true)
    {
        var producto = await _productoService.ObtenerPorIdAsync(id);
        if (producto == null)
            return Problem(detail: $"Producto {id} no encontrado.", statusCode: StatusCodes.Status404NotFound);

        var lotes = await _loteService.ObtenerLotesAsync(id, sucursalId, soloVigentes);
        return Ok(lotes);
    }

    /// <summary>
    /// Desactivar un producto (soft delete). El producto deja de aparecer en búsquedas.
    /// </summary>
    [HttpDelete("{id:guid}")]
    [Authorize(Policy = "Admin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> DesactivarProducto(Guid id, [FromQuery] string? motivo)
    {
        var (success, error) = await _productoService.DesactivarAsync(id, motivo);
        if (!success)
            return error!.Contains("no encontrado") ? Problem(detail: error, statusCode: StatusCodes.Status404NotFound) : Problem(detail: error, statusCode: StatusCodes.Status400BadRequest);

        _logger.LogInformation("Producto {Id} desactivado.", id);
        return NoContent();
    }

    /// <summary>
    /// Capa 5 — Retorna los productos más frecuentes del cajero autenticado.
    /// Alimentado por UserBehaviorProjection vía eventos VentaCompletadaEvent.
    /// </summary>
    [HttpGet("anticipados")]
    [Authorize]
    [ProducesResponseType(typeof(IReadOnlyList<ProductoDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ProductoDto>>> ObtenerAnticipados([FromQuery] int limite = 20)
    {
        var externalId = User.GetExternalId();
        if (string.IsNullOrEmpty(externalId))
            return Ok(Array.Empty<ProductoDto>());

        var productos = await _anticipacionService.ObtenerProductosAnticipados(externalId, limite);
        return Ok(productos);
    }
}
