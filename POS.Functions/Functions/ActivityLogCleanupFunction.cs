using Microsoft.Azure.Functions.Worker;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using POS.Infrastructure.Data;

namespace POS.Functions.Functions;

public class ActivityLogCleanupFunction
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ActivityLogCleanupFunction> _logger;
    private readonly ActivityLogCleanupOptions _options;

    public ActivityLogCleanupFunction(
        IServiceScopeFactory scopeFactory,
        ILogger<ActivityLogCleanupFunction> logger,
        IOptions<ActivityLogCleanupOptions> options)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _options = options.Value;
    }

    // Domingos a las 2:00 AM UTC
    [Function("ActivityLogCleanup")]
    public async Task Run([TimerTrigger("0 0 2 * * 0")] TimerInfo timer)
    {
        var hotCutoff     = DateTime.UtcNow.AddDays(-_options.RetentionDays);
        var archiveCutoff = DateTime.UtcNow.AddDays(-_options.ArchiveRetentionDays);

        _logger.LogInformation(
            "ActivityLogCleanup iniciado. Hot cutoff: {Hot:yyyy-MM-dd} ({HotDays}d) | " +
            "Archive cutoff: {Archive:yyyy-MM-dd} ({ArchiveDays}d).",
            hotCutoff, _options.RetentionDays,
            archiveCutoff, _options.ArchiveRetentionDays);

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // ── Fase 1: mover activity_logs → activity_logs_archivo ──────────────
        var totalMovidos = await ArchivarLogsAsync(db, hotCutoff);

        // ── Fase 2: purgar activity_logs_archivo vencidos ────────────────────
        var totalPurgados = await PurgarArchivoAsync(db, archiveCutoff);

        _logger.LogInformation(
            "ActivityLogCleanup completado. Movidos a archivo: {Movidos} | Purgados de archivo: {Purgados}.",
            totalMovidos, totalPurgados);
    }

    // ── Fase 1 ───────────────────────────────────────────────────────────────

    private async Task<int> ArchivarLogsAsync(AppDbContext db, DateTime hotCutoff)
    {
        var totalMovidos = 0;

        while (true)
        {
            // Seleccionar IDs del próximo batch a archivar
            var ids = await db.ActivityLogs
                .Where(l => l.FechaHora < hotCutoff)
                .OrderBy(l => l.Id)
                .Take(_options.BatchSize)
                .Select(l => l.Id)
                .ToListAsync();

            if (ids.Count == 0) break;

            var idsArray = ids.ToArray();

            // INSERT … SELECT directo en PostgreSQL — sin cargar entidades en memoria
            await db.Database.ExecuteSqlAsync($@"
                INSERT INTO activity_logs_archivo (
                    id, usuario_email, usuario_id, fecha_hora, accion, tipo, sucursal_id,
                    ip_address, user_agent, tipo_entidad, entidad_id, entidad_nombre,
                    descripcion, datos_anteriores, datos_nuevos, metadatos, exitosa, mensaje_error,
                    fecha_archivado
                )
                SELECT
                    id, usuario_email, usuario_id, fecha_hora, accion, tipo, sucursal_id,
                    ip_address, user_agent, tipo_entidad, entidad_id, entidad_nombre,
                    descripcion, datos_anteriores, datos_nuevos, metadatos, exitosa, mensaje_error,
                    NOW()
                FROM activity_logs
                WHERE id = ANY({idsArray})
                ON CONFLICT (id) DO NOTHING
            ");

            // Borrar del hot table solo los que fueron archivados
            await db.ActivityLogs
                .Where(l => ids.Contains(l.Id))
                .ExecuteDeleteAsync();

            totalMovidos += ids.Count;

            _logger.LogDebug("Batch archivado: {Count} registros (total: {Total}).",
                ids.Count, totalMovidos);

            if (ids.Count < _options.BatchSize) break;
        }

        if (totalMovidos > 0)
            _logger.LogInformation("Fase 1 completada: {Total} registros movidos a archivo.", totalMovidos);
        else
            _logger.LogInformation("Fase 1: sin registros que archivar.");

        return totalMovidos;
    }

    // ── Fase 2 ───────────────────────────────────────────────────────────────

    private async Task<int> PurgarArchivoAsync(AppDbContext db, DateTime archiveCutoff)
    {
        var totalPurgados = 0;
        int purgados;

        do
        {
            purgados = await db.ActivityLogsArchivo
                .Where(l => l.FechaHora < archiveCutoff)
                .OrderBy(l => l.Id)
                .Take(_options.BatchSize)
                .ExecuteDeleteAsync();

            totalPurgados += purgados;

            if (purgados > 0)
                _logger.LogDebug("Batch purgado del archivo: {Count} registros (total: {Total}).",
                    purgados, totalPurgados);

        } while (purgados == _options.BatchSize);

        if (totalPurgados > 0)
            _logger.LogInformation("Fase 2 completada: {Total} registros eliminados del archivo.", totalPurgados);
        else
            _logger.LogInformation("Fase 2: sin registros que purgar del archivo.");

        return totalPurgados;
    }
}

public class ActivityLogCleanupOptions
{
    public const string SectionName = "ActivityLogCleanup";

    /// <summary>
    /// Días que un log permanece en activity_logs (tabla hot).
    /// Al superarlos, se mueve a activity_logs_archivo. Default: 180 (6 meses).
    /// </summary>
    public int RetentionDays { get; set; } = 180;

    /// <summary>
    /// Días que un log permanece en activity_logs_archivo antes de borrarse definitivamente.
    /// Default: 730 (2 años).
    /// </summary>
    public int ArchiveRetentionDays { get; set; } = 730;

    /// <summary>Registros procesados por operación batch. Default: 1000.</summary>
    public int BatchSize { get; set; } = 1000;
}
