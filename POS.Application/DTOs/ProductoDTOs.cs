namespace POS.Application.DTOs;

public record CrearProductoDto(
    string CodigoBarras,
    string Nombre,
    string? Descripcion,
    int CategoriaId,
    decimal PrecioVenta,
    decimal PrecioCosto
);

public record ActualizarProductoDto(
    string Nombre,
    string? Descripcion,
    decimal PrecioVenta,
    decimal PrecioCosto
);

public record ProductoDto(
    Guid Id,
    string CodigoBarras,
    string Nombre,
    string? Descripcion,
    int CategoriaId,
    decimal PrecioVenta,
    decimal PrecioCosto,
    bool Activo,
    DateTime FechaCreacion
);
