using POS.Application.DTOs;
using POS.Infrastructure.Data.Entities;

namespace POS.Infrastructure.Services;

/// <summary>
/// Encapsula la lógica de generación del payload ERP y emisión de mensajes Outbox
/// durante la recepción de una orden de compra.
/// Extraído de CompraService para cumplir el principio de responsabilidad única.
/// </summary>
public interface ICompraErpService
{
    /// <summary>
    /// Construye los asientos contables y el payload CompraErpPayload,
    /// registra el DocumentoContable y encola el ErpOutboxMessage.
    /// Debe invocarse dentro de la transacción activa en AppDbContext.
    /// </summary>
    Task EmitirAsync(
        OrdenCompra orden,
        IReadOnlyList<AsientoContableErp> asientos,
        CompraErpPayload payload,
        string soporteRecepcion,
        int numeroRecepcion);
}
