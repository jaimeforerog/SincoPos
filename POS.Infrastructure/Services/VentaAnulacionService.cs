using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using POS.Application.DTOs;
using POS.Application.Services;
using POS.Domain.Aggregates;
using POS.Infrastructure.Data;
using POS.Infrastructure.Data.Entities;
using POS.Infrastructure.Services.Erp;

namespace POS.Infrastructure.Services;

/// <summary>
/// Gestiona la anulación de ventas: revierte inventario (Event Sourcing),
/// actualiza caja, genera asientos contables inversos y registra en el ERP Outbox.
/// </summary>
public class VentaAnulacionService
{
    private readonly AppDbContext _context;
    private readonly global::Marten.IDocumentSession _session;
    private readonly global::Marten.IDocumentStore _store;
    private readonly CosteoService _costeoService;
    private readonly ILogger<VentaAnulacionService> _logger;
    private readonly IActivityLogService _activityLogService;
    private readonly ErpSincoOptions _erpOptions;
    private readonly IVentaErpService _ventaErpService;

    public VentaAnulacionService(
        AppDbContext context,
        global::Marten.IDocumentSession session,
        global::Marten.IDocumentStore store,
        CosteoService costeoService,
        ILogger<VentaAnulacionService> logger,
        IActivityLogService activityLogService,
        IOptions<ErpSincoOptions> erpOptions,
        IVentaErpService ventaErpService)
    {
        _context = context;
        _session = session;
        _store = store;
        _costeoService = costeoService;
        _logger = logger;
        _activityLogService = activityLogService;
        _erpOptions = erpOptions.Value;
        _ventaErpService = ventaErpService;
    }

    public async Task<(bool success, string? error)> AnularVentaAsync(int id, string? motivo)
    {
        var venta = await _context.Ventas
            .Include(v => v.Detalles)
            .FirstOrDefaultAsync(v => v.Id == id);

        if (venta == null) return (false, "NOT_FOUND");
        if (venta.Estado == EstadoVenta.Anulada)
            return (false, "La venta ya esta anulada.");

        var sucursal = await _context.Sucursales.FindAsync(venta.SucursalId);

        // Revertir cada línea de inventario
        var pendingMartenEvents = new List<(Guid StreamId, object Evento)>();
        foreach (var detalle in venta.Detalles)
        {
            var streamId = InventarioAggregate.GenerarStreamId(detalle.ProductoId, venta.SucursalId);
            var aggregate = await _session.Events.AggregateStreamAsync<InventarioAggregate>(streamId);

            if (aggregate != null)
            {
                var entradaEvento = aggregate.AgregarEntrada(
                    detalle.Cantidad, detalle.CostoUnitario,
                    null, null, $"Anulacion venta {venta.NumeroVenta}",
                    motivo ?? "Venta anulada", null);
                pendingMartenEvents.Add((streamId, entradaEvento));
            }

            var montoImpuestoUnitario = detalle.Cantidad > 0
                ? detalle.MontoImpuesto / detalle.Cantidad
                : 0;
            await _costeoService.RegistrarLoteEntrada(
                detalle.ProductoId, venta.SucursalId,
                detalle.Cantidad, detalle.CostoUnitario,
                detalle.PorcentajeImpuesto, montoImpuestoUnitario,
                $"Anulacion {venta.NumeroVenta}", null);

            var stock = await _context.Stock
                .FirstOrDefaultAsync(s => s.ProductoId == detalle.ProductoId
                    && s.SucursalId == venta.SucursalId);
            if (stock != null)
                await _costeoService.ActualizarCostoEntrada(
                    stock, detalle.Cantidad, detalle.CostoUnitario, sucursal!.MetodoCosteo);
        }

        // Marcar como anulada
        venta.Estado = EstadoVenta.Anulada;
        venta.Observaciones = $"{venta.Observaciones} | ANULADA: {motivo ?? "Sin motivo"}";

        // Revertir monto de caja
        var caja = await _context.Cajas.FindAsync(venta.CajaId);
        if (caja != null)
            caja.MontoActual -= venta.Total;

        // Asientos contables inversos
        var asientosAnul = await BuildAsientosAnulacion(venta, sucursal);

        // Transacción atómica Marten + EF Core
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
            await martenTx.SaveChangesAsync();
            await _context.SaveChangesAsync();

            var anulPayload = new VentaErpPayload(
                NumeroVenta: venta.NumeroVenta,
                NitCliente: null,
                MetodoPago: venta.MetodoPago.ToString(),
                FechaVenta: DateTime.UtcNow,
                SucursalId: venta.SucursalId,
                Asientos: asientosAnul,
                TotalOriginalDocumento: venta.Total);
            await _ventaErpService.EmitirAnulacionAsync(venta, asientosAnul, anulPayload);
            await _context.SaveChangesAsync();

            await npgsqlTx.CommitAsync();
        });

        _logger.LogInformation("Venta {NumeroVenta} anulada. Motivo: {Motivo}",
            venta.NumeroVenta, motivo);

        await _activityLogService.LogActivityAsync(new ActivityLogDto(
            Accion: "AnularVenta",
            Tipo: TipoActividad.Venta,
            Descripcion: $"Venta {venta.NumeroVenta} anulada. Motivo: {motivo ?? "Sin motivo"}. Total revertido: ${venta.Total:N2}",
            SucursalId: venta.SucursalId,
            TipoEntidad: "Venta",
            EntidadId: id.ToString(),
            EntidadNombre: venta.NumeroVenta,
            DatosAnteriores: new { Estado = "Completada", Total = venta.Total, CantidadItems = venta.Detalles.Count },
            DatosNuevos: new
            {
                Estado = "Anulada",
                Motivo = motivo ?? "Sin motivo",
                ItemsRevertidos = venta.Detalles.Select(d => new { d.ProductoId, d.NombreProducto, d.Cantidad })
            }));

        return (true, null);
    }

    private async Task<List<AsientoContableErp>> BuildAsientosAnulacion(Venta venta, Sucursal? sucursal)
    {
        var centroCosto = sucursal?.CentroCosto ?? string.Empty;
        var productoIds = venta.Detalles.Select(d => d.ProductoId).Distinct().ToList();
        var prodMap = await _context.Productos
            .Include(p => p.Categoria)
            .Include(p => p.Impuesto)
            .Where(p => productoIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id);

        var ingresos = new Dictionary<string, decimal>();
        var iva = new Dictionary<string, (string Cuenta, decimal Total)>();

        foreach (var det in venta.Detalles)
        {
            var prod = prodMap.GetValueOrDefault(det.ProductoId);
            var cuentaIngreso = prod?.Categoria?.CuentaIngreso ?? "4135";
            ingresos.TryAdd(cuentaIngreso, 0);
            ingresos[cuentaIngreso] += det.Subtotal;

            if (det.PorcentajeImpuesto > 0)
            {
                var nombreImp = $"IVA {det.PorcentajeImpuesto * 100:0.##}%";
                var cuentaImp = prod?.Impuesto?.CodigoCuentaContable ?? "2408";
                iva.TryGetValue(nombreImp, out var cur);
                iva[nombreImp] = (cuentaImp, cur.Total + det.MontoImpuesto);
            }
        }

        var cuentaAcreditar = venta.MetodoPago switch
        {
            MetodoPago.Tarjeta       => _erpOptions.CuentaTarjeta,
            MetodoPago.Transferencia => _erpOptions.CuentaTransferencia,
            _                        => _erpOptions.CuentaCaja
        };

        var asientos = new List<AsientoContableErp>
        {
            new(cuentaAcreditar, centroCosto, "Credito", venta.Total,
                $"Reversión cobro anulación {venta.NumeroVenta}")
        };
        foreach (var ing in ingresos)
            asientos.Add(new(ing.Key, centroCosto, "Debito", ing.Value,
                $"Reversión ingreso anulación {venta.NumeroVenta}"));
        foreach (var item in iva)
            asientos.Add(new(item.Value.Cuenta, centroCosto, "Debito", item.Value.Total,
                $"Reversión {item.Key} anulación {venta.NumeroVenta}"));

        return asientos;
    }
}
