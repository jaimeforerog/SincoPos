using POS.Domain;

namespace POS.Infrastructure.Data.Entities;

/// <summary>
/// Clase base para todas las entidades que requieren auditoría.
/// Registra automáticamente quién y cuándo se creó/modificó cada registro.
/// </summary>
public abstract class EntidadAuditable : ISoftDelete
{
    public int Id { get; set; }

    /// <summary>
    /// Email del usuario que creó el registro
    /// </summary>
    public string CreadoPor { get; set; } = string.Empty;

    /// <summary>
    /// Fecha y hora de creación (UTC)
    /// </summary>
    public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Email del usuario que modificó el registro por última vez
    /// </summary>
    public string? ModificadoPor { get; set; }

    /// <summary>
    /// Fecha y hora de última modificación (UTC)
    /// </summary>
    public DateTime? FechaModificacion { get; set; }

    /// <summary>
    /// Indica si el registro está activo (soft delete)
    /// </summary>
    public bool Activo { get; set; } = true;

    /// <summary>
    /// Fecha en que fue desactivado el registro. null = activo.
    /// </summary>
    public DateTime? FechaDesactivacion { get; set; }
}
