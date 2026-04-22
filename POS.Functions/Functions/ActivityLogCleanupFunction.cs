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
        var cutoff = DateTime.UtcNow.AddDays(-_options.RetentionDays);

        _logger.LogInformation(
            "ActivityLogCleanup iniciado. Eliminando logs anteriores a {Cutoff:yyyy-MM-dd} (retención: {Days} días).",
            cutoff, _options.RetentionDays);

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var totalEliminados = 0;
        int eliminados;

        // Borrado en batches para no bloquear la tabla en sistemas con alto volumen
        do
        {
            eliminados = await db.ActivityLogs
                .Where(l => l.FechaHora < cutoff)
                .OrderBy(l => l.Id)
                .Take(_options.BatchSize)
                .ExecuteDeleteAsync();

            totalEliminados += eliminados;

            if (eliminados > 0)
                _logger.LogDebug("Batch eliminado: {Count} registros (total acumulado: {Total}).",
                    eliminados, totalEliminados);

        } while (eliminados == _options.BatchSize);

        if (totalEliminados == 0)
            _logger.LogInformation("ActivityLogCleanup: sin registros que eliminar.");
        else
            _logger.LogInformation(
                "ActivityLogCleanup completado. {Total} registros eliminados.", totalEliminados);
    }
}

public class ActivityLogCleanupOptions
{
    public const string SectionName = "ActivityLogCleanup";

    /// <summary>Días de retención. Logs más antiguos serán eliminados. Default: 180 (6 meses).</summary>
    public int RetentionDays { get; set; } = 180;

    /// <summary>Registros eliminados por batch para no bloquear la tabla. Default: 1000.</summary>
    public int BatchSize { get; set; } = 1000;
}
