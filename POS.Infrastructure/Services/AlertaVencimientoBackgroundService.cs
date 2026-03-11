using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using POS.Application.Services;

namespace POS.Infrastructure.Services;

/// <summary>
/// Servicio que se ejecuta una vez al día y emite notificaciones SignalR
/// para los lotes que estén próximos a vencer en cada sucursal.
/// Usa la configuración DiasAlertaVencimientoLotes de cada sucursal.
/// </summary>
public class AlertaVencimientoBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<AlertaVencimientoBackgroundService> _logger;

    // Ejecutar una vez al día a las 7:00 AM (hora Colombia)
    private static readonly TimeOnly HoraEjecucion = new(7, 0, 0);

    public AlertaVencimientoBackgroundService(
        IServiceScopeFactory scopeFactory,
        ILogger<AlertaVencimientoBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("AlertaVencimientoBackgroundService iniciado.");

        while (!stoppingToken.IsCancellationRequested)
        {
            var delay = CalcularDelayHastaProximaEjecucion();
            _logger.LogInformation("Próxima verificación de vencimientos en {Delay:hh\\:mm\\:ss}.", delay);

            await Task.Delay(delay, stoppingToken);

            if (stoppingToken.IsCancellationRequested)
                break;

            try
            {
                await VerificarVencimientosAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error verificando alertas de vencimiento de lotes.");
            }
        }
    }

    private async Task VerificarVencimientosAsync(CancellationToken stoppingToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var loteService = scope.ServiceProvider.GetRequiredService<ILoteService>();
        var notificationService = scope.ServiceProvider.GetRequiredService<INotificationService>();

        var alertas = await loteService.ObtenerTodasLasAlertasAsync();

        if (!alertas.Any())
        {
            _logger.LogInformation("Sin alertas de vencimiento de lotes para hoy.");
            return;
        }

        _logger.LogInformation("Se encontraron {Total} lotes próximos a vencer. Notificando...", alertas.Count);

        // Agrupar por sucursal para enviar una sola notificación por sucursal
        var porSucursal = alertas.GroupBy(a => a.SucursalId);

        foreach (var grupo in porSucursal)
        {
            var sucursalId = grupo.Key;
            var sucursalNombre = grupo.First().NombreSucursal;
            var count = grupo.Count();
            var proximos = grupo
                .OrderBy(a => a.DiasParaVencer)
                .Take(3)
                .Select(a => $"{a.NombreProducto} ({a.DiasParaVencer}d)")
                .ToList();

            var vencidosHoy = grupo.Count(a => a.DiasParaVencer <= 0);
            var nivel = vencidosHoy > 0 ? "error" : (grupo.Any(a => a.DiasParaVencer <= 7) ? "warning" : "info");
            var titulo = vencidosHoy > 0
                ? $"{vencidosHoy} lote(s) vencidos en {sucursalNombre}"
                : $"{count} lote(s) próximos a vencer en {sucursalNombre}";

            var mensaje = string.Join(", ", proximos);
            if (count > 3) mensaje += $" y {count - 3} más";

            await notificationService.EnviarNotificacionSucursalAsync(sucursalId, new NotificacionDto(
                Tipo: "lote_por_vencer",
                Titulo: titulo,
                Mensaje: mensaje,
                Nivel: nivel,
                Timestamp: DateTime.UtcNow,
                Datos: new { sucursalId, totalLotes = count, alertas = grupo.Select(a => new { a.LoteId, a.NombreProducto, a.NumeroLote, a.FechaVencimiento, a.DiasParaVencer, a.CantidadDisponible }) }
            ));

            _logger.LogInformation("Notificación enviada a sucursal {SucursalId}: {Count} lotes.", sucursalId, count);
        }
    }

    private static TimeSpan CalcularDelayHastaProximaEjecucion()
    {
        var tzId = OperatingSystem.IsWindows() ? "SA Pacific Standard Time" : "America/Bogota";
        var tz = TimeZoneInfo.FindSystemTimeZoneById(tzId);
        var ahora = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz);
        var hoyEjecucion = ahora.Date.Add(HoraEjecucion.ToTimeSpan());

        // Si ya pasó la hora de hoy, programar para mañana
        var proxima = ahora >= hoyEjecucion
            ? hoyEjecucion.AddDays(1)
            : hoyEjecucion;

        return proxima - ahora;
    }
}
