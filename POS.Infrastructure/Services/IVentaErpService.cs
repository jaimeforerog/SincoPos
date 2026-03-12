using POS.Application.DTOs;
using POS.Infrastructure.Data.Entities;

namespace POS.Infrastructure.Services;

/// <summary>
/// Encapsula la generación del payload ERP y emisión de mensajes Outbox
/// para ventas completadas y anulaciones de venta.
/// Debe invocarse dentro de la transacción activa en AppDbContext.
/// </summary>
public interface IVentaErpService
{
    /// <summary>
    /// Registra el DocumentoContable y encola un ErpOutboxMessage de tipo "VentaCompletada".
    /// </summary>
    Task EmitirVentaAsync(
        Venta venta,
        IReadOnlyList<AsientoContableErp> asientos,
        VentaErpPayload payload);

    /// <summary>
    /// Registra el DocumentoContable y encola un ErpOutboxMessage de tipo "AnulacionVenta"
    /// con asientos inversos a la venta original.
    /// </summary>
    Task EmitirAnulacionAsync(
        Venta venta,
        IReadOnlyList<AsientoContableErp> asientos,
        VentaErpPayload payload);
}
