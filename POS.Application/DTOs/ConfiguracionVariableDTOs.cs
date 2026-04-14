namespace POS.Application.DTOs;

public record CrearConfiguracionVariableDto(
    string Nombre,
    string Valor,
    string? Descripcion
);

public record ActualizarConfiguracionVariableDto(
    string Nombre,
    string Valor,
    string? Descripcion
);

public record ConfiguracionVariableDto(
    int Id,
    string Nombre,
    string Valor,
    string? Descripcion,
    bool Activo,
    DateTime FechaCreacion,
    int EmpresaId
);
