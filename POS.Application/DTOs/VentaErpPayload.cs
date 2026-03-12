namespace POS.Application.DTOs;

/// <summary>
/// Carga útil contable de una venta completada o anulación,
/// formateada para los adaptadores de ERP (SincoErpClient).
/// </summary>
public record VentaErpPayload(
    string NumeroVenta,
    string? NitCliente,          // null = consumidor final
    string MetodoPago,           // "Efectivo", "Tarjeta", "Transferencia", "Mixto"
    DateTime FechaVenta,
    int SucursalId,
    List<AsientoContableErp> Asientos,
    decimal TotalOriginalDocumento
);
