namespace POS.Infrastructure.Data.Entities;

/// <summary>
/// Tabla maestra de impuestos del Tax Engine.
/// Soporta IVA, INC, Impuestos Saludables (Ley 2277/2022) e Impuesto a las Bolsas.
/// </summary>
public class Impuesto : EntidadAuditable
{
    public string Nombre { get; set; } = string.Empty;       // "IVA 19%", "INC 8%", "Bolsa $66"
    public TipoImpuesto Tipo { get; set; } = TipoImpuesto.IVA;

    /// <summary>Porcentaje como decimal: 0.19 = 19%. Nulo si el impuesto es ValorFijo.</summary>
    public decimal Porcentaje { get; set; }

    /// <summary>Valor fijo por unidad (ej. Impuesto a la Bolsa: $66 por bolsa en 2026).</summary>
    public decimal? ValorFijo { get; set; }

    /// <summary>
    /// Cuenta contable para GL Mapping (ej. "2408" = IVA por pagar).
    /// Si es retención: cuenta de activo (ej. "1355" = Anticipo impuestos).
    /// </summary>
    public string? CodigoCuentaContable { get; set; }

    /// <summary>
    /// true  = impuesto acumulativo sobre la base (IVA).
    /// false = impuesto monofásico excluyente (INC — no se suma con IVA).
    /// </summary>
    public bool AplicaSobreBase { get; set; } = true;

    /// <summary>ISO 3166-1 alpha-2. Permite filtrar impuestos por país.</summary>
    public string CodigoPais { get; set; } = "CO";

    public string? Descripcion { get; set; }

    // Navegacion
    public ICollection<Producto> Productos { get; set; } = new List<Producto>();
}

/// <summary>
/// Clasifica el tipo de impuesto para que el TaxEngine aplique la lógica correcta.
/// </summary>
public enum TipoImpuesto
{
    IVA = 0,         // Impuesto al Valor Agregado (0%, 5%, 19%)
    INC = 1,         // Impuesto Nacional al Consumo — monofásico (8% restaurantes)
    Saludable = 2,   // Ultraprocesados / Bebidas Azucaradas (Ley 2277/2022)
    Bolsa = 3        // Impuesto al consumo de bolsas plásticas (valor fijo/unidad)
}

/// <summary>
/// Reglas de retención configurables por el administrador.
/// Las retenciones dependen del cruce de perfiles Vendedor/Comprador.
/// </summary>
public class RetencionRegla : EntidadAuditable
{
    public string Nombre { get; set; } = string.Empty;        // "ReteFuente 2.5%"
    public TipoRetencion Tipo { get; set; }
    public decimal Porcentaje { get; set; }                   // 0.025

    /// <summary>
    /// Umbral mínimo en UVT para que aplique la retención.
    /// Si subtotal < BaseMinUVT × ValorUVT, no retiene.
    /// Default 4 UVT (aprox. $188.260 COP en 2026).
    /// </summary>
    public decimal BaseMinUVT { get; set; } = 4;

    /// <summary>
    /// Código DANE del municipio para ReteICA territorial.
    /// Null = aplica a todos los municipios.
    /// </summary>
    public string? CodigoMunicipio { get; set; }

    /// <summary>Perfil tributario del vendedor/emisor que activa esta regla.</summary>
    public string PerfilVendedor { get; set; } = "REGIMEN_ORDINARIO";

    /// <summary>Perfil tributario del comprador/receptor que activa esta regla.</summary>
    public string PerfilComprador { get; set; } = "GRAN_CONTRIBUYENTE";

    /// <summary>
    /// Cuenta contable del activo (ej. "1355" = Anticipo de impuestos).
    /// </summary>
    public string? CodigoCuentaContable { get; set; }

    /// <summary>
    /// Concepto de retención DIAN asociado. Solo para ReteFuente.
    /// Si se especifica, la regla solo aplica a productos con el mismo concepto.
    /// </summary>
    public int? ConceptoRetencionId { get; set; }
    public ConceptoRetencion? ConceptoRetencion { get; set; }
}

public enum TipoRetencion
{
    ReteFuente = 0,   // Retención en la fuente (renta)
    ReteICA = 1,      // Retención de Industria y Comercio (territorial)
    ReteIVA = 2       // Retención del IVA
}

/// <summary>
/// Concepto de retención DIAN. Clasifica el tipo de actividad económica
/// para aplicar la tarifa de retención en la fuente correcta.
/// Códigos DIAN: 2301=Honorarios, 2302=Comisiones, 2304=Servicios, 2306=Arrendamientos, 2307=Compras.
/// </summary>
public class ConceptoRetencion : EntidadAuditable
{
    public string Nombre { get; set; } = string.Empty;

    /// <summary>Código DIAN del concepto (ej. "2301", "2307").</summary>
    public string? CodigoDian { get; set; }

    /// <summary>Porcentaje sugerido de referencia para la UI (ej. 2.5, 11).</summary>
    public decimal? PorcentajeSugerido { get; set; }

    // Navegación
    public ICollection<RetencionRegla> ReglasRetencion { get; set; } = new List<RetencionRegla>();
    public ICollection<Producto> Productos { get; set; } = new List<Producto>();
}
