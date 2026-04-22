using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using POS.Application.DTOs;
using POS.Application.Services;
using POS.Infrastructure.Data;

namespace POS.Api.Controllers;

/// <summary>
/// Controller para consultar y analizar el registro de actividades del sistema
/// </summary>
[Authorize]
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]")]
public class ActivityLogsController : ControllerBase
{
    private readonly IActivityLogService _activityLogService;
    private readonly AppDbContext _context;
    private readonly ILogger<ActivityLogsController> _logger;

    public ActivityLogsController(
        IActivityLogService activityLogService,
        AppDbContext context,
        ILogger<ActivityLogsController> logger)
    {
        _activityLogService = activityLogService;
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Obtener logs con filtros y paginación
    /// </summary>
    /// <remarks>
    /// Permite filtrar por fecha, usuario, tipo de actividad, entidad, etc.
    ///
    /// Filtros disponibles:
    /// - fechaDesde/fechaHasta: Rango de fechas
    /// - usuarioEmail: Email del usuario
    /// - tipo: Tipo de actividad (Caja=1, Venta=2, Inventario=3, Usuario=4, etc.)
    /// - accion: Nombre de la acción específica
    /// - sucursalId: ID de la sucursal
    /// - tipoEntidad: Tipo de entidad (ej: "Venta", "Caja")
    /// - entidadId: ID de la entidad específica
    /// - exitosa: Filtrar por resultado (true/false)
    /// - pageNumber: Número de página (default: 1)
    /// - pageSize: Tamaño de página (default: 50, max: 100)
    /// </remarks>
    [HttpGet]
    [Authorize(Policy = "Supervisor")]
    public async Task<ActionResult<PaginatedResult<ActivityLogFullDto>>> GetActivities(
        [FromQuery] DateTime? fechaDesde = null,
        [FromQuery] DateTime? fechaHasta = null,
        [FromQuery] string? usuarioEmail = null,
        [FromQuery] TipoActividad? tipo = null,
        [FromQuery] string? accion = null,
        [FromQuery] int? sucursalId = null,
        [FromQuery] string? tipoEntidad = null,
        [FromQuery] string? entidadId = null,
        [FromQuery] bool? exitosa = null,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 50)
    {
        // Validar pageSize
        if (pageSize > 100)
            pageSize = 100;

        var filter = new ActivityLogFilterDto(
            fechaDesde,
            fechaHasta,
            usuarioEmail,
            tipo,
            accion,
            sucursalId,
            tipoEntidad,
            entidadId,
            exitosa,
            pageNumber,
            pageSize
        );

        var result = await _activityLogService.GetActivitiesAsync(filter);

        _logger.LogInformation(
            "Usuario {Email} consultó activity logs. Filtros: tipo={Tipo}, sucursal={Sucursal}. Resultados: {Count}/{Total}",
            User.Identity?.Name ?? "Unknown",
            tipo?.ToString() ?? "Todos",
            sucursalId?.ToString() ?? "Todas",
            result.Items.Count,
            result.TotalCount);

        return Ok(result);
    }

    /// <summary>
    /// Obtener historial de cambios de una entidad específica
    /// </summary>
    /// <remarks>
    /// Retorna todos los cambios registrados para una entidad específica,
    /// ordenados cronológicamente (más reciente primero).
    ///
    /// Ejemplos:
    /// - GET /api/activitylogs/entidad/Venta/123 → Historial de la venta #123
    /// - GET /api/activitylogs/entidad/Caja/5 → Historial de la caja #5
    /// - GET /api/activitylogs/entidad/Usuario/10 → Historial del usuario #10
    /// </remarks>
    [HttpGet("entidad/{tipoEntidad}/{entidadId}")]
    [Authorize(Policy = "Supervisor")]
    public async Task<ActionResult<List<CambioEntidadDto>>> GetEntityHistory(
        string tipoEntidad,
        string entidadId)
    {
        var history = await _activityLogService.GetEntityHistoryAsync(tipoEntidad, entidadId);

        _logger.LogInformation(
            "Usuario {Email} consultó historial de {TipoEntidad} {EntidadId}. Cambios encontrados: {Count}",
            User.Identity?.Name ?? "Unknown",
            tipoEntidad,
            entidadId,
            history.Count);

        return Ok(history);
    }

    /// <summary>
    /// Obtener métricas del dashboard
    /// </summary>
    /// <remarks>
    /// Retorna métricas agregadas de actividad para el período especificado:
    /// - Total de acciones realizadas
    /// - Acciones exitosas vs fallidas
    /// - Distribución por tipo de actividad
    /// - Actividades recientes
    ///
    /// Si no se especifican fechas, usa el día actual.
    /// </remarks>
    [HttpGet("dashboard")]
    [Authorize(Policy = "Supervisor")]
    public async Task<ActionResult<DashboardActivityDto>> GetDashboardMetrics(
        [FromQuery] DateTime? fechaDesde = null,
        [FromQuery] DateTime? fechaHasta = null,
        [FromQuery] int? sucursalId = null)
    {
        // Si no se especifican fechas, usar el día actual
        var desde = fechaDesde ?? DateTime.UtcNow.Date;
        var hasta = fechaHasta ?? DateTime.UtcNow.Date.AddDays(1).AddSeconds(-1);

        var metrics = await _activityLogService.GetDashboardMetricsAsync(desde, hasta, sucursalId);

        _logger.LogInformation(
            "Usuario {Email} consultó dashboard de actividades. Período: {Desde} a {Hasta}. Total acciones: {Total}",
            User.Identity?.Name ?? "Unknown",
            desde,
            hasta,
            metrics.TotalAcciones);

        return Ok(metrics);
    }

    /// <summary>
    /// Obtener estadísticas de actividad de un usuario
    /// </summary>
    /// <remarks>
    /// Retorna métricas de actividad para un usuario específico:
    /// - Total de acciones
    /// - Acciones exitosas vs fallidas
    /// - Distribución por tipo de actividad
    /// - Última actividad registrada
    ///
    /// Si no se especifican fechas, retorna todas las actividades del usuario.
    /// </remarks>
    [HttpGet("usuario/{usuarioEmail}/stats")]
    [Authorize(Policy = "Supervisor")]
    public async Task<ActionResult<EstadisticasUsuarioDto>> GetUserStats(
        string usuarioEmail,
        [FromQuery] DateTime? fechaDesde = null,
        [FromQuery] DateTime? fechaHasta = null)
    {
        var stats = await _activityLogService.GetUserStatsAsync(
            usuarioEmail,
            fechaDesde,
            fechaHasta);

        _logger.LogInformation(
            "Usuario {Email} consultó estadísticas de {UsuarioConsultado}. Total acciones: {Total}",
            User.Identity?.Name ?? "Unknown",
            usuarioEmail,
            stats.TotalAcciones);

        return Ok(stats);
    }

    /// <summary>
    /// Obtener tipos de actividad disponibles
    /// </summary>
    /// <remarks>
    /// Retorna la lista de tipos de actividad que se pueden filtrar.
    /// Útil para construir interfaces de usuario con filtros.
    /// </remarks>
    [HttpGet("tipos")]
    public ActionResult<Dictionary<int, string>> GetActivityTypes()
    {
        var types = Enum.GetValues(typeof(TipoActividad))
            .Cast<TipoActividad>()
            .ToDictionary(t => (int)t, t => t.ToString());

        return Ok(types);
    }

    /// <summary>
    /// Consultar logs archivados (histórico más allá de la retención activa)
    /// </summary>
    /// <remarks>
    /// Logs movidos desde activity_logs al archivo histórico. Sin FKs activas,
    /// por lo que UsuarioNombre y NombreSucursal siempre son null.
    /// Soporta los mismos filtros que GET /.
    /// </remarks>
    [HttpGet("archivo")]
    [Authorize(Policy = "Supervisor")]
    public async Task<ActionResult<PaginatedResult<ActivityLogFullDto>>> GetArchive(
        [FromQuery] DateTime? fechaDesde = null,
        [FromQuery] DateTime? fechaHasta = null,
        [FromQuery] string? usuarioEmail = null,
        [FromQuery] TipoActividad? tipo = null,
        [FromQuery] string? accion = null,
        [FromQuery] int? sucursalId = null,
        [FromQuery] string? tipoEntidad = null,
        [FromQuery] string? entidadId = null,
        [FromQuery] bool? exitosa = null,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 50)
    {
        if (pageSize > 100) pageSize = 100;

        var query = _context.ActivityLogsArchivo.AsNoTracking();

        if (fechaDesde.HasValue)   query = query.Where(l => l.FechaHora >= fechaDesde.Value);
        if (fechaHasta.HasValue)   query = query.Where(l => l.FechaHora <= fechaHasta.Value);
        if (!string.IsNullOrEmpty(usuarioEmail)) query = query.Where(l => l.UsuarioEmail == usuarioEmail);
        if (tipo.HasValue)         query = query.Where(l => l.Tipo == tipo.Value);
        if (!string.IsNullOrEmpty(accion))       query = query.Where(l => l.Accion.Contains(accion));
        if (sucursalId.HasValue)   query = query.Where(l => l.SucursalId == sucursalId.Value);
        if (!string.IsNullOrEmpty(tipoEntidad))  query = query.Where(l => l.TipoEntidad == tipoEntidad);
        if (!string.IsNullOrEmpty(entidadId))    query = query.Where(l => l.EntidadId == entidadId);
        if (exitosa.HasValue)      query = query.Where(l => l.Exitosa == exitosa.Value);

        var totalCount = await query.CountAsync();
        var items = await query
            .OrderByDescending(l => l.FechaHora)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(l => new ActivityLogFullDto(
                l.Id,
                l.UsuarioEmail,
                null,           // UsuarioNombre — sin FK
                l.UsuarioId,
                l.FechaHora,
                l.Accion,
                l.Tipo,
                l.Tipo.ToString(),
                l.SucursalId,
                null,           // NombreSucursal — sin FK
                l.IpAddress,
                l.UserAgent,
                l.TipoEntidad,
                l.EntidadId,
                l.EntidadNombre,
                l.Descripcion,
                l.DatosAnteriores,
                l.DatosNuevos,
                l.Metadatos,
                l.Exitosa,
                l.MensajeError))
            .ToListAsync();

        _logger.LogInformation(
            "Usuario {Email} consultó activity_logs_archivo. Resultados: {Count}/{Total}",
            User.Identity?.Name ?? "Unknown",
            items.Count,
            totalCount);

        var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);
        return Ok(new PaginatedResult<ActivityLogFullDto>(items, totalCount, pageNumber, pageSize, totalPages));
    }
}
