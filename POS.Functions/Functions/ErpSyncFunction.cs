using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.SignalRService;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using POS.Application.DTOs;
using POS.Application.Services;
using POS.Infrastructure.Data;
using POS.Infrastructure.Data.Entities;
using POS.Infrastructure.Services.Erp;

namespace POS.Functions.Functions;

public class ErpSyncFunction
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ErpSyncFunction> _logger;
    private readonly ErpSincoOptions _options;

    public ErpSyncFunction(
        IServiceScopeFactory scopeFactory,
        ILogger<ErpSyncFunction> logger,
        IOptions<ErpSincoOptions> options)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _options = options.Value;
    }

    [Function("ErpSync")]
    public async Task<ErpSyncOutput> Run(
        [TimerTrigger("*/30 * * * * *")] TimerInfo timer)
    {
        _logger.LogInformation("ErpSyncFunction iniciado en {Timestamp}", DateTime.UtcNow);

        var messages = new List<SignalRMessageAction>();

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var erpClient = scope.ServiceProvider.GetRequiredService<IErpClient>();

        var mensajes = await db.ErpOutboxMessages
            .Where(m => m.Estado == EstadoOutbox.Pendiente ||
                       (m.Estado == EstadoOutbox.Error && m.Intentos < _options.MaxReintentos))
            .OrderBy(m => m.FechaCreacion)
            .Take(10)
            .ToListAsync();

        if (mensajes.Count == 0)
        {
            _logger.LogDebug("Sin mensajes Outbox pendientes.");
            return new ErpSyncOutput { Messages = [] };
        }

        _logger.LogInformation("Procesando {Count} mensajes Outbox.", mensajes.Count);

        foreach (var mensaje in mensajes)
        {
            mensaje.Intentos++;

            if (mensaje.TipoDocumento is "VentaCompletada" or "AnulacionVenta")
                await ProcesarVentaAsync(db, erpClient, mensaje, messages);
            else if (mensaje.TipoDocumento is "CompraRecibida" or "NotaCreditoVenta")
                await ProcesarCompraAsync(db, erpClient, mensaje, messages);
            else
                MarcarComoError(mensaje, $"Tipo '{mensaje.TipoDocumento}' no soportado.");
        }

        await db.SaveChangesAsync();

        _logger.LogInformation("ErpSyncFunction completado. {Count} notificaciones SignalR emitidas.",
            messages.Count);

        return new ErpSyncOutput { Messages = messages.ToArray() };
    }

    private async Task ProcesarVentaAsync(
        AppDbContext db,
        IErpClient erpClient,
        ErpOutboxMessage mensaje,
        List<SignalRMessageAction> messages)
    {
        var payload = JsonSerializer.Deserialize<VentaErpPayload>(mensaje.Payload,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        if (payload == null)
        {
            MarcarComoError(mensaje, "JSON inválido para VentaErpPayload.");
            return;
        }

        var esAnulacion = mensaje.TipoDocumento == "AnulacionVenta";
        var response = await erpClient.ContabilizarVentaAsync(payload);

        if (response.Exitoso)
        {
            mensaje.Estado = EstadoOutbox.Procesado;
            mensaje.FechaProcesamiento = DateTime.UtcNow;
            mensaje.UltimoError = null;

            var venta = await db.Ventas.FirstOrDefaultAsync(v => v.Id == mensaje.EntidadId);
            if (venta != null)
            {
                venta.SincronizadoErp = true;
                venta.FechaSincronizacionErp = DateTime.UtcNow;
                venta.ErpReferencia = response.ErpReferencia;
                venta.ErrorSincronizacion = null;
            }

            var tipoDoc = esAnulacion ? "AnulacionVenta" : "VentaCompletada";
            var numeroSoporte = esAnulacion ? $"ANU-{payload.NumeroVenta}" : payload.NumeroVenta;
            var docContable = await db.DocumentosContables
                .Where(d => d.TipoDocumento == tipoDoc && d.NumeroSoporte == numeroSoporte)
                .OrderByDescending(d => d.FechaCausacion)
                .FirstOrDefaultAsync();
            if (docContable != null)
            {
                docContable.SincronizadoErp = true;
                docContable.ErpReferencia = response.ErpReferencia;
                docContable.FechaSincronizacionErp = DateTime.UtcNow;
            }

            _logger.LogInformation("ERP_SYNC_OK Venta={NumeroVenta} Ref={Ref}",
                payload.NumeroVenta, response.ErpReferencia);

            messages.Add(Notificacion(payload.SucursalId,
                "erp_sincronizado",
                esAnulacion ? "Anulación contabilizada" : "Venta contabilizada",
                $"{payload.NumeroVenta} sincronizada con ERP Sinco (Ref: {response.ErpReferencia})",
                "success"));
        }
        else
        {
            MarcarComoError(mensaje, response.MensajeError ?? "Error desconocido");

            var venta = await db.Ventas.FirstOrDefaultAsync(v => v.Id == mensaje.EntidadId);
            if (venta != null)
            {
                venta.SincronizadoErp = false;
                venta.ErrorSincronizacion = response.MensajeError;
            }

            _logger.LogWarning("ERP_SYNC_ERROR Venta={NumeroVenta} Error={Error}",
                payload.NumeroVenta, response.MensajeError);

            messages.Add(Notificacion(payload.SucursalId,
                "erp_error",
                "Error de Sincronización Contable",
                $"Fallo contabilizando {payload.NumeroVenta}: {response.MensajeError}",
                "error"));
        }
    }

    private async Task ProcesarCompraAsync(
        AppDbContext db,
        IErpClient erpClient,
        ErpOutboxMessage mensaje,
        List<SignalRMessageAction> messages)
    {
        var payload = JsonSerializer.Deserialize<CompraErpPayload>(mensaje.Payload,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        if (payload == null)
        {
            MarcarComoError(mensaje, "JSON inválido para CompraErpPayload.");
            return;
        }

        var esCompra = mensaje.TipoDocumento == "CompraRecibida";
        var tipoDoc = esCompra ? "RecepcionCompra" : "NotaCredito";
        var response = await erpClient.ContabilizarCompraAsync(payload);

        if (response.Exitoso)
        {
            mensaje.Estado = EstadoOutbox.Procesado;
            mensaje.FechaProcesamiento = DateTime.UtcNow;
            mensaje.UltimoError = null;

            int sucursalIdNotif = payload.SucursalId;

            if (esCompra)
            {
                var orden = await db.OrdenesCompra.FirstOrDefaultAsync(o => o.Id == mensaje.EntidadId);
                if (orden != null)
                {
                    orden.SincronizadoErp = true;
                    orden.FechaSincronizacionErp = DateTime.UtcNow;
                    orden.ErpReferencia = response.ErpReferencia;
                    orden.ErrorSincronizacion = null;
                    sucursalIdNotif = orden.SucursalId;
                }
            }
            else
            {
                var devolucion = await db.DevolucionesVenta.FirstOrDefaultAsync(d => d.Id == mensaje.EntidadId);
                if (devolucion != null)
                {
                    devolucion.SincronizadoErp = true;
                    devolucion.FechaSincronizacionErp = DateTime.UtcNow;
                    devolucion.ErpReferencia = response.ErpReferencia;
                    devolucion.ErrorSincronizacion = null;
                }
            }

            var docContable = await db.DocumentosContables
                .Where(d => d.TipoDocumento == tipoDoc && d.NumeroSoporte == payload.NumeroOrden)
                .OrderByDescending(d => d.FechaCausacion)
                .FirstOrDefaultAsync();
            if (docContable != null)
            {
                docContable.SincronizadoErp = true;
                docContable.ErpReferencia = response.ErpReferencia;
                docContable.FechaSincronizacionErp = DateTime.UtcNow;
            }

            _logger.LogInformation("ERP_SYNC_OK {Tipo}={Numero} Ref={Ref}",
                tipoDoc, payload.NumeroOrden, response.ErpReferencia);

            messages.Add(Notificacion(sucursalIdNotif,
                "erp_sincronizado",
                esCompra ? "Compra contabilizada" : "Nota crédito contabilizada",
                $"{payload.NumeroOrden} sincronizada con ERP Sinco (Ref: {response.ErpReferencia})",
                "success"));
        }
        else
        {
            MarcarComoError(mensaje, response.MensajeError ?? "Error desconocido");

            if (esCompra)
            {
                var orden = await db.OrdenesCompra.FirstOrDefaultAsync(o => o.Id == mensaje.EntidadId);
                if (orden != null) { orden.SincronizadoErp = false; orden.ErrorSincronizacion = response.MensajeError; }
            }
            else
            {
                var devolucion = await db.DevolucionesVenta.FirstOrDefaultAsync(d => d.Id == mensaje.EntidadId);
                if (devolucion != null) { devolucion.SincronizadoErp = false; devolucion.ErrorSincronizacion = response.MensajeError; }
            }

            _logger.LogWarning("ERP_SYNC_ERROR {Tipo}={Numero} Error={Error}",
                tipoDoc, payload.NumeroOrden, response.MensajeError);

            messages.Add(Notificacion(payload.SucursalId,
                "erp_error",
                "Error de Sincronización Contable",
                $"Fallo contabilizando {payload.NumeroOrden}: {response.MensajeError}",
                "error"));
        }
    }

    private static SignalRMessageAction Notificacion(
        int sucursalId, string tipo, string titulo, string mensaje, string nivel)
    {
        var dto = new NotificacionDto(tipo, titulo, mensaje, nivel, DateTime.UtcNow);
        return new SignalRMessageAction("Notificacion")
        {
            GroupName = $"sucursal-{sucursalId}",
            Arguments = [dto]
        };
    }

    private void MarcarComoError(ErpOutboxMessage mensaje, string error)
    {
        mensaje.Estado = mensaje.Intentos >= _options.MaxReintentos
            ? EstadoOutbox.Descartado
            : EstadoOutbox.Error;
        mensaje.UltimoError = error;
        _logger.LogWarning("Outbox {Id} marcado como {Estado}: {Error}",
            mensaje.Id, mensaje.Estado, error);
    }
}

public class ErpSyncOutput
{
    [SignalROutput(HubName = "notificaciones", ConnectionStringSetting = "AzureSignalRConnectionString")]
    public SignalRMessageAction[] Messages { get; set; } = [];
}
