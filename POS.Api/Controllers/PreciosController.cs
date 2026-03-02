using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using POS.Application.DTOs;
using POS.Application.Validators;
using POS.Infrastructure.Data;
using POS.Infrastructure.Data.Entities;
using POS.Infrastructure.Services;

namespace POS.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class PreciosController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly PrecioService _precioService;
    private readonly ILogger<PreciosController> _logger;

    public PreciosController(
        AppDbContext context,
        PrecioService precioService,
        ILogger<PreciosController> logger)
    {
        _context = context;
        _precioService = precioService;
        _logger = logger;
    }

    /// <summary>
    /// Consultar precio resuelto de un producto en una sucursal.
    /// Prioridad: PrecioSucursal → Producto.PrecioVenta → Costo × (1 + Margen)
    /// </summary>
    [HttpGet("resolver")]
    public async Task<ActionResult<PrecioResueltoDto>> ConsultarPrecio(
        [FromQuery] Guid productoId, [FromQuery] int sucursalId)
    {
        try
        {
            var precio = await _precioService.ResolverPrecio(productoId, sucursalId);
            return Ok(new PrecioResueltoDto(precio.PrecioVenta, precio.PrecioMinimo, precio.Origen));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    /// <summary>
    /// Crear o actualizar precio de un producto en una sucursal.
    /// </summary>
    [HttpPost]
    [Authorize(Policy = "Supervisor")]
    public async Task<ActionResult<PrecioSucursalDto>> CrearPrecioSucursal(
        [FromBody] CrearPrecioSucursalDto dto)
    {
        var validator = new CrearPrecioSucursalValidator();
        var validResult = await validator.ValidateAsync(dto);
        if (!validResult.IsValid)
            return BadRequest(validResult.Errors.Select(e => e.ErrorMessage));

        var producto = await _context.Productos.FindAsync(dto.ProductoId);
        if (producto == null) return BadRequest("Producto no encontrado.");

        var sucursal = await _context.Sucursales.FindAsync(dto.SucursalId);
        if (sucursal == null) return BadRequest("Sucursal no encontrada.");

        var existente = await _context.PreciosSucursal
            .FirstOrDefaultAsync(p => p.ProductoId == dto.ProductoId && p.SucursalId == dto.SucursalId);

        if (existente != null)
        {
            existente.PrecioVenta = dto.PrecioVenta;
            existente.PrecioMinimo = dto.PrecioMinimo;
            // FechaModificacion y ModificadoPor se establecen automáticamente en SaveChangesAsync
        }
        else
        {
            existente = new PrecioSucursal
            {
                ProductoId = dto.ProductoId,
                SucursalId = dto.SucursalId,
                PrecioVenta = dto.PrecioVenta,
                PrecioMinimo = dto.PrecioMinimo
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
    /// Listar todos los precios por sucursal de un producto.
    /// </summary>
    [HttpGet("producto/{productoId:guid}")]
    public async Task<ActionResult<List<PrecioSucursalDto>>> ListarPreciosProducto(Guid productoId)
    {
        var producto = await _context.Productos.FindAsync(productoId);
        if (producto == null) return NotFound("Producto no encontrado.");

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
