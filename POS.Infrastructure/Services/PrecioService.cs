using Microsoft.EntityFrameworkCore;
using POS.Infrastructure.Data;
using POS.Infrastructure.Data.Entities;

namespace POS.Infrastructure.Services;

/// <summary>
/// Resuelve precio con cascada: PrecioSucursal -> Producto.PrecioVenta -> Costo × (1 + Margen)
/// </summary>
public class PrecioService
{
    private readonly AppDbContext _context;

    public PrecioService(AppDbContext context) => _context = context;

    /// <summary>
    /// Resuelve el precio de venta para un producto en una sucursal.
    /// Prioridad: 1) PrecioSucursal, 2) Producto.PrecioVenta, 3) Costo × (1 + Margen)
    /// </summary>
    public async Task<PrecioResuelto> ResolverPrecio(Guid productoId, int sucursalId)
    {
        // 1. Buscar precio especifico por sucursal
        var precioSuc = await _context.PreciosSucursal
            .FirstOrDefaultAsync(p => p.ProductoId == productoId && p.SucursalId == sucursalId);

        if (precioSuc != null)
        {
            return new PrecioResuelto(
                precioSuc.PrecioVenta,
                precioSuc.PrecioMinimo,
                "Sucursal",
                precioSuc.OrigenDato
            );
        }

        // 2. Precio base del producto
        var producto = await _context.Productos
            .Include(p => p.Categoria)
            .FirstOrDefaultAsync(p => p.Id == productoId);

        if (producto == null)
            throw new InvalidOperationException($"Producto {productoId} no encontrado.");

        if (producto.PrecioVenta > 0)
        {
            return new PrecioResuelto(producto.PrecioVenta, null, "Producto");
        }

        // 3. Calcular desde costo + margen categoria
        var stock = await _context.Stock
            .FirstOrDefaultAsync(s => s.ProductoId == productoId && s.SucursalId == sucursalId);

        var costo = stock?.CostoPromedio ?? producto.PrecioCosto;
        var margen = producto.Categoria.MargenGanancia;
        var precioCalculado = Math.Round(costo * (1 + margen), 2);

        return new PrecioResuelto(precioCalculado, costo, "Margen");
    }

    /// <summary>
    /// Valida que el precio solicitado no sea menor al minimo permitido.
    /// </summary>
    public async Task<(bool valido, string? error)> ValidarPrecio(
        Guid productoId, int sucursalId, decimal precioSolicitado)
    {
        var precioSuc = await _context.PreciosSucursal
            .FirstOrDefaultAsync(p => p.ProductoId == productoId && p.SucursalId == sucursalId);

        if (precioSuc?.PrecioMinimo != null && precioSolicitado < precioSuc.PrecioMinimo)
        {
            return (false, $"Precio {precioSolicitado} es menor al minimo permitido ({precioSuc.PrecioMinimo}).");
        }

        // No permitir vender por debajo del costo
        var stock = await _context.Stock
            .FirstOrDefaultAsync(s => s.ProductoId == productoId && s.SucursalId == sucursalId);
        if (stock != null && precioSolicitado < stock.CostoPromedio)
        {
            return (false, $"Precio {precioSolicitado} es menor al costo ({stock.CostoPromedio}).");
        }

        return (true, null);
    }
}

public record PrecioResuelto(decimal PrecioVenta, decimal? PrecioMinimo, string Origen, string? OrigenDato = null);
