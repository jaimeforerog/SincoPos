using JasperFx.Events;
using Marten;
using Marten.Events.Projections;
using Microsoft.Extensions.DependencyInjection;
using POS.Domain.Events.Inventario;
using POS.Infrastructure.Data;
using POS.Infrastructure.Data.Entities;
using POS.Infrastructure.Services;
using EFC = Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions;

namespace POS.Infrastructure.Projections;

/// <summary>
/// Projection inline: cada evento de inventario actualiza las tablas
/// stock y lotes_inventario en EF Core (modelos de lectura).
/// Usa FindAsync y LINQ en memoria para evitar colision de namespaces Marten/EF Core.
/// </summary>
public class InventarioProjection : IProjection
{
    private readonly IServiceProvider _serviceProvider;

    public InventarioProjection(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public async Task ApplyAsync(
        IDocumentOperations operations,
        IReadOnlyList<IEvent> events,
        CancellationToken cancellation)
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var costeoService = scope.ServiceProvider.GetRequiredService<CosteoService>();

        foreach (var @event in events)
        {
            switch (@event.Data)
            {
                case EntradaCompraRegistrada entrada:
                    await ProcesarEntrada(context, costeoService, entrada);
                    break;

                case EntradaManualRegistrada:
                    // Stock se actualiza directamente en InventarioService.RegistrarEntradaAsync
                    break;

                case DevolucionProveedorRegistrada devolucion:
                    await ProcesarDevolucion(context, costeoService, devolucion, @event.Timestamp.UtcDateTime);
                    break;

                case AjusteInventarioRegistrado ajuste:
                    await ProcesarAjuste(context, ajuste, @event.Timestamp.UtcDateTime);
                    break;

                case SalidaVentaRegistrada salida:
                    await ProcesarSalidaVenta(context, costeoService, salida, @event.Timestamp.UtcDateTime);
                    break;

                case StockMinimoActualizado minimo:
                    await ProcesarStockMinimo(context, minimo, @event.Timestamp.UtcDateTime);
                    break;

                case TrasladoSalidaRegistrado trasladoSalida:
                    await ProcesarSalidaTraslado(context, costeoService, trasladoSalida, @event.Timestamp.UtcDateTime);
                    break;

                case TrasladoEntradaRegistrado trasladoEntrada:
                    await ProcesarEntradaTraslado(context, costeoService, trasladoEntrada, @event.Timestamp.UtcDateTime);
                    break;
            }
        }

        await context.SaveChangesAsync(cancellation);
    }

    private static Task<Stock?> BuscarStock(AppDbContext context, Guid productoId, int sucursalId)
        // EFC alias evita colisión de nombres con las extensiones IQueryable de Marten
        => EFC.FirstOrDefaultAsync(context.Stock
            .Where(s => s.ProductoId == productoId && s.SucursalId == sucursalId));

    private Task ProcesarEntrada(AppDbContext context, CosteoService costeoService,
        EntradaCompraRegistrada e)
    {
        // IMPORTANTE: Este método NO procesa el stock/lotes para evitar doble procesamiento.
        //
        // Todos los emisores de EntradaCompraRegistrada actualizan stock directamente:
        // - InventarioService.RegistrarEntradaAsync: actualiza stock + lotes explícitamente
        // - CompraService.RecibirOrdenAsync: actualiza stock + lotes explícitamente
        // - VentaService.AnularVentaAsync: restaura stock + lotes explícitamente
        // - VentaService.CrearDevolucionParcialAsync: restaura stock + lotes explícitamente
        //
        // Este evento se conserva SOLO para auditoría del Event Store.

        return Task.CompletedTask;
    }

    private async Task ProcesarDevolucion(AppDbContext context, CosteoService costeoService,
        DevolucionProveedorRegistrada e, DateTime timestamp)
    {
        var sucursal = await context.Sucursales.FindAsync(e.SucursalId);
        var metodoCosteo = sucursal?.MetodoCosteo ?? MetodoCosteo.PromedioPonderado;

        var stock = await BuscarStock(context, e.ProductoId, e.SucursalId);

        if (stock != null)
        {
            await costeoService.ConsumirStock(e.ProductoId, e.SucursalId, e.Cantidad, metodoCosteo);
            stock.Cantidad -= e.Cantidad;
            stock.UltimaActualizacion = timestamp;
        }
    }

    private async Task ProcesarAjuste(AppDbContext context,
        AjusteInventarioRegistrado e, DateTime timestamp)
    {
        var stock = await BuscarStock(context, e.ProductoId, e.SucursalId);

        if (stock == null)
        {
            stock = new Stock
            {
                ProductoId = e.ProductoId,
                SucursalId = e.SucursalId,
                Cantidad = e.CantidadNueva,
                StockMinimo = 0,
                CostoPromedio = 0,
                UltimaActualizacion = timestamp
            };
            context.Stock.Add(stock);
        }
        else
        {
            stock.Cantidad = e.CantidadNueva;
            stock.UltimaActualizacion = timestamp;
        }
    }

    private Task ProcesarSalidaVenta(AppDbContext context, CosteoService costeoService,
        SalidaVentaRegistrada e, DateTime timestamp)
    {
        // IMPORTANTE: Este método NO procesa el stock para evitar doble consumo.
        //
        // El consumo de stock se realiza directamente en VentasController porque:
        // 1. El controller necesita el costo inmediatamente para crear el DetalleVenta
        // 2. Evita doble consumo (antes se consumía aquí Y en el controller)
        // 3. Los eventos SalidaVentaRegistrada se guardan solo para AUDITORÍA
        //
        // El flujo correcto es:
        // - VentasController: Consume stock + lotes, registra evento, crea venta
        // - InventarioProjection: Solo registra evento para auditoría (este método)
        //
        // Ver documentación: PROYECTO_SINCOPOS.md sección "Doble Consumo de Stock"

        return Task.CompletedTask;
    }

    private async Task ProcesarStockMinimo(AppDbContext context,
        StockMinimoActualizado e, DateTime timestamp)
    {
        var stock = await BuscarStock(context, e.ProductoId, e.SucursalId);

        if (stock != null)
        {
            stock.StockMinimo = e.StockMinimoNuevo;
            stock.UltimaActualizacion = timestamp;
        }
    }

    private async Task ProcesarSalidaTraslado(AppDbContext context, CosteoService _,
        TrasladoSalidaRegistrado e, DateTime timestamp)
    {
        var stock = await BuscarStock(context, e.ProductoId, e.SucursalOrigenId);

        if (stock == null) return;

        var sucursal = await context.Sucursales.FindAsync(e.SucursalOrigenId);
        if (sucursal == null) return;

        // Consumir lotes según método de costeo — query SQL con ORDER BY traducible
        var baseQuery = context.LotesInventario
            .Where(l => l.ProductoId == e.ProductoId
                && l.SucursalId == e.SucursalOrigenId
                && l.CantidadDisponible > 0);

        var lotes = sucursal.MetodoCosteo == MetodoCosteo.UEPS
            ? await EFC.ToListAsync(baseQuery.OrderByDescending(l => l.FechaEntrada))
            : await EFC.ToListAsync(baseQuery.OrderBy(l => l.FechaEntrada));

        var cantidadRestante = e.Cantidad;
        foreach (var lote in lotes)
        {
            if (cantidadRestante <= 0) break;

            var consumir = Math.Min(lote.CantidadDisponible, cantidadRestante);
            lote.CantidadDisponible -= consumir;
            cantidadRestante -= consumir;
        }

        stock.Cantidad -= e.Cantidad;
        stock.UltimaActualizacion = timestamp;

        // Registrar movimiento
        context.MovimientosInventario.Add(new MovimientoInventario
        {
            ProductoId = e.ProductoId,
            SucursalId = e.SucursalOrigenId,
            TipoMovimiento = TipoMovimiento.TransferenciaSalida,
            Cantidad = e.Cantidad,
            CostoUnitario = e.CostoUnitario,
            CostoTotal = e.CostoTotal,
            Referencia = e.NumeroTraslado,
            SucursalDestinoId = e.SucursalDestinoId,
            Observaciones = e.Observaciones,
            UsuarioId = e.UsuarioId ?? 0,
            FechaMovimiento = timestamp
        });
    }

    private async Task ProcesarEntradaTraslado(AppDbContext context, CosteoService costeoService,
        TrasladoEntradaRegistrado e, DateTime timestamp)
    {
        var stock = await BuscarStock(context, e.ProductoId, e.SucursalDestinoId);

        if (stock == null)
        {
            stock = new Stock
            {
                ProductoId = e.ProductoId,
                SucursalId = e.SucursalDestinoId,
                Cantidad = 0,
                StockMinimo = 0,
                CostoPromedio = 0
            };
            context.Stock.Add(stock);
        }

        // Crear lote de entrada
        context.LotesInventario.Add(new LoteInventario
        {
            ProductoId = e.ProductoId,
            SucursalId = e.SucursalDestinoId,
            CantidadInicial = e.CantidadRecibida,
            CantidadDisponible = e.CantidadRecibida,
            CostoUnitario = e.CostoUnitario,
            PorcentajeImpuesto = 0,
            MontoImpuestoUnitario = 0,
            Referencia = e.NumeroTraslado,
            FechaEntrada = timestamp
        });

        // Actualizar stock y costo promedio
        var cantidadAnterior = stock.Cantidad;
        var costoAnterior = stock.CostoPromedio;
        stock.Cantidad += e.CantidadRecibida;
        stock.CostoPromedio = stock.Cantidad > 0
            ? (cantidadAnterior * costoAnterior + e.CostoTotal) / stock.Cantidad
            : e.CostoUnitario;
        stock.UltimaActualizacion = timestamp;

        // Registrar movimiento
        context.MovimientosInventario.Add(new MovimientoInventario
        {
            ProductoId = e.ProductoId,
            SucursalId = e.SucursalDestinoId,
            TipoMovimiento = TipoMovimiento.TransferenciaEntrada,
            Cantidad = e.CantidadRecibida,
            CostoUnitario = e.CostoUnitario,
            CostoTotal = e.CostoTotal,
            Referencia = e.NumeroTraslado,
            SucursalDestinoId = e.SucursalOrigenId,  // De dónde vino
            Observaciones = e.Observaciones,
            UsuarioId = e.UsuarioId ?? 0,
            FechaMovimiento = timestamp
        });
    }
}
