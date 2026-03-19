namespace POS.Application.DTOs;

public record EmpresaDto(
    int Id,
    string Nombre,
    string? Nit,
    string? RazonSocial,
    bool Activo,
    DateTime FechaCreacion,
    int CantidadSucursales
);

public record CrearEmpresaDto(
    string Nombre,
    string? Nit,
    string? RazonSocial
);

public record ActualizarEmpresaDto(
    string Nombre,
    string? Nit,
    string? RazonSocial,
    bool Activo
);
