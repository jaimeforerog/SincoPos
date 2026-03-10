namespace POS.Infrastructure.Services.Erp;

public class ErpSincoOptions
{
    public const string SectionName = "ErpSinco";

    public string BaseUrl { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
    public int TimeoutSeconds { get; set; } = 30;
    public int MaxReintentos { get; set; } = 5;

    /// <summary>
    /// Cuenta contable PUC para Cuentas por Pagar a proveedores nacionales.
    /// Default: 220501 (Proveedores Nacionales).
    /// </summary>
    public string CuentaCxPProveedores { get; set; } = "220501";
}
