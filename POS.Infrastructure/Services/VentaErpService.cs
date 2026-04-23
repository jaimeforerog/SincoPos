using System.Text.Json;
using Microsoft.Extensions.Logging;
using POS.Application.DTOs;
using POS.Infrastructure.Data;
using POS.Infrastructure.Data.Entities;

namespace POS.Infrastructure.Services;

/// <summary>
/// Implementación de IVentaErpService.
/// Construye el DocumentoContable y emite el ErpOutboxMessage para ventas/anulaciones.
/// Patrón idéntico a CompraErpService.
/// </summary>
public sealed class VentaErpService : IVentaErpService
{
    private readonly AppDbContext _context;
    private readonly ILogger<VentaErpService> _logger;

    public VentaErpService(AppDbContext context, ILogger<VentaErpService> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task EmitirVentaAsync(
        Venta venta,
        IReadOnlyList<AsientoContableErp> asientos,
        VentaErpPayload payload)
    {
        await EmitirInternoAsync(venta, asientos, payload, "VentaCompletada");
    }

    /// <inheritdoc/>
    public async Task EmitirAnulacionAsync(
        Venta venta,
        IReadOnlyList<AsientoContableErp> asientos,
        VentaErpPayload payload)
    {
        await EmitirInternoAsync(venta, asientos, payload, "AnulacionVenta");
    }

    private async Task EmitirInternoAsync(
        Venta venta,
        IReadOnlyList<AsientoContableErp> asientos,
        VentaErpPayload payload,
        string tipoDocumento)
    {
        // 1. Descartar outbox pendientes anteriores de esta venta para el mismo tipo
        var pendientes = _context.ErpOutboxMessages
            .Where(m => m.TipoDocumento == tipoDocumento
                     && m.EntidadId == venta.Id
                     && (m.Estado == EstadoOutbox.Pendiente || m.Estado == EstadoOutbox.Error))
            .ToList();

        foreach (var p in pendientes)
        {
            p.Estado = EstadoOutbox.Descartado;
            p.UltimoError = $"Supersedido por nueva emisión de {tipoDocumento} para venta {venta.NumeroVenta}";
        }

        var numeroSoporte = tipoDocumento == "AnulacionVenta"
            ? $"ANU-{venta.NumeroVenta}"
            : venta.NumeroVenta;

        // 2. Registrar DocumentoContable
        _context.DocumentosContables.Add(new DocumentoContable
        {
            NumeroSoporte = numeroSoporte,
            TipoDocumento = tipoDocumento == "AnulacionVenta" ? "AnulacionVenta" : "VentaCompletada",
            TerceroId = venta.ClienteId,
            SucursalId = venta.SucursalId,
            FechaCausacion = DateTime.UtcNow,
            FormaPago = venta.MetodoPago.ToString(),
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
            TipoDocumento = tipoDocumento,
            EntidadId = venta.Id,
            Payload = JsonSerializer.Serialize(payload),
            FechaCreacion = DateTime.UtcNow,
            Estado = EstadoOutbox.Pendiente
        });

        _logger.LogDebug("ErpOutbox encolado: {Tipo} para venta {NumeroVenta}",
            tipoDocumento, venta.NumeroVenta);

        // La transacción la gestiona el llamador (VentaService)
        await Task.CompletedTask;
    }
}
