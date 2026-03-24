using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using POS.Application.DTOs;
using POS.Application.Services;
using POS.Infrastructure.Data;
using POS.Infrastructure.Data.Entities;

namespace POS.Infrastructure.Services.Erp;

public class ErpSyncBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ErpSyncBackgroundService> _logger;
    private readonly TimeSpan _pollingInterval = TimeSpan.FromSeconds(15);
    private readonly ErpSincoOptions _options;

    public ErpSyncBackgroundService(
        IServiceScopeFactory scopeFactory,
        ILogger<ErpSyncBackgroundService> logger,
        IOptions<ErpSincoOptions> options)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _options = options.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Iniciando ERP Sync Background Service...");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcesarPendientesAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error crítico verificando la bandeja Outbox del ERP");
            }

            // Esperar el polling interval (cancelar sin lanzar excepción al detener el host)
            try { await Task.Delay(_pollingInterval, stoppingToken); }
            catch (OperationCanceledException) { break; }
        }
    }

    private async Task ProcesarPendientesAsync(CancellationToken stoppingToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var erpClient = scope.ServiceProvider.GetRequiredService<IErpClient>();
        var notificationService = scope.ServiceProvider.GetRequiredService<INotificationService>();
        var activityLogService = scope.ServiceProvider.GetRequiredService<IActivityLogService>();

        // 1. Obtener mensajes pendientes o con error que no han superado reintentos
        var mensajes = await db.ErpOutboxMessages
            .Where(m => m.Estado == EstadoOutbox.Pendiente || 
                       (m.Estado == EstadoOutbox.Error && m.Intentos < _options.MaxReintentos))
            .OrderBy(m => m.FechaCreacion)
            .Take(10)
            .ToListAsync(stoppingToken);

        if (!mensajes.Any()) return;

        foreach (var mensaje in mensajes)
        {
            _logger.LogInformation("Procesando Outbox {Id} para Entidad {Entidad} ({Tipo})",
                mensaje.Id, mensaje.EntidadId, mensaje.TipoDocumento);

            mensaje.Intentos++;

            // 2. Deserializar Payload según Tipo
            if (mensaje.TipoDocumento == "VentaCompletada" || mensaje.TipoDocumento == "AnulacionVenta")
            {
                await ProcesarVentaOutboxAsync(db, erpClient, notificationService, activityLogService, mensaje, stoppingToken);
            }
            else if (mensaje.TipoDocumento == "CompraRecibida" || mensaje.TipoDocumento == "NotaCreditoVenta")
            {
                var payload = JsonSerializer.Deserialize<CompraErpPayload>(mensaje.Payload, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (payload == null)
                {
                    MarcarComoError(mensaje, "No se pudo deserializar el JSON de CompraErpPayload");
                    continue;
                }

                // 3. Enviar a ERP
                var response = await erpClient.ContabilizarCompraAsync(payload);

                // 4. Actualizar Estado Outbox
                if (response.Exitoso)
                {
                    mensaje.Estado = EstadoOutbox.Procesado;
                    mensaje.FechaProcesamiento = DateTime.UtcNow;
                    mensaje.UltimoError = null;

                    // 5. Auditar Exitoso, actualizar entidad y notificar SignalR
                    var tipoEntidad = mensaje.TipoDocumento == "CompraRecibida" ? "OrdenCompra" : "DevolucionVenta";
                    var tipoDoc = mensaje.TipoDocumento == "CompraRecibida" ? "RecepcionCompra" : "NotaCredito";
                    int sucursalIdNotif = payload.SucursalId;
                    string entidadNombre = payload.NumeroOrden;

                    if (mensaje.TipoDocumento == "CompraRecibida")
                    {
                        var ordenCompra = await db.OrdenesCompra.FirstOrDefaultAsync(o => o.Id == mensaje.EntidadId, stoppingToken);
                        if (ordenCompra != null)
                        {
                            ordenCompra.SincronizadoErp = true;
                            ordenCompra.FechaSincronizacionErp = DateTime.UtcNow;
                            ordenCompra.ErpReferencia = response.ErpReferencia;
                            ordenCompra.ErrorSincronizacion = null;
                            sucursalIdNotif = ordenCompra.SucursalId;
                            entidadNombre = ordenCompra.NumeroOrden;
                        }
                    }
                    else // NotaCreditoVenta
                    {
                        var devolucion = await db.DevolucionesVenta.FirstOrDefaultAsync(d => d.Id == mensaje.EntidadId, stoppingToken);
                        if (devolucion != null)
                        {
                            devolucion.SincronizadoErp = true;
                            devolucion.FechaSincronizacionErp = DateTime.UtcNow;
                            devolucion.ErpReferencia = response.ErpReferencia;
                            devolucion.ErrorSincronizacion = null;
                        }
                    }

                    // Actualizar DocumentoContable asociado
                    var docContable = await db.DocumentosContables
                        .Where(d => d.TipoDocumento == tipoDoc
                                 && d.NumeroSoporte == payload.NumeroOrden)
                        .OrderByDescending(d => d.FechaCausacion)
                        .FirstOrDefaultAsync(stoppingToken);
                    if (docContable != null)
                    {
                        docContable.SincronizadoErp = true;
                        docContable.ErpReferencia = response.ErpReferencia;
                        docContable.FechaSincronizacionErp = DateTime.UtcNow;
                    }

                    await activityLogService.LogActivityAsync(new ActivityLogDto(
                        Accion: "ERP_SYNC_EXITO",
                        Tipo: mensaje.TipoDocumento == "CompraRecibida" ? TipoActividad.Inventario : TipoActividad.Venta,
                        Descripcion: $"{tipoDoc} contabilizada en ERP Sinco. Ref: {response.ErpReferencia}",
                        SucursalId: sucursalIdNotif,
                        TipoEntidad: tipoEntidad,
                        EntidadId: mensaje.EntidadId.ToString(),
                        EntidadNombre: entidadNombre,
                        Exitosa: true
                    ));

                    await notificationService.EnviarNotificacionSucursalAsync(sucursalIdNotif, new NotificacionDto(
                        "erp_sincronizado",
                        mensaje.TipoDocumento == "CompraRecibida" ? "Compra contabilizada" : "Nota crédito contabilizada",
                        $"{entidadNombre} sincronizada con ERP Sinco (Ref: {response.ErpReferencia})",
                        "success",
                        DateTime.UtcNow
                    ));
                }
                else
                {
                    MarcarComoError(mensaje, response.MensajeError ?? "Error desconocido en ERP");

                    if (mensaje.TipoDocumento == "CompraRecibida")
                    {
                        var ordenCompra = await db.OrdenesCompra.FirstOrDefaultAsync(o => o.Id == mensaje.EntidadId, stoppingToken);
                        if (ordenCompra != null)
                        {
                            ordenCompra.SincronizadoErp = false;
                            ordenCompra.ErrorSincronizacion = response.MensajeError;
                        }
                    }
                    else // NotaCreditoVenta
                    {
                        var devolucion = await db.DevolucionesVenta.FirstOrDefaultAsync(d => d.Id == mensaje.EntidadId, stoppingToken);
                        if (devolucion != null)
                        {
                            devolucion.SincronizadoErp = false;
                            devolucion.ErrorSincronizacion = response.MensajeError;
                        }
                    }

                    await activityLogService.LogActivityAsync(new ActivityLogDto(
                        Accion: "ERP_SYNC_ERROR",
                        Tipo: mensaje.TipoDocumento == "CompraRecibida" ? TipoActividad.Inventario : TipoActividad.Venta,
                        Descripcion: $"Fallo sincronizando {mensaje.TipoDocumento} en ERP Sinco: {response.MensajeError}",
                        SucursalId: payload.SucursalId,
                        TipoEntidad: mensaje.TipoDocumento == "CompraRecibida" ? "OrdenCompra" : "DevolucionVenta",
                        EntidadId: mensaje.EntidadId.ToString(),
                        EntidadNombre: payload.NumeroOrden,
                        Exitosa: false,
                        MensajeError: response.MensajeError
                    ));

                    await notificationService.EnviarNotificacionSucursalAsync(payload.SucursalId, new NotificacionDto(
                        "erp_error",
                        "Error de Sincronización Contable",
                        $"Fallo contabilizando {payload.NumeroOrden}: {response.MensajeError}",
                        "error",
                        DateTime.UtcNow
                    ));
                }
            }
            else
            {
                MarcarComoError(mensaje, $"Tipo documento {mensaje.TipoDocumento} no soportado.");
            }
        }

        // 6. Guardar Cambios en la BD
        await db.SaveChangesAsync(stoppingToken);
    }

    private async Task ProcesarVentaOutboxAsync(
        AppDbContext db,
        IErpClient erpClient,
        INotificationService notificationService,
        IActivityLogService activityLogService,
        ErpOutboxMessage mensaje,
        CancellationToken stoppingToken)
    {
        var payload = JsonSerializer.Deserialize<VentaErpPayload>(mensaje.Payload, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        if (payload == null)
        {
            MarcarComoError(mensaje, "No se pudo deserializar el JSON de VentaErpPayload");
            return;
        }

        var esAnulacion = mensaje.TipoDocumento == "AnulacionVenta";
        var response = await erpClient.ContabilizarVentaAsync(payload);

        if (response.Exitoso)
        {
            mensaje.Estado = EstadoOutbox.Procesado;
            mensaje.FechaProcesamiento = DateTime.UtcNow;
            mensaje.UltimoError = null;

            var venta = await db.Ventas.FirstOrDefaultAsync(v => v.Id == mensaje.EntidadId, stoppingToken);
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
                .FirstOrDefaultAsync(stoppingToken);
            if (docContable != null)
            {
                docContable.SincronizadoErp = true;
                docContable.ErpReferencia = response.ErpReferencia;
                docContable.FechaSincronizacionErp = DateTime.UtcNow;
            }

            await activityLogService.LogActivityAsync(new ActivityLogDto(
                Accion: "ERP_SYNC_EXITO",
                Tipo: TipoActividad.Venta,
                Descripcion: $"{tipoDoc} contabilizada en ERP Sinco. Ref: {response.ErpReferencia}",
                SucursalId: payload.SucursalId,
                TipoEntidad: "Venta",
                EntidadId: mensaje.EntidadId.ToString(),
                EntidadNombre: payload.NumeroVenta,
                Exitosa: true
            ));

            await notificationService.EnviarNotificacionSucursalAsync(payload.SucursalId, new NotificacionDto(
                "erp_sincronizado",
                esAnulacion ? "Anulación contabilizada" : "Venta contabilizada",
                $"{payload.NumeroVenta} sincronizada con ERP Sinco (Ref: {response.ErpReferencia})",
                "success",
                DateTime.UtcNow
            ));
        }
        else
        {
            MarcarComoError(mensaje, response.MensajeError ?? "Error desconocido en ERP");

            var venta = await db.Ventas.FirstOrDefaultAsync(v => v.Id == mensaje.EntidadId, stoppingToken);
            if (venta != null)
            {
                venta.SincronizadoErp = false;
                venta.ErrorSincronizacion = response.MensajeError;
            }

            await activityLogService.LogActivityAsync(new ActivityLogDto(
                Accion: "ERP_SYNC_ERROR",
                Tipo: TipoActividad.Venta,
                Descripcion: $"Fallo sincronizando {mensaje.TipoDocumento} en ERP Sinco: {response.MensajeError}",
                SucursalId: payload.SucursalId,
                TipoEntidad: "Venta",
                EntidadId: mensaje.EntidadId.ToString(),
                EntidadNombre: payload.NumeroVenta,
                Exitosa: false,
                MensajeError: response.MensajeError
            ));

            await notificationService.EnviarNotificacionSucursalAsync(payload.SucursalId, new NotificacionDto(
                "erp_error",
                "Error de Sincronización Contable",
                $"Fallo contabilizando {payload.NumeroVenta}: {response.MensajeError}",
                "error",
                DateTime.UtcNow
            ));
        }
    }

    private void MarcarComoError(ErpOutboxMessage mensaje, string error)
    {
        mensaje.Estado = mensaje.Intentos >= _options.MaxReintentos ? EstadoOutbox.Descartado : EstadoOutbox.Error;
        mensaje.UltimoError = error;
    }
}
