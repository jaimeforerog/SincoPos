using POS.Domain.Events.Venta;

namespace POS.Domain.Aggregates;

/// <summary>
/// Capa 9 — Aprendizaje continuo (nivel organizacional).
/// Patrón de ventas de la tienda: velocidad de productos, horas pico, días activos.
/// Alimenta la Capa 14 (Radar de Negocio) para forecasting y detección de anomalías.
/// Id = SucursalId (int) — un documento por sucursal.
/// </summary>
public class StorePattern
{
    public int      Id                    { get; set; }  // = SucursalId
    public int      SucursalId            { get; set; }
    public int      TotalVentas           { get; set; }
    public DateTime UltimaActualizacion   { get; set; }

    /// <summary>productoId (string Guid) → unidades vendidas acumuladas en la tienda</summary>
    public Dictionary<string, int> ProductoVelocidad { get; set; } = new();

    /// <summary>Top 50 producto IDs de la tienda por volumen</summary>
    public List<string> TopProductos { get; set; } = new();

    /// <summary>hora del día (0–23) → cantidad de ventas</summary>
    public Dictionary<int, int> HorasPico { get; set; } = new();

    /// <summary>día de semana ISO (1=lunes … 7=domingo) → cantidad de ventas</summary>
    public Dictionary<int, int> DiasActivos { get; set; } = new();

    /// <summary>
    /// Promedio de ventas por hora pico (total ventas / horas con actividad).
    /// Insumo para detectar anomalías en Capa 14.
    /// </summary>
    public double PromedioVentasPorHora => HorasPico.Count > 0
        ? HorasPico.Values.Average()
        : 0;

    /// <summary>Hora pico de la tienda (0–23). -1 si no hay datos.</summary>
    public int HoraPicoMaxima => HorasPico.Count > 0
        ? HorasPico.MaxBy(kv => kv.Value).Key
        : -1;

    public void Apply(VentaCompletadaEvent evt)
    {
        TotalVentas++;
        UltimaActualizacion = DateTime.UtcNow;

        foreach (var item in evt.Items)
        {
            var key = item.ProductoId.ToString();
            ProductoVelocidad.TryGetValue(key, out var prev);
            ProductoVelocidad[key] = prev + (int)Math.Ceiling(item.Cantidad);
        }

        TopProductos = ProductoVelocidad
            .OrderByDescending(kv => kv.Value)
            .Take(50)
            .Select(kv => kv.Key)
            .ToList();

        HorasPico.TryGetValue(evt.HoraDelDia, out var prevHora);
        HorasPico[evt.HoraDelDia] = prevHora + 1;

        DiasActivos.TryGetValue(evt.DiaSemana, out var prevDia);
        DiasActivos[evt.DiaSemana] = prevDia + 1;
    }
}
