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
    private readonly ICurrentEmpresaProvider _empresaProvider;

    public ProductoLocalService(AppDbContext context, ICurrentEmpresaProvider empresaProvider)
    {
        _context = context;
        _empresaProvider = empresaProvider;
    }

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
        p.UnidadMedida,
        // Concepto Retención
        p.ConceptoRetencionId,
        p.ConceptoRetencion?.Nombre,
        p.ManejaLotes,
        p.DiasVidaUtil
    );

    // ── Queries ───────────────────────────────────────────────────────────────

    public async Task<ProductoDto?> ObtenerPorIdAsync(Guid id) =>
        await _context.Productos
            .IgnoreQueryFilters()
            .Include(p => p.Impuesto)
            .Include(p => p.ConceptoRetencion)
            .Where(p => p.Id == id)
            .Select(p => ToDto(p))
            .FirstOrDefaultAsync();

    public async Task<ProductoDto?> ObtenerPorCodigoBarrasAsync(string codigoBarras) =>
        await _context.Productos
            .IgnoreQueryFilters()
            .Include(p => p.Impuesto)
            .Include(p => p.ConceptoRetencion)
            .Where(p => p.CodigoBarras == codigoBarras)
            .Select(p => ToDto(p))
            .FirstOrDefaultAsync();

    public async Task<PaginatedResult<ProductoDto>> BuscarAsync(string? query, int? categoriaId, bool incluirInactivos, int page = 1, int pageSize = 50)
    {
        // Nota: NO incluimos Impuesto aquí porque el filtro global excluiría los registros
        // globales de seed (EmpresaId = null). Los cargamos por separado con IgnoreQueryFilters.
        var q = incluirInactivos
            ? _context.Productos.IgnoreQueryFilters().Include(p => p.ConceptoRetencion)
            : (IQueryable<Producto>)_context.Productos.Include(p => p.ConceptoRetencion).Where(p => p.Activo);

        if (!string.IsNullOrWhiteSpace(query))
            q = q.Where(p => p.Nombre.Contains(query) ||
                             p.CodigoBarras.Contains(query) ||
                             (p.Descripcion != null && p.Descripcion.Contains(query)));

        if (categoriaId.HasValue)
            q = q.Where(p => p.CategoriaId == categoriaId.Value);

        var totalCount = await q.CountAsync();
        var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

        var list = await q.OrderBy(p => p.Nombre)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        // Cargar impuestos con IgnoreQueryFilters para acceder también a registros globales (EmpresaId = null)
        var impuestoIds = list.Where(p => p.ImpuestoId.HasValue).Select(p => p.ImpuestoId!.Value).Distinct().ToList();
        if (impuestoIds.Count > 0)
        {
            var impuestosDict = await _context.Impuestos
                .IgnoreQueryFilters()
                .Where(i => impuestoIds.Contains(i.Id))
                .ToDictionaryAsync(i => i.Id);

            foreach (var p in list)
                if (p.ImpuestoId.HasValue && impuestosDict.TryGetValue(p.ImpuestoId.Value, out var imp))
                    p.Impuesto = imp;
        }

        var items = list.Select(ToDto).ToList();
        return new PaginatedResult<ProductoDto>(items, totalCount, page, pageSize, totalPages);
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

        if (dto.ConceptoRetencionId.HasValue)
        {
            var conceptoExiste = await _context.ConceptosRetencion.AnyAsync(c => c.Id == dto.ConceptoRetencionId && c.Activo);
            if (!conceptoExiste)
                return (null, $"El concepto de retención con Id {dto.ConceptoRetencionId} no existe o está inactivo.");
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
            ConceptoRetencionId = dto.ConceptoRetencionId,
            ManejaLotes = dto.ManejaLotes,
            DiasVidaUtil = dto.DiasVidaUtil,
            EmpresaId = _empresaProvider.EmpresaId,
        };

        _context.Productos.Add(producto);
        await _context.SaveChangesAsync();

        await _context.Entry(producto).Reference(p => p.Impuesto).LoadAsync();
        await _context.Entry(producto).Reference(p => p.ConceptoRetencion).LoadAsync();
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

        if (dto.ConceptoRetencionId.HasValue)
        {
            var conceptoExiste = await _context.ConceptosRetencion.AnyAsync(c => c.Id == dto.ConceptoRetencionId && c.Activo);
            if (!conceptoExiste)
                return (false, $"El concepto de retención con Id {dto.ConceptoRetencionId} no existe o está inactivo.");
        }

        producto.Nombre = dto.Nombre;
        producto.Descripcion = dto.Descripcion;
        producto.PrecioVenta = dto.PrecioVenta;
        producto.PrecioCosto = dto.PrecioCosto;
        producto.ImpuestoId = dto.ImpuestoId;
        producto.EsAlimentoUltraprocesado = dto.EsAlimentoUltraprocesado;
        producto.GramosAzucarPor100ml = dto.GramosAzucarPor100ml;
        producto.UnidadMedida = dto.UnidadMedida;
        producto.ConceptoRetencionId = dto.ConceptoRetencionId;
        producto.ManejaLotes = dto.ManejaLotes;
        producto.DiasVidaUtil = dto.DiasVidaUtil;
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
