namespace POS.Application.DTOs;

// ─── Configuración Emisor ─────────────────────────────────

public record ConfiguracionEmisorDto(
    int Id,
    int SucursalId,
    string NombreSucursal,
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
    DateTime FechaResolucion,
    string Prefijo,
    long NumeroDesde,
    long NumeroHasta,
    long NumeroActual,
    DateTime FechaVigenciaDesde,
    DateTime FechaVigenciaHasta,
    string Ambiente,
    string PinSoftware,
    string IdSoftware,
    bool TieneCertificado
);

public record ActualizarConfiguracionEmisorDto(
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
    DateTime FechaResolucion,
    string Prefijo,
    long NumeroDesde,
    long NumeroHasta,
    DateTime FechaVigenciaDesde,
    DateTime FechaVigenciaHasta,
    string Ambiente,
    string PinSoftware,
    string IdSoftware,
    string? CertificadoBase64,   // null = no cambiar
    string? CertificadoPassword  // null = no cambiar
);

// ─── Documentos Electrónicos ─────────────────────────────

public record DocumentoElectronicoDto(
    int Id,
    int? VentaId,
    int SucursalId,
    string NombreSucursal,
    string TipoDocumento,
    string Prefijo,
    long Numero,
    string NumeroCompleto,
    string Cufe,
    DateTime FechaEmision,
    string Estado,
    int CodigoEstado,
    DateTime? FechaEnvioDian,
    string? CodigoRespuestaDian,
    string? MensajeRespuestaDian,
    int Intentos,
    DateTime FechaCreacion
);

public record FiltroDocumentosElectronicosDto(
    int? SucursalId,
    DateTime? FechaDesde,
    DateTime? FechaHasta,
    string? TipoDocumento,
    int? Estado,
    int PageNumber = 1,
    int PageSize = 20
);
