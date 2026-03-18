using POS.Domain.Events.Venta;

namespace POS.Domain.Aggregates;

/// <summary>
/// Capa 9 — Aprendizaje continuo (nivel individual).
/// Perfil enriquecido del cajero: velocidad de productos + franjas horarias + días activos.
/// A diferencia de UserBehavior (Capa 5 — solo top productos para POS),
/// este documento acumula el patrón completo para análisis de curva de aprendizaje.
/// Id = Guid.Parse(ExternalUserId) — mismo stream que UserBehavior.
/// </summary>
public class CashierPattern
{
    public Guid     Id               { get; set; }
    public string   ExternalUserId   { get; set; } = string.Empty;
    public int      TotalVentas      { get; set; }
    public DateTime UltimaActividad  { get; set; }

    /// <summary>productoId (string Guid) → unidades vendidas acumuladas</summary>
    public Dictionary<string, int> ProductoVelocidad { get; set; } = new();

    /// <summary>Top 20 producto IDs ordenados por velocidad descendente</summary>
    public List<string> TopProductos { get; set; } = new();

    /// <summary>hora del día (0–23) → cantidad de ventas en esa franja</summary>
    public Dictionary<int, int> HorasPico { get; set; } = new();

    /// <summary>día de semana ISO (1=lunes … 7=domingo) → cantidad de ventas</summary>
    public Dictionary<int, int> DiasActivos { get; set; } = new();

    /// <summary>Hora de mayor actividad (0–23). -1 si no hay datos.</summary>
    public int HoraPicoMaxima => HorasPico.Count > 0
        ? HorasPico.MaxBy(kv => kv.Value).Key
        : -1;

    /// <summary>Día más activo ISO (1–7). -1 si no hay datos.</summary>
    public int DiaMasActivo => DiasActivos.Count > 0
        ? DiasActivos.MaxBy(kv => kv.Value).Key
        : -1;

    public void Apply(VentaCompletadaEvent evt)
    {
        TotalVentas++;
        UltimaActividad = DateTime.UtcNow;

        foreach (var item in evt.Items)
        {
            var key = item.ProductoId.ToString();
            ProductoVelocidad.TryGetValue(key, out var prev);
            ProductoVelocidad[key] = prev + (int)Math.Ceiling(item.Cantidad);
        }

        TopProductos = ProductoVelocidad
            .OrderByDescending(kv => kv.Value)
            .Take(20)
            .Select(kv => kv.Key)
            .ToList();

        HorasPico.TryGetValue(evt.HoraDelDia, out var prevHora);
        HorasPico[evt.HoraDelDia] = prevHora + 1;

        DiasActivos.TryGetValue(evt.DiaSemana, out var prevDia);
        DiasActivos[evt.DiaSemana] = prevDia + 1;
    }
}
