using POS.Application.DTOs;
using POS.Application.Services;
using POS.Infrastructure.Data;
using POS.Infrastructure.Data.Entities;
using Microsoft.Extensions.Logging;

namespace POS.Infrastructure.Services;

/// <summary>
/// Implementación de ICompraErpService.
/// Construye los asientos contables, registra el DocumentoContable y emite el ErpOutboxMessage.
/// Antes toda esta lógica estaba inline en CompraService.RecibirOrdenAsync.
/// </summary>
public sealed class CompraErpService : ICompraErpService
{
    private readonly AppDbContext _context;
    private readonly ILogger<CompraErpService> _logger;

    public CompraErpService(AppDbContext context, ILogger<CompraErpService> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task EmitirAsync(
        OrdenCompra orden,
        IReadOnlyList<AsientoContableErp> asientos,
        CompraErpPayload payload,
        string soporteRecepcion,
        int numeroRecepcion)
    {
        // 1. Invalidar outbox pendientes anteriores de esta misma orden
        var pendientes = _context.ErpOutboxMessages
            .Where(m => m.TipoDocumento == "CompraRecibida"
                     && m.EntidadId == orden.Id
                     && (m.Estado == EstadoOutbox.Pendiente || m.Estado == EstadoOutbox.Error))
            .ToList();

        foreach (var p in pendientes)
        {
            p.Estado = EstadoOutbox.Descartado;
            p.UltimoError = $"Supersedido por recepción #{numeroRecepcion} ({soporteRecepcion})";
        }

        // 2. Registrar DocumentoContable
        _context.DocumentosContables.Add(new DocumentoContable
        {
            NumeroSoporte = soporteRecepcion,
            TipoDocumento = "RecepcionCompra",
            TerceroId = orden.ProveedorId,
            SucursalId = orden.SucursalId,
            FechaCausacion = DateTime.UtcNow,
            FormaPago = orden.FormaPago,
            FechaVencimiento = payload.FechaVencimientoErp,
            TotalDebito = asientos.Where(a => a.Naturaleza == "Debito").Sum(a => a.Valor),
            TotalCredito = asientos.Where(a => a.Naturaleza == "Credito").Sum(a => a.Valor),
            Detalles = asientos.Select(a => new DetalleDocumentoContable
            {
                CuentaContable = a.Cuenta,
                CentroCosto = a.CentroCosto,
                Naturaleza = a.Naturaleza,
                Valor = a.Valor,
                Nota = a.Nota
            }).ToList()
        });

        // 3. Encolar ErpOutboxMessage
        _context.ErpOutboxMessages.Add(new ErpOutboxMessage
        {
            TipoDocumento = "CompraRecibida",
            EntidadId = orden.Id,
            Payload = System.Text.Json.JsonSerializer.Serialize(payload),
            FechaCreacion = DateTime.UtcNow,
            Estado = EstadoOutbox.Pendiente
        });

        _logger.LogDebug("ErpOutbox encolado para orden {NumeroOrden} recepción {NumRecepcion}",
            orden.NumeroOrden, soporteRecepcion);

        // La transacción la gestiona el llamador (CompraService)
        await Task.CompletedTask;
    }
}
