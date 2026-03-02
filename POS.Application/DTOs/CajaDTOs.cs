namespace POS.Application.DTOs;

public record CrearCajaDto(
    string Nombre,
    int SucursalId
);

public record AbrirCajaDto(
    decimal MontoApertura
);

public record CerrarCajaDto(
    decimal MontoReal,
    string? Observaciones
);

public record CajaDto(
    int Id,
    string Nombre,
    int SucursalId,
    string? NombreSucursal,
    string Estado,
    decimal MontoApertura,
    decimal MontoActual,
    DateTime? FechaApertura,
    DateTime? FechaCierre,
    bool Activa
);
