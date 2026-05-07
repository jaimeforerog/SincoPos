using POS.Application.DTOs;
using POS.Application.Services;
using POS.Infrastructure.Data.Entities;

namespace POS.Infrastructure.Services;

public sealed partial class VentaService
{
    private async Task NotificarVentaAsync(
        Venta venta,
        int sucursalId,
        string numeroVenta,
        decimal total,
        decimal subtotal,
        decimal descuentoTotal,
        decimal totalImpuestos,
        int metodoPago,
        int? clienteId,
        int cajaId,
        List<DetalleVenta> detalles,
        List<(Stock stock, string nombre)> stocksVerificar)
    {
        if (venta.RequiereFacturaElectronica)
            _facturacionBackground.Encolar(venta.Id);

        await _notificationService.EnviarNotificacionSucursalAsync(sucursalId, new NotificacionDto(
            "venta_completada", "Venta completada",
            $"Venta {numeroVenta} — ${total:N0}", "success", DateTime.UtcNow,
            new { VentaId = venta.Id, NumeroVenta = numeroVenta, Total = total }));

        foreach (var (stockItem, nombre) in stocksVerificar)
            if (stockItem.Cantidad >= 0 && stockItem.Cantidad <= stockItem.StockMinimo)
                await _notificationService.EnviarNotificacionSucursalAsync(sucursalId, new NotificacionDto(
                    "stock_bajo", "Stock bajo",
                    $"{nombre}: quedan {stockItem.Cantidad:F0} unidades", "warning", DateTime.UtcNow,
                    new { stockItem.ProductoId, NombreProducto = nombre, StockActual = stockItem.Cantidad }));

        await _activityLogService.LogActivityAsync(new ActivityLogDto(
            Accion: "CrearVenta",
            Tipo: TipoActividad.Venta,
            Descripcion: $"Venta {numeroVenta} creada. Total: ${total:N2}, Items: {detalles.Count}",
            SucursalId: sucursalId,
            TipoEntidad: "Venta",
            EntidadId: venta.Id.ToString(),
            EntidadNombre: numeroVenta,
            DatosNuevos: new
            {
                NumeroVenta = numeroVenta,
                Total = total,
                Subtotal = subtotal,
                Descuento = descuentoTotal,
                Impuestos = totalImpuestos,
                MetodoPago = ((MetodoPago)metodoPago).ToString(),
                CantidadItems = detalles.Count,
                ClienteId = clienteId,
                CajaId = cajaId,
                Productos = detalles.Select(d => new {
                    d.ProductoId,
                    d.NombreProducto,
                    d.Cantidad,
                    d.PrecioUnitario,
                    d.Subtotal
                })
            }
        ));
    }
}
