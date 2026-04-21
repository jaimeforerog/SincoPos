using POS.Application.DTOs;
using POS.Infrastructure.Data;
using POS.Infrastructure.Data.Entities;

namespace POS.Infrastructure.Services;

public static class CompraMapper
{
    public static OrdenCompraDto MapearOrdenCompraDtoSync(OrdenCompra orden, Dictionary<int, string?> usuariosDict)
    {
        var aprobadoPor = orden.AprobadoPorUsuarioId.HasValue
            ? usuariosDict.GetValueOrDefault(orden.AprobadoPorUsuarioId.Value)
            : null;
        var recibidoPor = orden.RecibidoPorUsuarioId.HasValue
            ? usuariosDict.GetValueOrDefault(orden.RecibidoPorUsuarioId.Value)
            : null;
        return BuildOrdenCompraDto(orden, aprobadoPor, recibidoPor);
    }

    public static async Task<OrdenCompraDto> MapearOrdenCompraDtoAsync(OrdenCompra orden, AppDbContext context)
    {
        string? aprobadoPor = null;
        if (orden.AprobadoPorUsuarioId.HasValue)
        {
            var u = await context.Usuarios.FindAsync(orden.AprobadoPorUsuarioId.Value);
            aprobadoPor = u?.Email;
        }
        string? recibidoPor = null;
        if (orden.RecibidoPorUsuarioId.HasValue)
        {
            var u = await context.Usuarios.FindAsync(orden.RecibidoPorUsuarioId.Value);
            recibidoPor = u?.Email;
        }
        return BuildOrdenCompraDto(orden, aprobadoPor, recibidoPor);
    }

    public static OrdenCompraDto BuildOrdenCompraDto(OrdenCompra orden, string? aprobadoPor, string? recibidoPor)
        => new OrdenCompraDto(
            Id: orden.Id,
            NumeroOrden: orden.NumeroOrden,
            SucursalId: orden.SucursalId,
            NombreSucursal: orden.Sucursal.Nombre,
            ProveedorId: orden.ProveedorId,
            NombreProveedor: orden.Proveedor.Nombre,
            Estado: orden.Estado.ToString(),
            FormaPago: orden.FormaPago,
            DiasPlazo: orden.DiasPlazo,
            FechaOrden: orden.FechaOrden,
            FechaEntregaEsperada: orden.FechaEntregaEsperada,
            FechaAprobacion: orden.FechaAprobacion,
            FechaRecepcion: orden.FechaRecepcion,
            AprobadoPor: aprobadoPor,
            RecibidoPor: recibidoPor,
            Observaciones: orden.Observaciones,
            MotivoRechazo: orden.MotivoRechazo,
            Subtotal: orden.Subtotal,
            Impuestos: orden.Impuestos,
            Total: orden.Total,
            RequiereFacturaElectronica: orden.RequiereFacturaElectronica,
            SincronizadoErp: orden.SincronizadoErp,
            FechaSincronizacionErp: orden.FechaSincronizacionErp,
            ErpReferencia: orden.ErpReferencia,
            ErrorSincronizacion: orden.ErrorSincronizacion,
            Detalles: orden.Detalles.Select(d => new DetalleOrdenCompraDto(
                Id: d.Id,
                ProductoId: d.ProductoId,
                NombreProducto: d.NombreProducto,
                CantidadSolicitada: d.CantidadSolicitada,
                CantidadRecibida: d.CantidadRecibida,
                PrecioUnitario: d.PrecioUnitario,
                PorcentajeImpuesto: d.PorcentajeImpuesto * 100,
                MontoImpuesto: d.MontoImpuesto,
                Subtotal: d.Subtotal,
                NombreImpuesto: d.NombreImpuesto
                    ?? (d.PorcentajeImpuesto > 0 ? $"IVA {d.PorcentajeImpuesto * 100:0.##}%" : "Exento 0%"),
                Observaciones: d.Observaciones,
                ManejaLotes: d.Producto?.ManejaLotes ?? false,
                DiasVidaUtil: d.Producto?.DiasVidaUtil
            )).ToList()
        );
}
