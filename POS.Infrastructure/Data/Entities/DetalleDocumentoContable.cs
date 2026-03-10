namespace POS.Infrastructure.Data.Entities;

public class DetalleDocumentoContable
{
    public int Id { get; set; }
    
    public int DocumentoContableId { get; set; }
    public DocumentoContable DocumentoContable { get; set; } = null!;
    
    public string CuentaContable { get; set; } = string.Empty; 
    public string CentroCosto { get; set; } = string.Empty;
    public string Naturaleza { get; set; } = string.Empty; // "Debito" o "Credito"
    public decimal Valor { get; set; }
    public string Nota { get; set; } = string.Empty;
}
