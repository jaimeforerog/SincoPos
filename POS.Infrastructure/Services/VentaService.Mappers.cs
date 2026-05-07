using POS.Application.DTOs;
using POS.Infrastructure.Data.Entities;

namespace POS.Infrastructure.Services;

public sealed partial class VentaService
{
    public static VentaDto MapToDto(Venta v, string sucNombre, string cajaNombre, string? clienteNombre) =>
        new(
            v.Id, v.NumeroVenta,
            v.SucursalId, sucNombre,
            v.CajaId, cajaNombre,
            v.ClienteId, clienteNombre,
            v.Subtotal, v.Descuento, v.Impuestos, v.Total,
            v.Estado.ToString(), v.MetodoPago.ToString(),
            v.MontoPagado, v.Cambio,
            v.Observaciones, v.FechaVenta,
            v.Detalles.Select(d =>
            {
                var margen = d.PrecioUnitario > 0
                    ? Math.Round((d.PrecioUnitario - d.CostoUnitario) / d.PrecioUnitario * 100, 2)
                    : 0;
                return new DetalleVentaDto(
                    d.Id, d.ProductoId, d.NombreProducto, d.NumeroLote,
                    d.Cantidad, d.PrecioUnitario, d.CostoUnitario,
                    d.Descuento, d.PorcentajeImpuesto, d.MontoImpuesto,
                    d.Subtotal, margen,
                    d.Lotes.Select(l => new DetalleVentaLoteDto(
                        l.LoteInventarioId, l.NumeroLote, l.Cantidad, l.CostoUnitario)).ToList());
            }).ToList(),
            v.RequiereFacturaElectronica,
            v.SincronizadoErp,
            v.FechaSincronizacionErp,
            v.ErpReferencia,
            v.ErrorSincronizacion
        );

    public static DevolucionVentaDto MapDevolucionToDto(
        DevolucionVenta d, string numeroVenta, string? autorizadoPor) =>
        new(
            d.Id,
            d.VentaId,
            numeroVenta,
            d.NumeroDevolucion,
            d.Motivo,
            d.TotalDevuelto,
            d.FechaDevolucion,
            autorizadoPor,
            d.Detalles.Select(dd => new DetalleDevolucionDto(
                dd.Id,
                dd.ProductoId,
                dd.NombreProducto,
                dd.CantidadDevuelta,
                dd.PrecioUnitario,
                dd.CostoUnitario,
                dd.SubtotalDevuelto
            )).ToList(),
            d.SincronizadoErp,
            d.FechaSincronizacionErp,
            d.ErpReferencia,
            d.ErrorSincronizacion
        );
}
