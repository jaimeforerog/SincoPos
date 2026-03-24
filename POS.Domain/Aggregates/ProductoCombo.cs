using POS.Domain.Events.Venta;

namespace POS.Domain.Aggregates;

/// <summary>
/// Capa 13 — Inteligencia colectiva (nivel local).
/// Acumula qué productos se venden juntos en la misma transacción dentro de una sucursal.
/// Los pares de productos frecuentes alimentan sugerencias de cross-selling.
///
/// Id = SucursalId (int) — un documento por sucursal.
///
/// Nivel global (servicio central Sinco multi-tenant): 🔮 Futuro.
/// Modo degradado: cada sucursal opera con sus patrones locales.
/// </summary>
public class ProductoCombo
{
    public int Id { get; set; }  // = SucursalId

    public int SucursalId { get; set; }

    /// <summary>
    /// "productoIdA:productoIdB" (ordenado) → número de veces que aparecieron juntos.
    /// Los IDs se ordenan lexicográficamente para evitar duplicados A:B vs B:A.
    /// </summary>
    public Dictionary<string, int> Combos { get; set; } = new();

    /// <summary>Snapshot del nombre de cada producto para evitar JOINs.</summary>
    public Dictionary<string, string> NombresProducto { get; set; } = new();

    /// <summary>Total de ventas analizadas (para calcular frecuencia relativa).</summary>
    public int TotalVentas { get; set; }

    public DateTime UltimaActualizacion { get; set; }

    /// <summary>
    /// Devuelve los N combos más frecuentes con frecuencia relativa.
    /// </summary>
    public List<(string Par, int Count, double Frecuencia)> TopCombos(int top = 10)
    {
        if (TotalVentas == 0) return [];
        return Combos
            .OrderByDescending(kv => kv.Value)
            .Take(top)
            .Select(kv => (kv.Key, kv.Value, (double)kv.Value / TotalVentas))
            .ToList();
    }

    public void Apply(VentaCompletadaEvent evt)
    {
        TotalVentas++;
        UltimaActualizacion = DateTime.UtcNow;

        var items = evt.Items;
        if (items.Count < 2) return;

        for (var i = 0; i < items.Count; i++)
        {
            var a = items[i];
            NombresProducto[a.ProductoId.ToString()] = a.NombreProducto;

            for (var j = i + 1; j < items.Count; j++)
            {
                var b = items[j];
                NombresProducto[b.ProductoId.ToString()] = b.NombreProducto;

                var ids = new[] { a.ProductoId.ToString(), b.ProductoId.ToString() };
                Array.Sort(ids, StringComparer.Ordinal);
                var key = $"{ids[0]}:{ids[1]}";

                Combos.TryGetValue(key, out var prev);
                Combos[key] = prev + 1;
            }
        }
    }
}
