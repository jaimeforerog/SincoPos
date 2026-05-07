using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.SignalRService;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using POS.Application.Services;

namespace POS.Functions.Functions;

/// <summary>
/// Timer-triggered function (cada 30s) que delega el procesamiento del Outbox
/// al servicio inyectable IErpOutboxProcessor. Se mantiene fina para que la
/// lógica sea testeable sin levantar el host de Functions.
/// </summary>
public class ErpSyncFunction
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ErpSyncFunction> _logger;

    public ErpSyncFunction(
        IServiceScopeFactory scopeFactory,
        ILogger<ErpSyncFunction> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    [Function("ErpSync")]
    public async Task<ErpSyncOutput> Run(
        [TimerTrigger("*/30 * * * * *")] TimerInfo timer)
    {
        _logger.LogInformation("ErpSyncFunction iniciado en {Timestamp}", DateTime.UtcNow);

        using var scope = _scopeFactory.CreateScope();
        var processor = scope.ServiceProvider.GetRequiredService<IErpOutboxProcessor>();
        var notifications = await processor.ProcesarLoteAsync();

        var messages = notifications.Select(n => new SignalRMessageAction("Notificacion")
        {
            GroupName = $"sucursal-{n.SucursalId}",
            Arguments = [n.Notificacion]
        }).ToArray();

        _logger.LogInformation("ErpSyncFunction completado. {Count} notificaciones SignalR emitidas.",
            messages.Length);

        return new ErpSyncOutput { Messages = messages };
    }
}

public class ErpSyncOutput
{
    [SignalROutput(HubName = "notificaciones", ConnectionStringSetting = "AzureSignalRConnectionString")]
    public SignalRMessageAction[] Messages { get; set; } = [];
}
