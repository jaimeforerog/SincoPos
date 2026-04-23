namespace POS.Infrastructure.Services.Erp;

public sealed class ErpSincoOptions
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

    /// <summary>
    /// Cuenta contable PUC para Caja General (efectivo).
    /// Default: 110505 (Caja General).
    /// </summary>
    public string CuentaCaja { get; set; } = "110505";

    /// <summary>
    /// Cuenta contable PUC para Cuentas por Cobrar a clientes.
    /// Default: 130505 (Clientes Nacionales).
    /// </summary>
    public string CuentaCxCClientes { get; set; } = "130505";

    /// <summary>
    /// Cuenta contable PUC para pagos con tarjeta débito/crédito.
    /// Default: 111005 (Bancos - Cuenta Corriente).
    /// </summary>
    public string CuentaTarjeta { get; set; } = "111005";

    /// <summary>
    /// Cuenta contable PUC para pagos por transferencia bancaria.
    /// Default: 111010 (Bancos - Cuenta de Ahorros).
    /// </summary>
    public string CuentaTransferencia { get; set; } = "111010";
}
