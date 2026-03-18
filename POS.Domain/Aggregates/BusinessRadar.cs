using POS.Domain.Events.Venta;

namespace POS.Domain.Aggregates;

/// <summary>
/// Capa 14 — Radar de Negocio.
/// Documento Marten por sucursal que acumula ingresos diarios + velocidad de productos
/// para proyecciones intradiarias y detección de riesgos de ruptura.
/// Id = SucursalId (int).
/// </summary>
public class BusinessRadar
{
    public int      Id          { get; set; }  // = SucursalId
    public int      SucursalId  { get; set; }
    public DateTime UltimaActualizacion { get; set; }

    /// <summary>"yyyy-MM-dd" → ingresos totales del día</summary>
    public Dictionary<string, decimal> IngresosPorFecha { get; set; } = new();

    /// <summary>"yyyy-MM-dd" → cantidad de ventas del día</summary>
    public Dictionary<string, int> VentasPorFecha { get; set; } = new();

    /// <summary>"yyyy-MM-dd:HH" → ingresos acumulados en esa hora</summary>
    public Dictionary<string, decimal> IngresosPorFechaHora { get; set; } = new();

    /// <summary>productoId.ToString() → unidades vendidas acumuladas (todas las fechas)</summary>
    public Dictionary<string, decimal> ProductoVelocidad { get; set; } = new();

    public void Apply(VentaCompletadaEvent evt, DateTime timestamp)
    {
        UltimaActualizacion = timestamp;

        var fecha     = timestamp.ToString("yyyy-MM-dd");
        var fechaHora = $"{fecha}:{evt.HoraDelDia:D2}";

        IngresosPorFecha.TryAdd(fecha, 0m);
        IngresosPorFecha[fecha] += evt.Total;

        VentasPorFecha.TryAdd(fecha, 0);
        VentasPorFecha[fecha]++;

        IngresosPorFechaHora.TryAdd(fechaHora, 0m);
        IngresosPorFechaHora[fechaHora] += evt.Total;

        foreach (var item in evt.Items)
        {
            var key = item.ProductoId.ToString();
            ProductoVelocidad.TryGetValue(key, out var prev);
            ProductoVelocidad[key] = prev + item.Cantidad;
        }
    }
}
