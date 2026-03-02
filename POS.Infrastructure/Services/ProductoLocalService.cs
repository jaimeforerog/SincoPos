using Microsoft.EntityFrameworkCore;
using POS.Application.DTOs;
using POS.Application.Services;
using POS.Infrastructure.Data;
using POS.Infrastructure.Data.Entities;

namespace POS.Infrastructure.Services;

/// <summary>
/// Implementacion local de IProductoService.
/// Lee y escribe productos directamente en la base de datos del POS.
/// Para ERP, se crea una ProductoErpService que consulta al ERP externo.
/// </summary>
public class ProductoLocalService : IProductoService
{
    private readonly AppDbContext _context;

    public ProductoLocalService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<ProductoDto?> ObtenerPorIdAsync(Guid id)
    {
        return await _context.Productos
            .Where(p => p.Id == id)
            .Select(p => new ProductoDto(
                p.Id, p.CodigoBarras, p.Nombre, p.Descripcion,
                p.CategoriaId, p.PrecioVenta, p.PrecioCosto,
                p.Activo, p.FechaCreacion))
            .FirstOrDefaultAsync();
    }

    public async Task<ProductoDto?> ObtenerPorCodigoBarrasAsync(string codigoBarras)
    {
        return await _context.Productos
            .Where(p => p.CodigoBarras == codigoBarras)
            .Select(p => new ProductoDto(
                p.Id, p.CodigoBarras, p.Nombre, p.Descripcion,
                p.CategoriaId, p.PrecioVenta, p.PrecioCosto,
                p.Activo, p.FechaCreacion))
            .FirstOrDefaultAsync();
    }

    public async Task<List<ProductoDto>> BuscarAsync(string? query, int? categoriaId, bool incluirInactivos)
    {
        var q = _context.Productos.AsQueryable();

        if (!incluirInactivos)
            q = q.Where(p => p.Activo);

        if (!string.IsNullOrWhiteSpace(query))
            q = q.Where(p => p.Nombre.Contains(query) ||
                             p.CodigoBarras.Contains(query) ||
                             (p.Descripcion != null && p.Descripcion.Contains(query)));

        if (categoriaId.HasValue)
            q = q.Where(p => p.CategoriaId == categoriaId.Value);

        return await q
            .OrderBy(p => p.Nombre)
            .Select(p => new ProductoDto(
                p.Id, p.CodigoBarras, p.Nombre, p.Descripcion,
                p.CategoriaId, p.PrecioVenta, p.PrecioCosto,
                p.Activo, p.FechaCreacion))
            .ToListAsync();
    }

    public async Task<(ProductoDto? Result, string? Error)> CrearAsync(CrearProductoDto dto)
    {
        // Validar codigo unico
        var existe = await _context.Productos
            .AnyAsync(p => p.CodigoBarras == dto.CodigoBarras);
        if (existe)
            return (null, "El codigo de barras ya existe.");

        // Validar categoria
        var categoriaExiste = await _context.Categorias
            .AnyAsync(c => c.Id == dto.CategoriaId && c.Activo);
        if (!categoriaExiste)
            return (null, "La categoria no existe o esta inactiva.");

        var producto = new Producto
        {
            Id = Guid.NewGuid(),
            CodigoBarras = dto.CodigoBarras,
            Nombre = dto.Nombre,
            Descripcion = dto.Descripcion,
            CategoriaId = dto.CategoriaId,
            PrecioVenta = dto.PrecioVenta,
            PrecioCosto = dto.PrecioCosto,
            Activo = true,
            FechaCreacion = DateTime.UtcNow
        };

        _context.Productos.Add(producto);
        await _context.SaveChangesAsync();

        var result = new ProductoDto(
            producto.Id, producto.CodigoBarras, producto.Nombre,
            producto.Descripcion, producto.CategoriaId,
            producto.PrecioVenta, producto.PrecioCosto,
            producto.Activo, producto.FechaCreacion);

        return (result, null);
    }

    public async Task<(bool Success, string? Error)> ActualizarAsync(Guid id, ActualizarProductoDto dto)
    {
        var producto = await _context.Productos.FindAsync(id);
        if (producto == null)
            return (false, $"Producto {id} no encontrado.");

        if (!producto.Activo)
            return (false, "No se puede actualizar un producto inactivo.");

        producto.Nombre = dto.Nombre;
        producto.Descripcion = dto.Descripcion;
        producto.PrecioVenta = dto.PrecioVenta;
        producto.PrecioCosto = dto.PrecioCosto;
        producto.FechaModificacion = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        return (true, null);
    }

    public async Task<(bool Success, string? Error)> DesactivarAsync(Guid id, string? motivo)
    {
        var producto = await _context.Productos.FindAsync(id);
        if (producto == null)
            return (false, $"Producto {id} no encontrado.");

        if (!producto.Activo)
            return (false, "El producto ya esta inactivo.");

        producto.Activo = false;
        producto.FechaModificacion = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        return (true, null);
    }
}
