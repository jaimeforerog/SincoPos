using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using POS.Application.DTOs;
using POS.Application.Services;
using POS.Infrastructure.Data;
using POS.Infrastructure.Data.Entities;

namespace POS.Infrastructure.Services.Erp;

public sealed class ErpOutboxProcessor : IErpOutboxProcessor
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly AppDbContext _db;
    private readonly IErpClient _erpClient;
    private readonly ILogger<ErpOutboxProcessor> _logger;
    private readonly ErpSincoOptions _options;

    public ErpOutboxProcessor(
        AppDbContext db,
        IErpClient erpClient,
        ILogger<ErpOutboxProcessor> logger,
        IOptions<ErpSincoOptions> options)
    {
        _db = db;
        _erpClient = erpClient;
        _logger = logger;
        _options = options.Value;
    }

    public async Task<IReadOnlyList<OutboxNotification>> ProcesarLoteAsync(int batchSize = 10)
    {
        var notifications = new List<OutboxNotification>();

        var mensajes = await _db.ErpOutboxMessages
            .Where(m => m.Estado == EstadoOutbox.Pendiente ||
                       (m.Estado == EstadoOutbox.Error && m.Intentos < _options.MaxReintentos))
            .OrderBy(m => m.FechaCreacion)
            .Take(batchSize)
            .ToListAsync();

        if (mensajes.Count == 0)
        {
            _logger.LogDebug("Sin mensajes Outbox pendientes.");
            return notifications;
        }

        _logger.LogInformation("Procesando {Count} mensajes Outbox.", mensajes.Count);

        foreach (var mensaje in mensajes)
        {
            mensaje.Intentos++;

            if (mensaje.TipoDocumento is "VentaCompletada" or "AnulacionVenta")
                await ProcesarVentaAsync(mensaje, notifications);
            else if (mensaje.TipoDocumento is "CompraRecibida" or "NotaCreditoVenta")
                await ProcesarCompraAsync(mensaje, notifications);
            else
                MarcarComoError(mensaje, $"Tipo '{mensaje.TipoDocumento}' no soportado.");
        }

        await _db.SaveChangesAsync();
        return notifications;
    }

    private async Task ProcesarVentaAsync(ErpOutboxMessage mensaje, List<OutboxNotification> notifications)
    {
        VentaErpPayload? payload;
        try
        {
            payload = JsonSerializer.Deserialize<VentaErpPayload>(mensaje.Payload, JsonOpts);
        }
        catch (JsonException ex)
        {
            MarcarComoError(mensaje, $"JSON inválido para VentaErpPayload: {ex.Message}");
            return;
        }

        if (payload == null)
        {
            MarcarComoError(mensaje, "JSON inválido para VentaErpPayload.");
            return;
        }

        var esAnulacion = mensaje.TipoDocumento == "AnulacionVenta";
        var response = await _erpClient.ContabilizarVentaAsync(payload);

        if (response.Exitoso)
        {
            mensaje.Estado = EstadoOutbox.Procesado;
            mensaje.FechaProcesamiento = DateTime.UtcNow;
            mensaje.UltimoError = null;

            var venta = await _db.Ventas.FirstOrDefaultAsync(v => v.Id == mensaje.EntidadId);
            if (venta != null)
            {
                venta.SincronizadoErp = true;
                venta.FechaSincronizacionErp = DateTime.UtcNow;
                venta.ErpReferencia = response.ErpReferencia;
                venta.ErrorSincronizacion = null;
            }

            var tipoDoc = esAnulacion ? "AnulacionVenta" : "VentaCompletada";
            var numeroSoporte = esAnulacion ? $"ANU-{payload.NumeroVenta}" : payload.NumeroVenta;
            var docContable = await _db.DocumentosContables
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

            notifications.Add(new OutboxNotification(
                payload.SucursalId,
                new NotificacionDto(
                    "erp_sincronizado",
                    esAnulacion ? "Anulación contabilizada" : "Venta contabilizada",
                    $"{payload.NumeroVenta} sincronizada con ERP Sinco (Ref: {response.ErpReferencia})",
                    "success",
                    DateTime.UtcNow)));
        }
        else
        {
            MarcarComoError(mensaje, response.MensajeError ?? "Error desconocido");

            var venta = await _db.Ventas.FirstOrDefaultAsync(v => v.Id == mensaje.EntidadId);
            if (venta != null)
            {
                venta.SincronizadoErp = false;
                venta.ErrorSincronizacion = response.MensajeError;
            }

            _logger.LogWarning("ERP_SYNC_ERROR Venta={NumeroVenta} Error={Error}",
                payload.NumeroVenta, response.MensajeError);

            notifications.Add(new OutboxNotification(
                payload.SucursalId,
                new NotificacionDto(
                    "erp_error",
                    "Error de Sincronización Contable",
                    $"Fallo contabilizando {payload.NumeroVenta}: {response.MensajeError}",
                    "error",
                    DateTime.UtcNow)));
        }
    }

    private async Task ProcesarCompraAsync(ErpOutboxMessage mensaje, List<OutboxNotification> notifications)
    {
        CompraErpPayload? payload;
        try
        {
            payload = JsonSerializer.Deserialize<CompraErpPayload>(mensaje.Payload, JsonOpts);
        }
        catch (JsonException ex)
        {
            MarcarComoError(mensaje, $"JSON inválido para CompraErpPayload: {ex.Message}");
            return;
        }

        if (payload == null)
        {
            MarcarComoError(mensaje, "JSON inválido para CompraErpPayload.");
            return;
        }

        var esCompra = mensaje.TipoDocumento == "CompraRecibida";
        var tipoDoc = esCompra ? "RecepcionCompra" : "NotaCredito";
        var response = await _erpClient.ContabilizarCompraAsync(payload);

        if (response.Exitoso)
        {
            mensaje.Estado = EstadoOutbox.Procesado;
            mensaje.FechaProcesamiento = DateTime.UtcNow;
            mensaje.UltimoError = null;

            int sucursalIdNotif = payload.SucursalId;

            if (esCompra)
            {
                var orden = await _db.OrdenesCompra.FirstOrDefaultAsync(o => o.Id == mensaje.EntidadId);
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
                var devolucion = await _db.DevolucionesVenta.FirstOrDefaultAsync(d => d.Id == mensaje.EntidadId);
                if (devolucion != null)
                {
                    devolucion.SincronizadoErp = true;
                    devolucion.FechaSincronizacionErp = DateTime.UtcNow;
                    devolucion.ErpReferencia = response.ErpReferencia;
                    devolucion.ErrorSincronizacion = null;
                }
            }

            var docContable = await _db.DocumentosContables
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

            notifications.Add(new OutboxNotification(
                sucursalIdNotif,
                new NotificacionDto(
                    "erp_sincronizado",
                    esCompra ? "Compra contabilizada" : "Nota crédito contabilizada",
                    $"{payload.NumeroOrden} sincronizada con ERP Sinco (Ref: {response.ErpReferencia})",
                    "success",
                    DateTime.UtcNow)));
        }
        else
        {
            MarcarComoError(mensaje, response.MensajeError ?? "Error desconocido");

            if (esCompra)
            {
                var orden = await _db.OrdenesCompra.FirstOrDefaultAsync(o => o.Id == mensaje.EntidadId);
                if (orden != null) { orden.SincronizadoErp = false; orden.ErrorSincronizacion = response.MensajeError; }
            }
            else
            {
                var devolucion = await _db.DevolucionesVenta.FirstOrDefaultAsync(d => d.Id == mensaje.EntidadId);
                if (devolucion != null) { devolucion.SincronizadoErp = false; devolucion.ErrorSincronizacion = response.MensajeError; }
            }

            _logger.LogWarning("ERP_SYNC_ERROR {Tipo}={Numero} Error={Error}",
                tipoDoc, payload.NumeroOrden, response.MensajeError);

            notifications.Add(new OutboxNotification(
                payload.SucursalId,
                new NotificacionDto(
                    "erp_error",
                    "Error de Sincronización Contable",
                    $"Fallo contabilizando {payload.NumeroOrden}: {response.MensajeError}",
                    "error",
                    DateTime.UtcNow)));
        }
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
