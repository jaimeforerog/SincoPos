namespace POS.Domain.Aggregates;

using POS.Domain.Events.Venta;

/// <summary>
/// Documento Marten que acumula el historial de compras de un cliente.
/// Id = ClienteId (int, FK a Terceros).
/// Capa 4 — Dependencias inteligentes: cuando una venta se completa, el historial
/// del cliente se actualiza automáticamente sin intervención manual.
/// </summary>
public class ClienteHistorial
{
    public int      Id          { get; set; }  // = ClienteId
    public int      ClienteId   { get; set; }
    public int      TotalCompras    { get; set; }
    public decimal  TotalGastado    { get; set; }
    public decimal  GastoPromedio   => TotalCompras > 0 ? TotalGastado / TotalCompras : 0m;
    public DateTime PrimeraVisita   { get; set; }
    public DateTime UltimaVisita    { get; set; }
    public DateTime UltimaActualizacion { get; set; }

    /// <summary>
    /// productoId (string Guid) → cantidad total comprada acumulada
    /// </summary>
    public Dictionary<string, int> ProductoFrecuencia { get; set; } = new();

    /// <summary>
    /// productoId (string Guid) → nombre del producto (último nombre visto)
    /// </summary>
    public Dictionary<string, string> ProductoNombres { get; set; } = new();

    /// <summary>
    /// Top 10 producto IDs ordenados por frecuencia descendente
    /// </summary>
    public List<string> TopProductos { get; set; } = new();

    /// <summary>
    /// Día de semana (0-6) → número de visitas
    /// </summary>
    public Dictionary<int, int> VisitasPorDiaSemana { get; set; } = new();

    /// <summary>
    /// Hora del día (0-23) → número de visitas
    /// </summary>
    public Dictionary<int, int> VisitasPorHora { get; set; } = new();

    public void Apply(VentaCompletadaEvent evt, DateTime timestamp)
    {
        TotalCompras++;
        TotalGastado += evt.Total;
        UltimaActualizacion = timestamp;

        if (TotalCompras == 1)
            PrimeraVisita = timestamp;
        UltimaVisita = timestamp;

        foreach (var item in evt.Items)
        {
            var key = item.ProductoId.ToString();
            ProductoFrecuencia.TryGetValue(key, out var count);
            ProductoFrecuencia[key] = count + (int)Math.Ceiling(item.Cantidad);
            ProductoNombres[key] = item.NombreProducto;
        }

        TopProductos = ProductoFrecuencia
            .OrderByDescending(kv => kv.Value)
            .Take(10)
            .Select(kv => kv.Key)
            .ToList();

        // Frecuencias de visita
        var dia = (int)timestamp.DayOfWeek;
        VisitasPorDiaSemana.TryGetValue(dia, out var dcount);
        VisitasPorDiaSemana[dia] = dcount + 1;

        var hora = timestamp.Hour;
        VisitasPorHora.TryGetValue(hora, out var hcount);
        VisitasPorHora[hora] = hcount + 1;
    }
}
