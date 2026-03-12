using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using POS.Application.DTOs;
using POS.Application.Validators;
using POS.Infrastructure.Data;
using POS.Infrastructure.Data.Entities;
using POS.Application.Services;

namespace POS.Api.Controllers;

[Authorize]
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]")]
public class PreciosController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly IPrecioService _precioService;
    private readonly ILogger<PreciosController> _logger;

    public PreciosController(
        AppDbContext context,
        IPrecioService precioService,
        ILogger<PreciosController> logger)
    {
        _context = context;
        _precioService = precioService;
        _logger = logger;
    }

    /// <summary>
    /// Listar precios configurados explícitamente para una sucursal (tabla <c>precios_sucursal</c>).
    /// No incluye precios resueltos por cascada — solo los registros existentes en la tabla.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(List<PrecioSucursalDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<PrecioSucursalDto>>> ListarPorSucursal([FromQuery] int sucursalId)
    {
        var precios = await _context.PreciosSucursal
            .Where(p => p.SucursalId == sucursalId)
            .Select(p => new PrecioSucursalDto(
                p.Id, p.ProductoId, p.Producto.Nombre,
                p.SucursalId, p.Sucursal.Nombre,
                p.PrecioVenta, p.PrecioMinimo, p.FechaModificacion))
            .ToListAsync();

        return Ok(precios);
    }

    /// <summary>
    /// Resolver precios de TODOS los productos activos para una sucursal en una sola llamada (batch).
    /// Usa 3 queries en total — sin N+1. Ideal para cargar el catálogo POS al abrir el turno.
    /// </summary>
    /// <remarks>
    /// Cascada de resolución para cada producto:<br/>
    /// 1. <c>PrecioSucursal</c> específico de esta sucursal<br/>
    /// 2. <c>Producto.PrecioVenta</c> (precio base del catálogo)<br/>
    /// 3. <c>Costo × (1 + MargenCategoria)</c> (calculado)<br/>
    /// El campo <c>Origen</c> en la respuesta indica qué nivel aplicó: "Sucursal" | "Producto" | "Margen".
    /// </remarks>
    [HttpGet("resolver-lote")]
    [ProducesResponseType(typeof(List<PrecioResueltoLoteItemDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<PrecioResueltoLoteItemDto>>> ResolverPrecioLote([FromQuery] int sucursalId)
    {
        return Ok(await _precioService.ResolverPrecioLote(sucursalId));
    }

    /// <summary>
    /// Consultar precio resuelto de un único producto en una sucursal.
    /// Para cargar múltiples productos en el POS, use <c>/resolver-lote</c>.
    /// </summary>
    [HttpGet("resolver")]
    [ProducesResponseType(typeof(PrecioResueltoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PrecioResueltoDto>> ConsultarPrecio(
        [FromQuery] Guid productoId, [FromQuery] int sucursalId)
    {
        try
        {
            return Ok(await _precioService.ResolverPrecio(productoId, sucursalId));
        }
        catch (InvalidOperationException ex)
        {
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status400BadRequest);
        }
    }

    /// <summary>
    /// Crear o actualizar precio de un producto en una sucursal (upsert).
    /// Si ya existe un precio para ese producto+sucursal, lo actualiza.
    /// </summary>
    /// <response code="200">Precio creado o actualizado.</response>
    /// <response code="400">Producto o sucursal no encontrados, o precio inválido.</response>
    [HttpPost]
    [Authorize(Policy = "Supervisor")]
    [ProducesResponseType(typeof(PrecioSucursalDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PrecioSucursalDto>> CrearPrecioSucursal(
        [FromBody] CrearPrecioSucursalDto dto)
    {
        var validator = new CrearPrecioSucursalValidator();
        var validResult = await validator.ValidateAsync(dto);
        if (!validResult.IsValid)
        {
            var errors = validResult.Errors
                .GroupBy(e => e.PropertyName)
                .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray());
            foreach (var (key, messages) in errors)
                foreach (var msg in messages)
                    ModelState.AddModelError(key, msg);
            return ValidationProblem();
        }

        var producto = await _context.Productos.FindAsync(dto.ProductoId);
        if (producto == null) return Problem(detail: "Producto no encontrado.", statusCode: StatusCodes.Status400BadRequest);

        var sucursal = await _context.Sucursales.FindAsync(dto.SucursalId);
        if (sucursal == null) return Problem(detail: "Sucursal no encontrada.", statusCode: StatusCodes.Status400BadRequest);

        var existente = await _context.PreciosSucursal
            .FirstOrDefaultAsync(p => p.ProductoId == dto.ProductoId && p.SucursalId == dto.SucursalId);

        if (existente != null)
        {
            existente.PrecioVenta = dto.PrecioVenta;
            existente.PrecioMinimo = dto.PrecioMinimo;
            existente.OrigenDato = dto.OrigenDato ?? existente.OrigenDato;
            // FechaModificacion y ModificadoPor se establecen automáticamente en SaveChangesAsync
        }
        else
        {
            existente = new PrecioSucursal
            {
                ProductoId = dto.ProductoId,
                SucursalId = dto.SucursalId,
                PrecioVenta = dto.PrecioVenta,
                PrecioMinimo = dto.PrecioMinimo,
                OrigenDato = dto.OrigenDato ?? "Manual"
            };
            _context.PreciosSucursal.Add(existente);
        }

        await _context.SaveChangesAsync();

        _logger.LogInformation("Precio actualizado para Producto {ProductoId} en Sucursal {SucursalId}: {Precio}",
            dto.ProductoId, dto.SucursalId, dto.PrecioVenta);

        return Ok(new PrecioSucursalDto(
            existente.Id, existente.ProductoId, producto.Nombre,
            existente.SucursalId, sucursal.Nombre,
            existente.PrecioVenta, existente.PrecioMinimo, existente.FechaModificacion));
    }

    /// <summary>
    /// Listar todos los precios configurados por sucursal para un producto específico.
    /// </summary>
    /// <response code="200">Lista de precios por sucursal (puede estar vacía si no hay precios configurados).</response>
    /// <response code="404">Producto no encontrado.</response>
    [HttpGet("producto/{productoId:guid}")]
    [ProducesResponseType(typeof(List<PrecioSucursalDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<List<PrecioSucursalDto>>> ListarPreciosProducto(Guid productoId)
    {
        var producto = await _context.Productos.FindAsync(productoId);
        if (producto == null) return Problem(detail: "Producto no encontrado.", statusCode: StatusCodes.Status404NotFound);

        var precios = await _context.PreciosSucursal
            .Include(p => p.Sucursal)
            .Where(p => p.ProductoId == productoId)
            .Select(p => new PrecioSucursalDto(
                p.Id, p.ProductoId, producto.Nombre,
                p.SucursalId, p.Sucursal.Nombre,
                p.PrecioVenta, p.PrecioMinimo, p.FechaModificacion))
            .ToListAsync();

        return Ok(precios);
    }
}
