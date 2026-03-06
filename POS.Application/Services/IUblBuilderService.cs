namespace POS.Application.Services;

public record EmisorUblData(
    string Nit,
    string DigitoVerificacion,
    string RazonSocial,
    string NombreComercial,
    string Direccion,
    string CodigoMunicipio,
    string CodigoDepartamento,
    string Telefono,
    string Email,
    string CodigoCiiu,
    string PerfilTributario,
    string NumeroResolucion,
    DateTime FechaVigenciaDesde,
    DateTime FechaVigenciaHasta,
    string Prefijo,
    long NumeroDesde,
    long NumeroHasta,
    string Ambiente,
    string PinSoftware,
    string IdSoftware,
    string CertificadoBase64,
    string CertificadoPassword
);

public record VentaUblData(
    // Cabecera
    string NumeroCompleto,
    string Prefijo,
    long Numero,
    DateTime FechaEmision,
    // Totales
    decimal SubtotalSinImpuestos,
    decimal TotalIva19,
    decimal TotalIva5,
    decimal TotalInc,
    decimal Total,
    // Emisor (tomado de ConfiguracionEmisor)
    // Receptor
    string? ClienteNit,
    string? ClienteDV,
    string? ClienteNombre,
    string? ClienteDireccion,
    string? ClienteCodigoMunicipio,
    string? ClienteEmail,
    string ClienteTipoDocumento,   // "13"=CC, "31"=NIT, "22"=CE
    string ClientePerfilTributario,
    // Líneas
    List<LineaUblData> Lineas
);

public record LineaUblData(
    int NumeroLinea,
    string CodigoProducto,
    string DescripcionProducto,
    string UnidadMedida,
    decimal Cantidad,
    decimal PrecioUnitario,
    decimal Descuento,
    decimal Subtotal,
    decimal PorcentajeIva,
    decimal MontoIva
);

public record DevolucionUblData(
    string NumeroCompleto,
    string Prefijo,
    long Numero,
    DateTime FechaEmision,
    string CufeFacturaOriginal,
    string NumeroFacturaOriginal,
    DateTime FechaFacturaOriginal,
    string MotivoDevolucion,
    decimal SubtotalSinImpuestos,
    decimal TotalIva19,
    decimal TotalIva5,
    decimal TotalInc,
    decimal Total,
    string? ClienteNit,
    string? ClienteDV,
    string? ClienteNombre,
    string? ClienteDireccion,
    string? ClienteCodigoMunicipio,
    string? ClienteEmail,
    string ClienteTipoDocumento,
    string ClientePerfilTributario,
    List<LineaUblData> Lineas
);

public interface IUblBuilderService
{
    (string xml, string cufe) GenerarFacturaVenta(VentaUblData data, EmisorUblData emisor);
    (string xml, string cufe) GenerarNotaCredito(DevolucionUblData data, EmisorUblData emisor);
}
