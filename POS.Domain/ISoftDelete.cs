namespace POS.Domain;

/// <summary>
/// Patrón de desactivación lógica (soft-delete) unificado.
/// Las entidades que implementen esta interfaz pueden ser filtradas
/// automáticamente via EF Core global query filters.
/// </summary>
public interface ISoftDelete
{
    bool Activo { get; set; }
    DateTime? FechaDesactivacion { get; set; }
}
