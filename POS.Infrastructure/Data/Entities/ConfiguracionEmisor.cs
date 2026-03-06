namespace POS.Infrastructure.Data.Entities;

public class ConfiguracionEmisor
{
    public int Id { get; set; }
    public int SucursalId { get; set; }

    // Datos fiscales del emisor
    public string Nit { get; set; } = "";
    public string DigitoVerificacion { get; set; } = "";
    public string RazonSocial { get; set; } = "";
    public string NombreComercial { get; set; } = "";
    public string Direccion { get; set; } = "";
    public string CodigoMunicipio { get; set; } = "";
    public string CodigoDepartamento { get; set; } = "";
    public string Telefono { get; set; } = "";
    public string Email { get; set; } = "";
    public string CodigoCiiu { get; set; } = "";
    public string PerfilTributario { get; set; } = "REGIMEN_ORDINARIO";

    // Resolución DIAN
    public string NumeroResolucion { get; set; } = "";
    public DateTime FechaResolucion { get; set; }
    public string Prefijo { get; set; } = "FV";
    public long NumeroDesde { get; set; }
    public long NumeroHasta { get; set; }
    public long NumeroActual { get; set; }
    public DateTime FechaVigenciaDesde { get; set; }
    public DateTime FechaVigenciaHasta { get; set; }

    // Ambiente y software DIAN
    public string Ambiente { get; set; } = "2"; // "1"=Producción, "2"=Pruebas
    public string PinSoftware { get; set; } = "";
    public string IdSoftware { get; set; } = "";

    // Certificado digital (.p12 en base64)
    public string CertificadoBase64 { get; set; } = "";
    public string CertificadoPassword { get; set; } = "";

    // Navegación
    public Sucursal Sucursal { get; set; } = null!;
}
