using System.Threading.Channels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using POS.Application.Services;

namespace POS.Infrastructure.Services;

/// <summary>
/// Servicio de background que procesa la emisión de facturas electrónicas
/// de forma asíncrona (fire-and-forget) sin bloquear el flujo de ventas.
/// </summary>
public sealed class FacturacionBackgroundService : BackgroundService
{
    private readonly Channel<int> _channel;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<FacturacionBackgroundService> _logger;

    // Delays de reintento: 1 min, 5 min, 15 min
    private static readonly TimeSpan[] RetryDelays =
    [
        TimeSpan.FromMinutes(1),
        TimeSpan.FromMinutes(5),
        TimeSpan.FromMinutes(15)
    ];

    public FacturacionBackgroundService(
        IServiceScopeFactory scopeFactory,
        ILogger<FacturacionBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _channel = Channel.CreateUnbounded<int>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false
        });
    }

    /// <summary>
    /// Encola un ventaId para facturación asíncrona (fire-and-forget).
    /// </summary>
    public void Encolar(int ventaId)
    {
        if (!_channel.Writer.TryWrite(ventaId))
            _logger.LogWarning("No se pudo encolar ventaId={VentaId} para facturación", ventaId);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("FacturacionBackgroundService iniciado");

        try
        {
            await foreach (var ventaId in _channel.Reader.ReadAllAsync(stoppingToken))
            {
                await ProcesarConReintentos(ventaId, stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("FacturacionBackgroundService detenido");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fatal en FacturacionBackgroundService");
        }
    }

    private async Task ProcesarConReintentos(int ventaId, CancellationToken ct)
    {
        for (int intento = 0; intento <= RetryDelays.Length; intento++)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var facturacionService = scope.ServiceProvider.GetRequiredService<IFacturacionService>();

                var (doc, error) = await facturacionService.EmitirFacturaVentaAsync(ventaId);

                if (doc != null)
                {
                    _logger.LogInformation(
                        "Factura emitida automáticamente para VentaId={VentaId}: {NumeroCompleto} Estado={Estado}",
                        ventaId, doc.NumeroCompleto, doc.Estado);
                    return; // Éxito
                }

                if (error == "NOT_FOUND" || error?.Contains("no requiere") == true)
                {
                    _logger.LogDebug("VentaId={VentaId} no requiere factura: {Error}", ventaId, error);
                    return; // No reintentar
                }

                _logger.LogWarning("Error emitiendo factura para VentaId={VentaId} (intento {Intento}): {Error}",
                    ventaId, intento + 1, error);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Excepción emitiendo factura para VentaId={VentaId} (intento {Intento})",
                    ventaId, intento + 1);
            }

            // Si quedan reintentos, esperar antes de volver a intentar
            if (intento < RetryDelays.Length && !ct.IsCancellationRequested)
            {
                _logger.LogInformation(
                    "Reintentando factura VentaId={VentaId} en {Minutos} min",
                    ventaId, RetryDelays[intento].TotalMinutes);
                await Task.Delay(RetryDelays[intento], ct);
            }
        }

        _logger.LogError(
            "Factura VentaId={VentaId} falló después de {MaxIntentos} intentos. Requiere revisión manual.",
            ventaId, RetryDelays.Length + 1);
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _channel.Writer.TryComplete();
        await base.StopAsync(cancellationToken);
    }
}
