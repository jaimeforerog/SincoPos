namespace POS.Application.DTOs;

public record CrearSucursalDto(
    string Nombre,
    string? Direccion,
    string? Ciudad,
    string? Telefono,
    string? Email,
    string? MetodoCosteo
);

public record ActualizarSucursalDto(
    string Nombre,
    string? Direccion,
    string? Ciudad,
    string? Telefono,
    string? Email,
    string? MetodoCosteo
);

public record SucursalDto(
    int Id,
    string Nombre,
    string? Direccion,
    string? Ciudad,
    string? Telefono,
    string? Email,
    string MetodoCosteo,
    bool Activa,
    DateTime FechaCreacion
);
