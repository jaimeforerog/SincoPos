namespace POS.Application.DTOs;

public record CrearCategoriaDto(
    string Nombre,
    string? Descripcion,
    int? CategoriaPadreId = null,
    decimal MargenGanancia = 0.30m
);

public record ActualizarCategoriaDto(
    string Nombre,
    string? Descripcion,
    int? CategoriaPadreId = null,
    decimal MargenGanancia = 0.30m
);

public record CategoriaDto(
    int Id,
    string Nombre,
    string? Descripcion,
    bool Activa,
    int? CategoriaPadreId,
    string? NombrePadre,
    int Nivel,
    string RutaCompleta,
    int CantidadSubCategorias,
    int CantidadProductos,
    decimal MargenGanancia = 0.30m
);

public record CategoriaArbolDto(
    int Id,
    string Nombre,
    string? Descripcion,
    bool Activa,
    int? CategoriaPadreId,
    int Nivel,
    string RutaCompleta,
    int CantidadProductos,
    decimal MargenGanancia,
    List<CategoriaArbolDto> SubCategorias
);

public record MoverCategoriaDto(
    int CategoriaId,
    int? NuevaCategoriaPadreId
);
