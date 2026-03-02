using POS.Application.DTOs;

namespace POS.Application.Services;

/// <summary>
/// Servicio para registrar y consultar actividades del sistema.
/// Complementa la auditoría automática registrando acciones de negocio específicas.
/// </summary>
public interface IActivityLogService
{
    /// <summary>
    /// Registra una actividad de forma asíncrona (fire-and-forget).
    /// Nunca falla la operación principal, incluso si el logging falla.
    /// </summary>
    /// <param name="activity">Datos de la actividad a registrar</param>
    Task LogActivityAsync(ActivityLogDto activity);

    /// <summary>
    /// Obtiene actividades con filtros y paginación
    /// </summary>
    /// <param name="filter">Criterios de filtrado</param>
    /// <returns>Resultado paginado de actividades</returns>
    Task<PaginatedResult<ActivityLogFullDto>> GetActivitiesAsync(ActivityLogFilterDto filter);

    /// <summary>
    /// Obtiene el historial completo de una entidad específica
    /// </summary>
    /// <param name="tipoEntidad">Tipo de entidad (ej: "Venta", "Caja")</param>
    /// <param name="entidadId">ID de la entidad</param>
    /// <returns>Lista de cambios ordenados cronológicamente</returns>
    Task<List<CambioEntidadDto>> GetEntityHistoryAsync(string tipoEntidad, string entidadId);

    /// <summary>
    /// Obtiene métricas para el dashboard
    /// </summary>
    /// <param name="fechaDesde">Fecha inicial</param>
    /// <param name="fechaHasta">Fecha final</param>
    /// <param name="sucursalId">Filtrar por sucursal (opcional)</param>
    /// <returns>Métricas agregadas</returns>
    Task<DashboardActivityDto> GetDashboardMetricsAsync(
        DateTime fechaDesde,
        DateTime fechaHasta,
        int? sucursalId = null);

    /// <summary>
    /// Obtiene estadísticas de actividad por usuario
    /// </summary>
    /// <param name="usuarioEmail">Email del usuario</param>
    /// <param name="fechaDesde">Fecha inicial (opcional)</param>
    /// <param name="fechaHasta">Fecha final (opcional)</param>
    /// <returns>Estadísticas del usuario</returns>
    Task<EstadisticasUsuarioDto> GetUserStatsAsync(
        string usuarioEmail,
        DateTime? fechaDesde = null,
        DateTime? fechaHasta = null);
}
