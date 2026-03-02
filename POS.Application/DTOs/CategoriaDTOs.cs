namespace POS.Application.DTOs;

public record CrearCategoriaDto(
    string Nombre,
    string? Descripcion
);

public record ActualizarCategoriaDto(
    string Nombre,
    string? Descripcion
);

public record CategoriaDto(
    int Id,
    string Nombre,
    string? Descripcion,
    bool Activa
);
