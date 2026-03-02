using System.Text.Json;
using System.Threading.Channels;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using POS.Application.DTOs;
using POS.Application.Services;
using POS.Infrastructure.Data;
using POS.Infrastructure.Data.Entities;
using System.Security.Claims;

namespace POS.Infrastructure.Services;

/// <summary>
/// Implementación del servicio de Activity Log con Channel-based background processing.
/// Usa un patrón fire-and-forget para no bloquear operaciones de usuario.
/// </summary>
public class ActivityLogService : IActivityLogService
{
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<ActivityLogService> _logger;
    private readonly Channel<ActivityLog> _channel;
    private readonly ActivityLogBackgroundProcessor _backgroundProcessor;

    public ActivityLogService(
        IServiceScopeFactory serviceScopeFactory,
        IHttpContextAccessor httpContextAccessor,
        ILogger<ActivityLogService> logger,
        IHostApplicationLifetime applicationLifetime)
    {
        _serviceScopeFactory = serviceScopeFactory;
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;

        // Canal sin límite para no perder logs
        _channel = Channel.CreateUnbounded<ActivityLog>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false
        });

        // Background processor que consume logs del canal
        _backgroundProcessor = new ActivityLogBackgroundProcessor(
            _channel.Reader,
            _serviceScopeFactory,
            logger);

        // Iniciar procesamiento en background
        _ = _backgroundProcessor.ProcessLogsAsync(applicationLifetime.ApplicationStopping);
    }

    /// <summary>
    /// Registra una actividad de forma asíncrona (fire-and-forget).
    /// Si el canal está lleno o hay error, loguea pero NO falla.
    /// </summary>
    public async Task LogActivityAsync(ActivityLogDto activity)
    {
        try
        {
            var httpContext = _httpContextAccessor.HttpContext;
            var usuarioEmail = ObtenerUsuarioActual(httpContext);
            var usuarioId = ObtenerUsuarioId(httpContext);

            var log = new ActivityLog
            {
                UsuarioEmail = usuarioEmail,
                UsuarioId = usuarioId,
                FechaHora = DateTime.UtcNow,
                Accion = activity.Accion,
                Tipo = activity.Tipo,
                SucursalId = activity.SucursalId,
                IpAddress = activity.IpAddress ?? httpContext?.Connection?.RemoteIpAddress?.ToString(),
                UserAgent = activity.UserAgent ?? httpContext?.Request?.Headers["User-Agent"].ToString(),
                TipoEntidad = activity.TipoEntidad,
                EntidadId = activity.EntidadId,
                EntidadNombre = activity.EntidadNombre,
                Descripcion = activity.Descripcion,
                DatosAnteriores = activity.DatosAnteriores != null
                    ? JsonSerializer.Serialize(activity.DatosAnteriores)
                    : null,
                DatosNuevos = activity.DatosNuevos != null
                    ? JsonSerializer.Serialize(activity.DatosNuevos)
                    : null,
                Metadatos = activity.Metadatos != null
                    ? JsonSerializer.Serialize(activity.Metadatos)
                    : null,
                Exitosa = activity.Exitosa,
                MensajeError = activity.MensajeError
            };

            // Encolar log para procesamiento asíncrono (fire-and-forget)
            await _channel.Writer.WriteAsync(log);
        }
        catch (Exception ex)
        {
            // NUNCA fallar la operación principal por error en logging
            _logger.LogError(ex, "Error encolando activity log para {Accion}", activity.Accion);
        }
    }

    /// <summary>
    /// Obtiene actividades con filtros y paginación
    /// </summary>
    public async Task<PaginatedResult<ActivityLogFullDto>> GetActivitiesAsync(ActivityLogFilterDto filter)
    {
        using var scope = _serviceScopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var query = context.ActivityLogs
            .Include(a => a.Sucursal)
            .AsQueryable();

        // Aplicar filtros
        if (filter.FechaDesde.HasValue)
            query = query.Where(a => a.FechaHora >= filter.FechaDesde.Value);

        if (filter.FechaHasta.HasValue)
            query = query.Where(a => a.FechaHora <= filter.FechaHasta.Value);

        if (!string.IsNullOrEmpty(filter.UsuarioEmail))
            query = query.Where(a => a.UsuarioEmail.Contains(filter.UsuarioEmail));

        if (filter.Tipo.HasValue)
            query = query.Where(a => a.Tipo == filter.Tipo.Value);

        if (!string.IsNullOrEmpty(filter.Accion))
            query = query.Where(a => a.Accion.Contains(filter.Accion));

        if (filter.SucursalId.HasValue)
            query = query.Where(a => a.SucursalId == filter.SucursalId.Value);

        if (!string.IsNullOrEmpty(filter.TipoEntidad))
            query = query.Where(a => a.TipoEntidad == filter.TipoEntidad);

        if (!string.IsNullOrEmpty(filter.EntidadId))
            query = query.Where(a => a.EntidadId == filter.EntidadId);

        if (filter.Exitosa.HasValue)
            query = query.Where(a => a.Exitosa == filter.Exitosa.Value);

        // Contar total antes de paginar
        var totalCount = await query.CountAsync();

        // Ordenar y paginar
        var items = await query
            .OrderByDescending(a => a.FechaHora)
            .Skip((filter.PageNumber - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .Select(a => new ActivityLogFullDto(
                a.Id,
                a.UsuarioEmail,
                a.UsuarioId,
                a.FechaHora,
                a.Accion,
                a.Tipo,
                a.Tipo.ToString(),
                a.SucursalId,
                a.Sucursal != null ? a.Sucursal.Nombre : null,
                a.IpAddress,
                a.UserAgent,
                a.TipoEntidad,
                a.EntidadId,
                a.EntidadNombre,
                a.Descripcion,
                a.DatosAnteriores,
                a.DatosNuevos,
                a.Metadatos,
                a.Exitosa,
                a.MensajeError
            ))
            .ToListAsync();

        var totalPages = (int)Math.Ceiling(totalCount / (double)filter.PageSize);

        return new PaginatedResult<ActivityLogFullDto>(
            items,
            totalCount,
            filter.PageNumber,
            filter.PageSize,
            totalPages
        );
    }

    /// <summary>
    /// Obtiene el historial de cambios de una entidad específica
    /// </summary>
    public async Task<List<CambioEntidadDto>> GetEntityHistoryAsync(string tipoEntidad, string entidadId)
    {
        using var scope = _serviceScopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        return await context.ActivityLogs
            .Where(a => a.TipoEntidad == tipoEntidad && a.EntidadId == entidadId)
            .OrderByDescending(a => a.FechaHora)
            .Select(a => new CambioEntidadDto(
                a.Id,
                a.FechaHora,
                a.UsuarioEmail,
                a.Accion,
                a.Descripcion,
                a.DatosAnteriores,
                a.DatosNuevos,
                a.Exitosa
            ))
            .ToListAsync();
    }

    /// <summary>
    /// Obtiene métricas para el dashboard
    /// </summary>
    public async Task<DashboardActivityDto> GetDashboardMetricsAsync(
        DateTime fechaDesde,
        DateTime fechaHasta,
        int? sucursalId = null)
    {
        using var scope = _serviceScopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var query = context.ActivityLogs
            .Where(a => a.FechaHora >= fechaDesde && a.FechaHora <= fechaHasta);

        if (sucursalId.HasValue)
            query = query.Where(a => a.SucursalId == sucursalId.Value);

        var logs = await query.ToListAsync();

        var totalAcciones = logs.Count;
        var accionesExitosas = logs.Count(a => a.Exitosa);
        var accionesFallidas = logs.Count(a => !a.Exitosa);

        var accionesPorTipo = logs
            .GroupBy(a => a.Tipo.ToString())
            .ToDictionary(g => g.Key, g => g.Count());

        var actividadesRecientes = await context.ActivityLogs
            .Where(a => a.FechaHora >= fechaDesde && a.FechaHora <= fechaHasta)
            .OrderByDescending(a => a.FechaHora)
            .Take(10)
            .Select(a => new ActividadRecienteDto(
                a.Id,
                a.UsuarioEmail,
                a.FechaHora,
                a.Accion,
                a.Tipo.ToString(),
                a.Descripcion,
                a.Exitosa
            ))
            .ToListAsync();

        return new DashboardActivityDto(
            DateTime.UtcNow,
            totalAcciones,
            accionesExitosas,
            accionesFallidas,
            accionesPorTipo,
            actividadesRecientes
        );
    }

    /// <summary>
    /// Obtiene estadísticas de un usuario
    /// </summary>
    public async Task<EstadisticasUsuarioDto> GetUserStatsAsync(
        string usuarioEmail,
        DateTime? fechaDesde = null,
        DateTime? fechaHasta = null)
    {
        using var scope = _serviceScopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var query = context.ActivityLogs
            .Where(a => a.UsuarioEmail == usuarioEmail);

        if (fechaDesde.HasValue)
            query = query.Where(a => a.FechaHora >= fechaDesde.Value);

        if (fechaHasta.HasValue)
            query = query.Where(a => a.FechaHora <= fechaHasta.Value);

        var logs = await query.ToListAsync();

        var totalAcciones = logs.Count;
        var accionesExitosas = logs.Count(a => a.Exitosa);
        var accionesFallidas = logs.Count(a => !a.Exitosa);

        var accionesPorTipo = logs
            .GroupBy(a => a.Tipo.ToString())
            .ToDictionary(g => g.Key, g => g.Count());

        var ultimaActividad = logs.Any()
            ? logs.Max(a => a.FechaHora)
            : (DateTime?)null;

        return new EstadisticasUsuarioDto(
            usuarioEmail,
            totalAcciones,
            accionesExitosas,
            accionesFallidas,
            accionesPorTipo,
            ultimaActividad
        );
    }

    // ========== HELPERS ==========

    private string ObtenerUsuarioActual(HttpContext? httpContext)
    {
        if (httpContext?.User?.Identity?.IsAuthenticated == true)
        {
            var email = httpContext.User.FindFirst(ClaimTypes.Email)?.Value
                       ?? httpContext.User.FindFirst("email")?.Value
                       ?? httpContext.User.FindFirst(ClaimTypes.Name)?.Value
                       ?? httpContext.User.FindFirst("preferred_username")?.Value;

            return email ?? "usuario-autenticado";
        }

        return "sistema";
    }

    private int? ObtenerUsuarioId(HttpContext? httpContext)
    {
        if (httpContext?.User?.Identity?.IsAuthenticated == true)
        {
            var userIdClaim = httpContext.User.FindFirst("user_id")?.Value
                             ?? httpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (int.TryParse(userIdClaim, out var userId))
                return userId;
        }

        return null;
    }
}

/// <summary>
/// Procesador en background que consume logs del canal y los persiste en BD
/// </summary>
internal class ActivityLogBackgroundProcessor
{
    private readonly ChannelReader<ActivityLog> _reader;
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly ILogger _logger;

    public ActivityLogBackgroundProcessor(
        ChannelReader<ActivityLog> reader,
        IServiceScopeFactory serviceScopeFactory,
        ILogger logger)
    {
        _reader = reader;
        _serviceScopeFactory = serviceScopeFactory;
        _logger = logger;
    }

    /// <summary>
    /// Procesa logs del canal en background
    /// </summary>
    public async Task ProcessLogsAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("ActivityLog background processor iniciado");

        try
        {
            await foreach (var log in _reader.ReadAllAsync(cancellationToken))
            {
                try
                {
                    using var scope = _serviceScopeFactory.CreateScope();
                    var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                    context.ActivityLogs.Add(log);
                    await context.SaveChangesAsync(cancellationToken);

                    _logger.LogDebug("Activity log guardado: {Accion} por {Usuario}",
                        log.Accion, log.UsuarioEmail);
                }
                catch (Exception ex)
                {
                    // Loguear error pero continuar procesando otros logs
                    _logger.LogError(ex, "Error persistiendo activity log: {Accion}", log.Accion);
                }
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("ActivityLog background processor detenido");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fatal en ActivityLog background processor");
        }
    }
}
