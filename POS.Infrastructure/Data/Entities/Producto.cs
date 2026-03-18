using POS.Domain;

namespace POS.Infrastructure.Data.Entities;

public class Producto : ISoftDelete
{
    public Guid Id { get; set; }  // Mismo Guid que el StreamId de Marten

    /// <summary>
    /// Empresa propietaria del producto. Null = catálogo global (legado).
    /// </summary>
    public int? EmpresaId { get; set; }
    public string CodigoBarras { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public int CategoriaId { get; set; }
    public int? ImpuestoId { get; set; } // Opcional, null = Exento
    public decimal PrecioVenta { get; set; }
    public decimal PrecioCosto { get; set; }
    public bool Activo { get; set; } = true;
    public DateTime? FechaDesactivacion { get; set; }

    // ── Impuestos Saludables (Ley 2277/2022) ──────────────────────────────────
    /// <summary>
    /// Marca como alimento ultraprocesado. El TaxEngine aplica la tarifa
    /// del impuesto saludable vigente sobre la base imponible.
    /// </summary>
    /// <summary>
    /// Unidad de medida DIAN (UN/ECE). Ej: "94"=Unidad, "KGM"=Kg, "LTR"=Litro.
    /// Requerida en factura electrónica.
    /// </summary>
    public string UnidadMedida { get; set; } = "94"; // 94 = Unidad (default)

    public bool EsAlimentoUltraprocesado { get; set; } = false;

    /// <summary>
    /// Indica que este producto requiere registro de número de lote y fecha de vencimiento
    /// al recibir mercancía. La venta usa lógica FEFO (primero en vencer, primero en salir).
    /// </summary>
    public bool ManejaLotes { get; set; } = false;

    /// <summary>
    /// Vida útil del producto en días. Si se define, al recibir un lote sin fecha de
    /// vencimiento explícita el sistema calcula: FechaEntrada + DiasVidaUtil.
    /// Null = sin plazo predeterminado (la fecha debe ingresarse manualmente).
    /// </summary>
    public int? DiasVidaUtil { get; set; }

    /// <summary>
    /// Solo para bebidas azucaradas: contenido de azúcar en g/100ml.
    /// El TaxEngine consulta la tabla de tramos DIAN para calcular el impuesto.
    /// Null = no aplica.
    /// </summary>
    public decimal? GramosAzucarPor100ml { get; set; }

    // Auditoría
    public string CreadoPor { get; set; } = string.Empty;
    public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;
    public string? ModificadoPor { get; set; }
    public DateTime? FechaModificacion { get; set; }

    /// <summary>
    /// Concepto de retención DIAN del producto. Determina qué regla de ReteFuente aplica.
    /// Null = sin concepto específico (aplican todas las reglas sin filtro de concepto).
    /// </summary>
    public int? ConceptoRetencionId { get; set; }

    // Navegacion
    public Categoria Categoria { get; set; } = null!;
    public Impuesto? Impuesto { get; set; }
    public ConceptoRetencion? ConceptoRetencion { get; set; }
}

public class Categoria : EntidadAuditable
{
    /// <summary>
    /// Empresa propietaria de la categoría. Null = catálogo global (legado).
    /// </summary>
    public int? EmpresaId { get; set; }

    public string Nombre { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public decimal MargenGanancia { get; set; } = 0.30m; // 30% default

    // Jerarquía
    public int? CategoriaPadreId { get; set; }
    public int Nivel { get; set; } = 0; // 0 = raíz, 1 = subcategoría, etc.
    public string RutaCompleta { get; set; } = string.Empty; // "Alimentos > Granos > Arroz"

    // Integración ERP y Contable
    public string? CuentaInventario { get; set; }
    public string? CuentaCosto { get; set; }
    public string? CuentaIngreso { get; set; }
    public string? ExternalId { get; set; }
    public OrigenDatos OrigenDatos { get; set; } = OrigenDatos.Local;

    // Navegacion
    public Categoria? CategoriaPadre { get; set; }
    public ICollection<Categoria> SubCategorias { get; set; } = new List<Categoria>();
    public ICollection<Producto> Productos { get; set; } = new List<Producto>();
}
