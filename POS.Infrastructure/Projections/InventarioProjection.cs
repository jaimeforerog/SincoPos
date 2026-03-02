using JasperFx.Events;
using Marten;
using Marten.Events.Projections;
using Microsoft.Extensions.DependencyInjection;
using POS.Domain.Events.Inventario;
using POS.Infrastructure.Data;
using POS.Infrastructure.Data.Entities;
using POS.Infrastructure.Services;

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
            }
        }

        await context.SaveChangesAsync(cancellation);
    }

    private static Task<Stock?> BuscarStock(AppDbContext context, Guid productoId, int sucursalId)
    {
        // Usar LINQ en memoria para evitar colision FirstOrDefaultAsync de Marten/EF Core
        var stockList = context.Stock.AsEnumerable()
            .Where(s => s.ProductoId == productoId && s.SucursalId == sucursalId)
            .ToList();
        return Task.FromResult(stockList.FirstOrDefault());
    }

    private async Task ProcesarEntrada(AppDbContext context, CosteoService costeoService,
        EntradaCompraRegistrada e)
    {
        var sucursal = await context.Sucursales.FindAsync(e.SucursalId);
        var metodoCosteo = sucursal?.MetodoCosteo ?? MetodoCosteo.PromedioPonderado;

        var stock = await BuscarStock(context, e.ProductoId, e.SucursalId);

        if (stock == null)
        {
            stock = new Stock
            {
                ProductoId = e.ProductoId,
                SucursalId = e.SucursalId,
                Cantidad = 0,
                StockMinimo = 0,
                CostoPromedio = 0
            };
            context.Stock.Add(stock);
        }

        await costeoService.RegistrarLoteEntrada(
            e.ProductoId, e.SucursalId, e.Cantidad,
            e.CostoUnitario, e.PorcentajeImpuesto,
            e.Cantidad > 0 ? (e.MontoImpuesto / e.Cantidad) : 0,
            e.Referencia, e.TerceroId);

        await costeoService.ActualizarCostoEntrada(stock, e.Cantidad, e.CostoUnitario, metodoCosteo);
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
}
