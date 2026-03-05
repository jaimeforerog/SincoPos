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

    public ProductoLocalService(AppDbContext context) => _context = context;

    // ── Projection helper ─────────────────────────────────────────────────────

    private static ProductoDto ToDto(Producto p) => new(
        p.Id, p.CodigoBarras, p.Nombre, p.Descripcion,
        p.CategoriaId, p.PrecioVenta, p.PrecioCosto,
        p.Activo, p.FechaCreacion,
        // Tax Engine fields
        p.ImpuestoId,
        p.Impuesto?.Nombre,
        p.Impuesto?.Tipo.ToString(),
        p.Impuesto?.Porcentaje,
        p.EsAlimentoUltraprocesado,
        p.GramosAzucarPor100ml,
        p.UnidadMedida
    );

    // ── Queries ───────────────────────────────────────────────────────────────

    public async Task<ProductoDto?> ObtenerPorIdAsync(Guid id) =>
        await _context.Productos
            .Include(p => p.Impuesto)
            .Where(p => p.Id == id)
            .Select(p => ToDto(p))
            .FirstOrDefaultAsync();

    public async Task<ProductoDto?> ObtenerPorCodigoBarrasAsync(string codigoBarras) =>
        await _context.Productos
            .Include(p => p.Impuesto)
            .Where(p => p.CodigoBarras == codigoBarras)
            .Select(p => ToDto(p))
            .FirstOrDefaultAsync();

    public async Task<List<ProductoDto>> BuscarAsync(string? query, int? categoriaId, bool incluirInactivos)
    {
        var q = _context.Productos.Include(p => p.Impuesto).AsQueryable();

        if (!incluirInactivos) q = q.Where(p => p.Activo);

        if (!string.IsNullOrWhiteSpace(query))
            q = q.Where(p => p.Nombre.Contains(query) ||
                             p.CodigoBarras.Contains(query) ||
                             (p.Descripcion != null && p.Descripcion.Contains(query)));

        if (categoriaId.HasValue)
            q = q.Where(p => p.CategoriaId == categoriaId.Value);

        var productos = await q.OrderBy(p => p.Nombre).ToListAsync();
        return productos.Select(ToDto).ToList();
    }

    // ── Commands ──────────────────────────────────────────────────────────────

    public async Task<(ProductoDto? Result, string? Error)> CrearAsync(CrearProductoDto dto)
    {
        var existe = await _context.Productos.AnyAsync(p => p.CodigoBarras == dto.CodigoBarras);
        if (existe)
            return (null, $"El código de barras '{dto.CodigoBarras}' ya está registrado en otro producto.");

        var categoriaExiste = await _context.Categorias.AnyAsync(c => c.Id == dto.CategoriaId && c.Activo);
        if (!categoriaExiste)
            return (null, "La categoría especificada no existe o está inactiva.");

        if (dto.ImpuestoId.HasValue)
        {
            var impuestoExiste = await _context.Impuestos.AnyAsync(i => i.Id == dto.ImpuestoId && i.Activo);
            if (!impuestoExiste)
                return (null, $"El impuesto con Id {dto.ImpuestoId} no existe o está inactivo.");
        }

        var producto = new Producto
        {
            Id = Guid.NewGuid(),
            CodigoBarras = dto.CodigoBarras,
            Nombre = dto.Nombre,
            Descripcion = dto.Descripcion,
            CategoriaId = dto.CategoriaId,
            PrecioVenta = dto.PrecioVenta,
            PrecioCosto = dto.PrecioCosto,
            ImpuestoId = dto.ImpuestoId,
            EsAlimentoUltraprocesado = dto.EsAlimentoUltraprocesado,
            GramosAzucarPor100ml = dto.GramosAzucarPor100ml,
            UnidadMedida = dto.UnidadMedida,
            Activo = true,
            FechaCreacion = DateTime.UtcNow
        };

        _context.Productos.Add(producto);
        await _context.SaveChangesAsync();

        await _context.Entry(producto).Reference(p => p.Impuesto).LoadAsync();
        return (ToDto(producto), null);
    }

    public async Task<(bool Success, string? Error)> ActualizarAsync(Guid id, ActualizarProductoDto dto)
    {
        var producto = await _context.Productos.FindAsync(id);
        if (producto == null) return (false, $"Producto {id} no encontrado.");
        if (!producto.Activo) return (false, "No se puede actualizar un producto inactivo.");

        if (dto.ImpuestoId.HasValue)
        {
            var impuestoExiste = await _context.Impuestos.AnyAsync(i => i.Id == dto.ImpuestoId && i.Activo);
            if (!impuestoExiste)
                return (false, $"El impuesto con Id {dto.ImpuestoId} no existe o está inactivo.");
        }

        producto.Nombre = dto.Nombre;
        producto.Descripcion = dto.Descripcion;
        producto.PrecioVenta = dto.PrecioVenta;
        producto.PrecioCosto = dto.PrecioCosto;
        producto.ImpuestoId = dto.ImpuestoId;
        producto.EsAlimentoUltraprocesado = dto.EsAlimentoUltraprocesado;
        producto.GramosAzucarPor100ml = dto.GramosAzucarPor100ml;
        producto.UnidadMedida = dto.UnidadMedida;
        producto.FechaModificacion = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        return (true, null);
    }

    public async Task<(bool Success, string? Error)> DesactivarAsync(Guid id, string? motivo)
    {
        var producto = await _context.Productos.FindAsync(id);
        if (producto == null) return (false, $"Producto {id} no encontrado.");
        if (!producto.Activo) return (false, "El producto ya esta inactivo.");

        producto.Activo = false;
        producto.FechaModificacion = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        return (true, null);
    }
}
