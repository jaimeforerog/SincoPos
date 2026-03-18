using POS.Application.DTOs;

namespace POS.Application.Services;

public interface ISugerenciasService
{
    /// <summary>
    /// Genera sugerencias de reabastecimiento para la sucursal usando:
    /// - StorePattern (Marten, Capa 9) → velocidad de productos
    /// - BusinessRadar (Marten, Capa 14) → días de actividad como denominador
    /// - Stock actual (EF Core) → días de stock restantes
    ///
    /// Retorna lista vacía si no hay suficientes datos (confidence &lt; 0.1).
    /// Nunca muestra una sugerencia sin respaldo de datos.
    /// </summary>
    Task<List<AutomaticActionDto>> ObtenerSugerenciasReabastecimientoAsync(int sucursalId);
}
