namespace POS.Infrastructure.Data.Entities;

public class Tercero : EntidadAuditable
{
    /// <summary>
    /// Empresa propietaria del tercero (cliente/proveedor). Null = catálogo global (legado).
    /// </summary>
    public int? EmpresaId { get; set; }

    public TipoIdentificacion TipoIdentificacion { get; set; }
    public string Identificacion { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public TipoTercero TipoTercero { get; set; }
    public string? Telefono { get; set; }
    public string? Email { get; set; }
    public string? Direccion { get; set; }
    public string? Ciudad { get; set; }
    public OrigenDatos OrigenDatos { get; set; } = OrigenDatos.Local;
    public string? ExternalId { get; set; }

    /// <summary>
    /// Perfil tributario del tercero (comprador/proveedor).
    /// El Tax Engine usa este valor para evaluar la matriz de retenciones.
    /// Valores: GRAN_CONTRIBUYENTE | REGIMEN_COMUN | REGIMEN_SIMPLE | PERSONA_NATURAL
    /// </summary>
    public string PerfilTributario { get; set; } = "REGIMEN_COMUN";

    // ── Campos fiscales enriquecidos ──────────────────────────────────────────
    /// <summary>Dígito de Verificación (módulo 11 DIAN). Solo NIT.</summary>
    public string? DigitoVerificacion { get; set; }

    public string? CodigoDepartamento { get; set; }  // "11" = Bogotá
    public string? CodigoMunicipio { get; set; }     // "11001" = Bogotá DC
    public bool EsGranContribuyente { get; set; } = false;
    public bool EsAutorretenedor { get; set; } = false;
    public bool EsResponsableIVA { get; set; } = false;

    public ICollection<TerceroActividad> Actividades { get; set; } = new List<TerceroActividad>();
}

public class TerceroActividad
{
    public int Id { get; set; }
    public int TerceroId { get; set; }
    public string CodigoCIIU { get; set; } = string.Empty;
    public string Descripcion { get; set; } = string.Empty;
    public bool EsPrincipal { get; set; } = false;
    public Tercero Tercero { get; set; } = null!;
}

public enum TipoIdentificacion
{
    CC = 0,          // Cedula de Ciudadania
    NIT = 1,         // Numero de Identificacion Tributaria
    CE = 2,          // Cedula de Extranjeria
    Pasaporte = 3,
    TI = 4,          // Tarjeta de Identidad
    Otro = 5
}

public enum TipoTercero
{
    Cliente = 0,
    Proveedor = 1,
    Ambos = 2
}

public enum OrigenDatos
{
    Local = 0,
    ERP = 1
}
