namespace POS.Infrastructure.Data.Entities;

public class Tercero : EntidadAuditable
{
    public TipoIdentificacion TipoIdentificacion { get; set; }
    public string Identificacion { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public TipoTercero TipoTercero { get; set; }
    public string? Telefono { get; set; }
    public string? Email { get; set; }
    public string? Direccion { get; set; }
    public string? Ciudad { get; set; }
    public OrigenDatos OrigenDatos { get; set; } = OrigenDatos.Local;
    public string? ExternalId { get; set; }
}

public enum TipoIdentificacion
{
    CC = 0,          // Cedula de Ciudadania
    NIT = 1,         // Numero de Identificacion Tributaria
    CE = 2,          // Cedula de Extranjeria
    Pasaporte = 3,
    TI = 4,          // Tarjeta de Identidad
    Otro = 5
}

public enum TipoTercero
{
    Cliente = 0,
    Proveedor = 1,
    Ambos = 2
}

public enum OrigenDatos
{
    Local = 0,
    ERP = 1
}
