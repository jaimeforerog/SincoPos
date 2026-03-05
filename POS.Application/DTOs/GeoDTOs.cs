namespace POS.Application.DTOs;

public record PaisDto(
    string Iso2,
    string Nombre,
    string? NombreNativo,
    string? Emoji
);

public record CiudadDto(
    string Nombre,
    string? CodigoPais,
    double? Latitud,
    double? Longitud
);

public record DepartamentoDto(
    string Iso2,
    string Nombre,
    string CodigoPais
);
