namespace POS.Infrastructure.Data.Entities;

/// <summary>
/// Tabla maestra para la configuracion de impuestos (IVA, Consumo, etc.)
/// </summary>
public class Impuesto : EntidadAuditable
{
    public string Nombre { get; set; } = string.Empty; // Ej. "IVA 19%", "Exento 0%"
    public decimal Porcentaje { get; set; }            // Ej. 0.19m

    // Navegacion
    public ICollection<Producto> Productos { get; set; } = new List<Producto>();
}
