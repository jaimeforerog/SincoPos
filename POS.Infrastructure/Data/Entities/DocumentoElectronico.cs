namespace POS.Infrastructure.Data.Entities;

public class DocumentoElectronico : EntidadAuditable
{
    public int? VentaId { get; set; }
    public int SucursalId { get; set; }
    public string TipoDocumento { get; set; } = ""; // "FV","NC","ND"
    public string Prefijo { get; set; } = "";
    public long Numero { get; set; }
    public string NumeroCompleto { get; set; } = ""; // "FV000001"
    public string Cufe { get; set; } = "";           // SHA-384
    public DateTime FechaEmision { get; set; }
    public string XmlUbl { get; set; } = "";         // XML firmado
    public EstadoDocumento Estado { get; set; } = EstadoDocumento.Pendiente;
    public DateTime? FechaEnvioDian { get; set; }
    public string? CodigoRespuestaDian { get; set; }
    public string? MensajeRespuestaDian { get; set; }
    public int Intentos { get; set; } = 0;

    // Navegación
    public Venta? Venta { get; set; }
    public Sucursal Sucursal { get; set; } = null!;
}

public enum EstadoDocumento
{
    Pendiente = 0,
    Generado = 1,
    Firmado = 2,
    Enviado = 3,
    Aceptado = 4,
    Rechazado = 5
}
