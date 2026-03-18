namespace POS.Domain.Aggregates;

using POS.Domain.Events.Venta;

/// <summary>
/// Documento Marten que acumula el comportamiento de venta de un cajero.
/// Id = Guid del stream = Guid.Parse(ExternalUserId).
/// Alimenta la Capa 5: anticipación de productos en el POS.
/// </summary>
public class UserBehavior
{
    public Guid   Id                { get; set; }
    public string ExternalUserId    { get; set; } = string.Empty;
    public int    TotalVentas       { get; set; }
    public DateTime UltimaActualizacion { get; set; }

    /// <summary>
    /// productoId (string Guid) → cantidad total vendida (acumulada)
    /// </summary>
    public Dictionary<string, int> ProductoFrecuencia { get; set; } = new();

    /// <summary>
    /// Top N producto IDs ordenados por frecuencia descendente (max 20)
    /// </summary>
    public List<string> TopProductos { get; set; } = new();

    public void Apply(VentaCompletadaEvent evt)
    {
        TotalVentas++;
        UltimaActualizacion = DateTime.UtcNow;

        foreach (var item in evt.Items)
        {
            var key = item.ProductoId.ToString();
            ProductoFrecuencia.TryGetValue(key, out var count);
            ProductoFrecuencia[key] = count + (int)Math.Ceiling(item.Cantidad);
        }

        TopProductos = ProductoFrecuencia
            .OrderByDescending(kv => kv.Value)
            .Take(20)
            .Select(kv => kv.Key)
            .ToList();
    }
}
