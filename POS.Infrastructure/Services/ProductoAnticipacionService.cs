using Microsoft.EntityFrameworkCore;
using POS.Application.DTOs;
using POS.Application.Services;
using POS.Domain.Aggregates;
using POS.Infrastructure.Data;

namespace POS.Infrastructure.Services;

public sealed class ProductoAnticipacionService : IProductoAnticipacionService
{
    private readonly global::Marten.IDocumentSession _session;
    private readonly AppDbContext                    _context;

    public ProductoAnticipacionService(
        global::Marten.IDocumentSession session,
        AppDbContext                    context)
    {
        _session = session;
        _context = context;
    }

    public async Task<IReadOnlyList<ProductoDto>> ObtenerProductosAnticipados(
        string externalUserId,
        int    limite = 20)
    {
        if (!Guid.TryParse(externalUserId, out var streamId))
            return [];

        var behavior = await _session.LoadAsync<UserBehavior>(streamId);
        if (behavior == null || behavior.TopProductos.Count == 0)
            return [];

        var topIds = behavior.TopProductos
            .Take(limite)
            .Select(id => Guid.TryParse(id, out var g) ? g : Guid.Empty)
            .Where(g => g != Guid.Empty)
            .ToList();

        if (topIds.Count == 0) return [];

        var productos = await _context.Productos
            .Include(p => p.Impuesto)
            .Include(p => p.ConceptoRetencion)
            .Where(p => topIds.Contains(p.Id) && p.Activo)
            .ToListAsync();

        // Preservar el orden de frecuencia
        return topIds
            .Select(id => productos.FirstOrDefault(p => p.Id == id))
            .Where(p => p != null)
            .Select(p => new ProductoDto(
                p!.Id,
                p.CodigoBarras,
                p.Nombre,
                p.Descripcion,
                p.CategoriaId,
                p.PrecioVenta,
                p.PrecioCosto,
                p.Activo,
                p.FechaCreacion,
                p.ImpuestoId,
                p.Impuesto?.Nombre,
                p.Impuesto?.Tipo.ToString(),
                p.Impuesto?.Porcentaje,
                p.EsAlimentoUltraprocesado,
                p.GramosAzucarPor100ml,
                p.UnidadMedida,
                p.ConceptoRetencionId,
                p.ConceptoRetencion?.Nombre,
                p.ManejaLotes,
                p.DiasVidaUtil
            ))
            .ToList()
            .AsReadOnly();
    }
}
