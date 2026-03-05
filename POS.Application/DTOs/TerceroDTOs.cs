namespace POS.Application.DTOs;

public class CrearTerceroDto
{
    public string TipoIdentificacion { get; set; } = string.Empty;
    public string Identificacion { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string TipoTercero { get; set; } = string.Empty;
    public string? Telefono { get; set; }
    public string? Email { get; set; }
    public string? Direccion { get; set; }
    public string? Ciudad { get; set; }
    public string? CodigoDepartamento { get; set; }
    public string? CodigoMunicipio { get; set; }
    public string? PerfilTributario { get; set; }
    public bool EsGranContribuyente { get; set; } = false;
    public bool EsAutorretenedor { get; set; } = false;
    public bool EsResponsableIVA { get; set; } = false;
}

public class ActualizarTerceroDto
{
    public string Nombre { get; set; } = string.Empty;
    public string? TipoTercero { get; set; }
    public string? Telefono { get; set; }
    public string? Email { get; set; }
    public string? Direccion { get; set; }
    public string? Ciudad { get; set; }
    public string? CodigoDepartamento { get; set; }
    public string? CodigoMunicipio { get; set; }
    public string? PerfilTributario { get; set; }
    public bool EsGranContribuyente { get; set; } = false;
    public bool EsAutorretenedor { get; set; } = false;
    public bool EsResponsableIVA { get; set; } = false;
}

public record TerceroDto(
    int Id,
    string TipoIdentificacion,
    string Identificacion,
    string? DigitoVerificacion,
    string Nombre,
    string TipoTercero,
    string? Telefono,
    string? Email,
    string? Direccion,
    string? Ciudad,
    string? CodigoDepartamento,
    string? CodigoMunicipio,
    string PerfilTributario,
    bool EsGranContribuyente,
    bool EsAutorretenedor,
    bool EsResponsableIVA,
    string OrigenDatos,
    string? ExternalId,
    bool Activo,
    List<TerceroActividadDto> Actividades
);

public record TerceroActividadDto(
    int Id,
    string CodigoCIIU,
    string Descripcion,
    bool EsPrincipal
);

public class AgregarActividadDto
{
    public string CodigoCIIU { get; set; } = string.Empty;
    public string Descripcion { get; set; } = string.Empty;
    public bool EsPrincipal { get; set; } = false;
}

// ── Importación Excel ─────────────────────────────────────────────────────────

public class ResultadoImportacionTercerosDto
{
    public int TotalFilas { get; set; }
    public int Importados { get; set; }
    public int Omitidos { get; set; }
    public int Errores { get; set; }
    public List<ResultadoFilaTerceroDto> Filas { get; set; } = new();
}

public class ResultadoFilaTerceroDto
{
    public int Fila { get; set; }
    public string? Identificacion { get; set; }
    public string? Nombre { get; set; }
    public string Estado { get; set; } = string.Empty;  // "Importado" | "Omitido" | "Error"
    public string? Mensaje { get; set; }
}
