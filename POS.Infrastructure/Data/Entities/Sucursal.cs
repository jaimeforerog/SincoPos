namespace POS.Infrastructure.Data.Entities;

public class Sucursal : EntidadAuditable
{
    public string Nombre { get; set; } = string.Empty;
    public string? Direccion { get; set; }
    public string? CodigoPais { get; set; } = "CO"; // ISO 3166-1 alpha-2
    public string? NombrePais { get; set; } = "Colombia";
    public string? Ciudad { get; set; }
    public string? Telefono { get; set; }
    public string? Email { get; set; }
    public string? CentroCosto { get; set; } // Codigo/Tag Financiero ERP
    public MetodoCosteo MetodoCosteo { get; set; } = MetodoCosteo.PromedioPonderado;

    // ── Tax Engine ──────────────────────────────────────────────────────────────
    /// <summary>
    /// Código DANE del municipio para calcular ReteICA territorial.
    /// Ej: "11001" = Bogotá D.C.
    /// </summary>
    public string? CodigoMunicipio { get; set; }

    /// <summary>
    /// Régimen tributario del emisor (vendedor).
    /// Valores: REGIMEN_ORDINARIO | REGIMEN_SIMPLE
    /// </summary>
    public string PerfilTributario { get; set; } = "REGIMEN_ORDINARIO";

    /// <summary>
    /// Valor de la UVT vigente en COP. Usado para calcular umbrales de retención
    /// y el flag REQUIRES_ELECTRONIC_INVOICE (5 UVT).
    /// Se actualiza cada año según DIAN.
    /// </summary>
    public decimal ValorUVT { get; set; } = 47065m; // 2026

    /// <summary>
    /// Días de anticipación para alertar sobre lotes próximos a vencer.
    /// 0 = alertas deshabilitadas.
    /// </summary>
    public int DiasAlertaVencimientoLotes { get; set; } = 30;
}

public enum MetodoCosteo
{
    PromedioPonderado = 0,
    PEPS = 1,              // FIFO
    UEPS = 2               // LIFO
}
