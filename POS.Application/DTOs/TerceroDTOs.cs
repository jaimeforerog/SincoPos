namespace POS.Application.DTOs;

public record CrearTerceroDto(
    string TipoIdentificacion,
    string Identificacion,
    string Nombre,
    string TipoTercero,
    string? Telefono,
    string? Email,
    string? Direccion,
    string? Ciudad
);

public record ActualizarTerceroDto(
    string Nombre,
    string? TipoTercero,
    string? Telefono,
    string? Email,
    string? Direccion,
    string? Ciudad
);

public record TerceroDto(
    int Id,
    string TipoIdentificacion,
    string Identificacion,
    string Nombre,
    string TipoTercero,
    string? Telefono,
    string? Email,
    string? Direccion,
    string? Ciudad,
    string OrigenDatos,
    string? ExternalId,
    bool Activo
);
