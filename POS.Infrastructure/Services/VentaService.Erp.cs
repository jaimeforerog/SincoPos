using Microsoft.EntityFrameworkCore;
using POS.Application.DTOs;
using POS.Domain.Events.Venta;
using POS.Infrastructure.Data.Entities;
using POS.Infrastructure.Services.Erp;

namespace POS.Infrastructure.Services;

public sealed partial class VentaService
{
    private List<AsientoContableErp> ConstruirAsientosErp(
        List<DetalleVenta> detalles,
        Dictionary<Guid, Producto> productosMap,
        decimal total,
        string numeroVenta,
        int metodoPago,
        string centroCosto)
    {
        var ingresosErp = new Dictionary<string, decimal>();
        var ivaErp = new Dictionary<string, (string Cuenta, decimal Total)>();

        foreach (var det in detalles)
        {
            var prod = productosMap[det.ProductoId];
            var cuentaIngreso = prod.Categoria?.CuentaIngreso ?? "4135";
            ingresosErp.TryAdd(cuentaIngreso, 0);
            ingresosErp[cuentaIngreso] += det.Subtotal;

            if (det.PorcentajeImpuesto > 0)
            {
                var nombreImp = $"IVA {det.PorcentajeImpuesto * 100:0.##}%";
                var cuentaImp = prod.Impuesto?.CodigoCuentaContable ?? "2408";
                ivaErp.TryGetValue(nombreImp, out var cur);
                ivaErp[nombreImp] = (cuentaImp, cur.Total + det.MontoImpuesto);
            }
        }

        var cuentaDebito = ((MetodoPago)metodoPago) switch
        {
            MetodoPago.Tarjeta       => _erpOptions.CuentaTarjeta,
            MetodoPago.Transferencia => _erpOptions.CuentaTransferencia,
            _                        => _erpOptions.CuentaCaja
        };

        var asientos = new List<AsientoContableErp>();
        asientos.Add(new AsientoContableErp(
            cuentaDebito, centroCosto, "Debito", total,
            $"Cobro venta {numeroVenta} - {((MetodoPago)metodoPago)}"));
        foreach (var ing in ingresosErp)
            asientos.Add(new AsientoContableErp(
                ing.Key, centroCosto, "Credito", ing.Value,
                $"Ingreso venta {numeroVenta}"));
        foreach (var iva in ivaErp)
            asientos.Add(new AsientoContableErp(
                iva.Value.Cuenta, centroCosto, "Credito", iva.Value.Total,
                $"{iva.Key} generado venta {numeroVenta}"));

        return asientos;
    }

    private async Task EjecutarTransaccionAtomicaAsync(
        Venta venta,
        List<(Guid StreamId, object Evento)> pendingMartenEvents,
        string? externalId,
        CrearVentaDto dto,
        string? nitCliente,
        decimal total,
        List<AsientoContableErp> asientosVenta,
        List<DetalleVenta> detalles)
    {
        await _context.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            var npgsqlConn = (Npgsql.NpgsqlConnection)_context.Database.GetDbConnection();
            if (npgsqlConn.State != System.Data.ConnectionState.Open)
                await npgsqlConn.OpenAsync();
            await using var npgsqlTx = await npgsqlConn.BeginTransactionAsync();
            await _context.Database.UseTransactionAsync(npgsqlTx);
            await using var martenTx = _store.LightweightSession(
                global::Marten.Services.SessionOptions.ForTransaction(npgsqlTx));
            foreach (var (sid, evt) in pendingMartenEvents)
                martenTx.Events.Append(sid, evt);

            if (externalId != null)
            {
                // WorkOS user IDs son "user_xxx" — no GUIDs. Se deriva un stream key
                // determinístico para que todas las ventas del mismo usuario queden
                // en el mismo stream y las proyecciones acumulen correctamente.
                var userStreamId = new Guid(
                    System.Security.Cryptography.SHA256.HashData(
                        System.Text.Encoding.UTF8.GetBytes(externalId))[..16]);

                var ventaEvt = new VentaCompletadaEvent(
                    ExternalUserId: externalId,
                    SucursalId:     dto.SucursalId,
                    CajaId:         dto.CajaId,
                    HoraDelDia:     DateTime.UtcNow.Hour,
                    DiaSemana:      (int)DateTime.UtcNow.DayOfWeek,
                    Items:          detalles.Select(d => new VentaItemLine(d.ProductoId, d.NombreProducto, d.Cantidad, d.PrecioUnitario)).ToList(),
                    Total:          total,
                    ClienteId:      dto.ClienteId
                );
                martenTx.Events.Append(userStreamId, ventaEvt);
            }

            await martenTx.SaveChangesAsync();
            await _context.SaveChangesAsync();

            var ventaPayload = new VentaErpPayload(
                NumeroVenta: venta.NumeroVenta,
                NitCliente: nitCliente,
                MetodoPago: ((MetodoPago)dto.MetodoPago).ToString(),
                FechaVenta: venta.FechaVenta,
                SucursalId: venta.SucursalId,
                Asientos: asientosVenta,
                TotalOriginalDocumento: total);
            await _ventaErpService.EmitirVentaAsync(venta, asientosVenta, ventaPayload);
            await _context.SaveChangesAsync();

            await npgsqlTx.CommitAsync();
        });
    }
}
