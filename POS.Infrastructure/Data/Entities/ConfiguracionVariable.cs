namespace POS.Infrastructure.Data.Entities;

/// <summary>
/// Variable de configuración del sistema con clave/valor.
/// Permite parametrizar comportamientos del negocio sin recompilar.
/// Ejemplos: AperturaCaja_MontoMax, diasVencimientoAlerta, etc.
/// </summary>
public class ConfiguracionVariable : EntidadAuditable
{
    public int EmpresaId { get; set; }
    public Empresa Empresa { get; set; } = null!;

    /// <summary>
    /// Clave única de la variable. Usar snake_case. Ej: AperturaCaja_MontoMax
    /// </summary>
    public string Nombre { get; set; } = string.Empty;

    /// <summary>
    /// Valor almacenado como string. El consumidor es responsable de parsear el tipo.
    /// </summary>
    public string Valor { get; set; } = string.Empty;

    /// <summary>
    /// Descripción legible del propósito y valores válidos de la variable.
    /// </summary>
    public string? Descripcion { get; set; }
}
