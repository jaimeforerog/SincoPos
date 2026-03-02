namespace POS.Infrastructure.Data.Entities;

public class Producto
{
    public Guid Id { get; set; }  // Mismo Guid que el StreamId de Marten
    public string CodigoBarras { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public int CategoriaId { get; set; }
    public int? ImpuestoId { get; set; } // Opcional, null = Exento
    public decimal PrecioVenta { get; set; }
    public decimal PrecioCosto { get; set; }
    public bool Activo { get; set; } = true;

    // Auditoría
    public string CreadoPor { get; set; } = string.Empty;
    public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;
    public string? ModificadoPor { get; set; }
    public DateTime? FechaModificacion { get; set; }

    // Navegacion
    public Categoria Categoria { get; set; } = null!;
    public Impuesto? Impuesto { get; set; }
}

public class Categoria : EntidadAuditable
{
    public string Nombre { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public decimal MargenGanancia { get; set; } = 0.30m; // 30% default

    // Navegacion
    public ICollection<Producto> Productos { get; set; } = new List<Producto>();
}
