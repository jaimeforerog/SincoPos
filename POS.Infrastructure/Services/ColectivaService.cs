using Microsoft.EntityFrameworkCore;
using POS.Application.Services;
using POS.Domain.Aggregates;
using POS.Infrastructure.Data;

namespace POS.Infrastructure.Services;

/// <summary>
/// Capa 13 — Inteligencia colectiva (implementación local).
/// Lee ProductoCombo y StorePattern desde Marten + nombres de sucursales desde EF Core.
/// </summary>
public class ColectivaService : IColectivaService
{
    private readonly global::Marten.IDocumentSession _session;
    private readonly AppDbContext _context;

    public ColectivaService(
        global::Marten.IDocumentSession session,
        AppDbContext context)
    {
        _session = session;
        _context = context;
    }

    // ── Combos ────────────────────────────────────────────────────────────

    public async Task<List<ComboProductoDto>> ObtenerCombosAsync(int sucursalId, int top = 15)
    {
        var combo = await _session.LoadAsync<ProductoCombo>(sucursalId);
        if (combo == null || combo.TotalVentas == 0) return [];

        return combo.TopCombos(top)
            .Select(t =>
            {
                var ids = t.Par.Split(':');
                var idA = ids[0]; var idB = ids[1];
                combo.NombresProducto.TryGetValue(idA, out var nombreA);
                combo.NombresProducto.TryGetValue(idB, out var nombreB);
                return new ComboProductoDto(
                    idA, nombreA ?? idA,
                    idB, nombreB ?? idB,
                    t.Count, t.Frecuencia);
            })
            .ToList();
    }

    // ── Comparación cross-sucursal ─────────────────────────────────────────

    public async Task<PatronComparativoDto> CompararSucursalesAsync(int empresaId)
    {
        // Sucursales de la empresa
        var sucursales = await _context.Sucursales
            .Where(s => s.EmpresaId == empresaId && s.Activo)
            .Select(s => new { s.Id, s.Nombre })
            .ToListAsync();

        if (sucursales.Count == 0)
            return new PatronComparativoDto([], []);

        // Cargar StorePattern de cada sucursal
        var patrones = new List<(string Nombre, StorePattern? Patron)>();
        foreach (var s in sucursales)
        {
            var patron = await _session.LoadAsync<StorePattern>(s.Id);
            patrones.Add((s.Nombre, patron));
        }

        // Obtener top productos a nivel empresa (unión de top 20 por sucursal)
        var topGlobal = patrones
            .Where(p => p.Patron != null)
            .SelectMany(p => p.Patron!.ProductoVelocidad)
            .GroupBy(kv => kv.Key)
            .OrderByDescending(g => g.Sum(kv => kv.Value))
            .Take(20)
            .Select(g => g.Key)
            .ToHashSet();

        // Recuperar nombres de productos (desde cualquier combo o directo de EF)
        var productoIds = topGlobal
            .Select(id => Guid.TryParse(id, out var g) ? g : Guid.Empty)
            .Where(g => g != Guid.Empty)
            .ToList();

        var nombres = await _context.Productos
            .Where(p => productoIds.Contains(p.Id))
            .Select(p => new { Id = p.Id.ToString(), p.Nombre })
            .ToDictionaryAsync(p => p.Id, p => p.Nombre);

        var items = topGlobal.Select(pid =>
        {
            var velocidadPorSucursal = patrones
                .Where(p => p.Patron != null)
                .ToDictionary(
                    p => p.Nombre,
                    p => p.Patron!.ProductoVelocidad.GetValueOrDefault(pid, 0));

            nombres.TryGetValue(pid, out var nombre);
            return new ProductoVelocidadComparativoDto(
                pid, nombre ?? pid, velocidadPorSucursal);
        }).ToList();

        return new PatronComparativoDto(
            sucursales.Select(s => s.Nombre).ToList(),
            items);
    }

    // ── Estado global ──────────────────────────────────────────────────────

    public EstadoGlobalDto ObtenerEstadoGlobal() => new(
        ServicioCentralDisponible: false,
        Mensaje: "Modo local activo. El servicio central Sinco (multi-tenant) no está disponible. " +
                 "Cada sucursal opera con sus patrones locales. " +
                 "Criterio de activación global: mínimo 5 tiendas con ≥ 90 días de datos.",
        UltimaActualizacionGlobal: null);
}
