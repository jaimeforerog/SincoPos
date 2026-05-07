namespace POS.Application.Services;

/// <summary>
/// Procesa mensajes pendientes de la tabla erp_outbox_messages y los envía
/// al ERP a través de IErpClient. Implementa el patrón Outbox: garantiza que
/// los eventos sincronizados al ERP no se pierdan ante caídas de red.
///
/// Está separado de ErpSyncFunction para permitir tests unitarios sin levantar
/// el host de Azure Functions (la function queda como un thin trigger que delega).
/// </summary>
public interface IErpOutboxProcessor
{
    /// <summary>
    /// Procesa hasta <paramref name="batchSize"/> mensajes en estado Pendiente
    /// o Error (dentro del límite de reintentos). Persiste cambios y devuelve
    /// las notificaciones SignalR a emitir por el caller.
    /// </summary>
    Task<IReadOnlyList<OutboxNotification>> ProcesarLoteAsync(int batchSize = 10);
}

/// <summary>
/// Resultado de procesamiento que el caller (Function) traduce a SignalRMessageAction.
/// Se mantiene neutro para no acoplar este interfaz al SDK de Functions.
/// </summary>
public sealed record OutboxNotification(int SucursalId, NotificacionDto Notificacion);
