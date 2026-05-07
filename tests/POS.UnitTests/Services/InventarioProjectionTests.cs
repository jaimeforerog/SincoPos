using FluentAssertions;
using JasperFx.Events;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using POS.Application.Services;
using POS.Domain.Events.Inventario;
using POS.Infrastructure.Data;
using POS.Infrastructure.Data.Entities;
using POS.Infrastructure.Projections;
using POS.Infrastructure.Services;

namespace POS.UnitTests.Services;

public sealed class InventarioProjectionTests : IDisposable
{
    private const int SucursalId = 1;
    private static readonly Guid ProductoId = Guid.NewGuid();

    private readonly string _dbName = Guid.NewGuid().ToString();
    private readonly ServiceProvider _serviceProvider;
    private readonly InventarioProjection _sut;

    public InventarioProjectionTests()
    {
        var services = new ServiceCollection();

        services.AddSingleton(_ => new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(_dbName)
            .Options);

        var empresaProvider = Substitute.For<ICurrentEmpresaProvider>();
        empresaProvider.EmpresaId.Returns((int?)null);
        services.AddSingleton(empresaProvider);
        services.AddSingleton(Substitute.For<IHttpContextAccessor>());

        services.AddScoped(sp => new AppDbContext(
            sp.GetRequiredService<DbContextOptions<AppDbContext>>(),
            sp.GetRequiredService<IHttpContextAccessor>(),
            sp.GetRequiredService<ICurrentEmpresaProvider>(),
            NullLogger<AppDbContext>.Instance));

        services.AddScoped(sp => new CosteoService(
            sp.GetRequiredService<AppDbContext>(),
            NullLogger<CosteoService>.Instance));

        _serviceProvider = services.BuildServiceProvider();
        _sut = new InventarioProjection(_serviceProvider);
    }

    public void Dispose() => _serviceProvider.Dispose();

    // ─── Helpers ──────────────────────────────────────────────────────────────

    private async Task SeedAsync(Action<AppDbContext> seed)
    {
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        seed(db);
        await db.SaveChangesAsync();
    }

    private async Task<T> QueryAsync<T>(Func<AppDbContext, Task<T>> query)
    {
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await query(db);
    }

    private static IEvent EventFor(object data, DateTime? timestamp = null)
    {
        var e = Substitute.For<IEvent>();
        e.Data.Returns(data);
        e.Timestamp.Returns(new DateTimeOffset(timestamp ?? DateTime.UtcNow, TimeSpan.Zero));
        return e;
    }

    private Task ApplyAsync(params IEvent[] events) =>
        _sut.ApplyAsync(Substitute.For<Marten.IDocumentOperations>(), events, CancellationToken.None);

    // ─── EntradaCompraRegistrada / SalidaVentaRegistrada (no-op) ──────────────

    [Fact]
    public async Task EntradaCompraRegistrada_NoModificaStock()
    {
        // El stock se actualiza directamente en CompraService/InventarioService;
        // la projection conserva el evento solo para auditoría.
        await SeedAsync(db => db.Stock.Add(new Stock
        {
            ProductoId = ProductoId, SucursalId = SucursalId,
            Cantidad = 100, CostoPromedio = 50
        }));

        await ApplyAsync(EventFor(new EntradaCompraRegistrada
        {
            ProductoId = ProductoId, SucursalId = SucursalId,
            Cantidad = 5, CostoUnitario = 60
        }));

        var stock = await QueryAsync(db => db.Stock.AsNoTracking()
            .FirstAsync(s => s.ProductoId == ProductoId));
        stock.Cantidad.Should().Be(100, "EntradaCompra es solo auditoría en la projection");
    }

    [Fact]
    public async Task SalidaVentaRegistrada_NoModificaStock()
    {
        await SeedAsync(db => db.Stock.Add(new Stock
        {
            ProductoId = ProductoId, SucursalId = SucursalId,
            Cantidad = 50, CostoPromedio = 100
        }));

        await ApplyAsync(EventFor(new SalidaVentaRegistrada
        {
            ProductoId = ProductoId, SucursalId = SucursalId,
            Cantidad = 10, CostoUnitario = 100, CostoTotal = 1000, PrecioVenta = 150
        }));

        var stock = await QueryAsync(db => db.Stock.AsNoTracking()
            .FirstAsync(s => s.ProductoId == ProductoId));
        stock.Cantidad.Should().Be(50);
    }

    // ─── AjusteInventarioRegistrado ───────────────────────────────────────────

    [Fact]
    public async Task AjusteInventario_StockExistente_FijaCantidadNueva()
    {
        await SeedAsync(db => db.Stock.Add(new Stock
        {
            ProductoId = ProductoId, SucursalId = SucursalId,
            Cantidad = 100, CostoPromedio = 50
        }));

        await ApplyAsync(EventFor(new AjusteInventarioRegistrado
        {
            ProductoId = ProductoId, SucursalId = SucursalId,
            CantidadAnterior = 100, CantidadNueva = 95, Diferencia = -5, EsPositivo = false
        }));

        var stock = await QueryAsync(db => db.Stock.AsNoTracking()
            .FirstAsync(s => s.ProductoId == ProductoId));
        stock.Cantidad.Should().Be(95);
    }

    [Fact]
    public async Task AjusteInventario_SinStock_CreaRegistroNuevo()
    {
        // Sin Stock previo en la tabla
        await ApplyAsync(EventFor(new AjusteInventarioRegistrado
        {
            ProductoId = ProductoId, SucursalId = SucursalId,
            CantidadAnterior = 0, CantidadNueva = 25, Diferencia = 25, EsPositivo = true
        }));

        var stock = await QueryAsync(db => db.Stock.AsNoTracking()
            .FirstOrDefaultAsync(s => s.ProductoId == ProductoId));
        stock.Should().NotBeNull();
        stock!.Cantidad.Should().Be(25);
    }

    // ─── StockMinimoActualizado ───────────────────────────────────────────────

    [Fact]
    public async Task StockMinimoActualizado_StockExistente_ActualizaMinimo()
    {
        await SeedAsync(db => db.Stock.Add(new Stock
        {
            ProductoId = ProductoId, SucursalId = SucursalId,
            Cantidad = 10, StockMinimo = 5
        }));

        await ApplyAsync(EventFor(new StockMinimoActualizado
        {
            ProductoId = ProductoId, SucursalId = SucursalId,
            StockMinimoAnterior = 5, StockMinimoNuevo = 12
        }));

        var stock = await QueryAsync(db => db.Stock.AsNoTracking()
            .FirstAsync(s => s.ProductoId == ProductoId));
        stock.StockMinimo.Should().Be(12);
        stock.Cantidad.Should().Be(10, "actualizar mínimo no debe alterar la cantidad");
    }

    [Fact]
    public async Task StockMinimoActualizado_SinStock_NoLanzaExcepcion()
    {
        // Defensivo: si no hay stock previo, el evento se ignora silenciosamente
        var act = () => ApplyAsync(EventFor(new StockMinimoActualizado
        {
            ProductoId = ProductoId, SucursalId = SucursalId,
            StockMinimoAnterior = 0, StockMinimoNuevo = 5
        }));

        await act.Should().NotThrowAsync();
    }

    // ─── TrasladoEntradaRegistrado ────────────────────────────────────────────

    [Fact]
    public async Task TrasladoEntrada_RecalculaCostoPromedioPonderado()
    {
        const int destino = 2;
        await SeedAsync(db => db.Stock.Add(new Stock
        {
            ProductoId = ProductoId, SucursalId = destino,
            Cantidad = 10, CostoPromedio = 100
        }));

        await ApplyAsync(EventFor(new TrasladoEntradaRegistrado
        {
            ProductoId = ProductoId,
            SucursalOrigenId = SucursalId, SucursalDestinoId = destino,
            CantidadRecibida = 10, CostoUnitario = 200, CostoTotal = 2000,
            NumeroTraslado = "TRL-001"
        }));

        // (10*100 + 10*200) / 20 = 150
        var stock = await QueryAsync(db => db.Stock.AsNoTracking()
            .FirstAsync(s => s.SucursalId == destino));
        stock.Cantidad.Should().Be(20);
        stock.CostoPromedio.Should().Be(150);

        var lote = await QueryAsync(db => db.LotesInventario.AsNoTracking()
            .FirstAsync(l => l.SucursalId == destino));
        lote.CantidadInicial.Should().Be(10);
        lote.CostoUnitario.Should().Be(200);
        lote.Referencia.Should().Be("TRL-001");
    }

    [Fact]
    public async Task TrasladoEntrada_SinStockPrevio_CreaStockYLote()
    {
        const int destino = 2;

        await ApplyAsync(EventFor(new TrasladoEntradaRegistrado
        {
            ProductoId = ProductoId,
            SucursalOrigenId = SucursalId, SucursalDestinoId = destino,
            CantidadRecibida = 5, CostoUnitario = 80, CostoTotal = 400,
            NumeroTraslado = "TRL-002"
        }));

        var stock = await QueryAsync(db => db.Stock.AsNoTracking()
            .FirstOrDefaultAsync(s => s.SucursalId == destino));
        stock.Should().NotBeNull();
        stock!.Cantidad.Should().Be(5);
        stock.CostoPromedio.Should().Be(80, "primer ingreso fija el costo promedio al unitario");

        var movimientos = await QueryAsync(db => db.MovimientosInventario.AsNoTracking()
            .CountAsync(m => m.SucursalId == destino && m.TipoMovimiento == TipoMovimiento.TransferenciaEntrada));
        movimientos.Should().Be(1);
    }

    // ─── TrasladoSalidaRegistrado ─────────────────────────────────────────────

    [Fact]
    public async Task TrasladoSalida_FIFO_ConsumeLoteMasAntiguoPrimero()
    {
        var loteViejo = DateTime.UtcNow.AddDays(-10);
        var loteNuevo = DateTime.UtcNow.AddDays(-1);

        await SeedAsync(db =>
        {
            db.Sucursales.Add(new Sucursal
            {
                Id = SucursalId, EmpresaId = 1, Nombre = "Origen",
                Direccion = "x", Telefono = "y",
                MetodoCosteo = MetodoCosteo.PEPS
            });
            db.Stock.Add(new Stock
            {
                ProductoId = ProductoId, SucursalId = SucursalId,
                Cantidad = 20, CostoPromedio = 100
            });
            db.LotesInventario.Add(new LoteInventario
            {
                ProductoId = ProductoId, SucursalId = SucursalId,
                CantidadInicial = 10, CantidadDisponible = 10,
                CostoUnitario = 100, FechaEntrada = loteViejo
            });
            db.LotesInventario.Add(new LoteInventario
            {
                ProductoId = ProductoId, SucursalId = SucursalId,
                CantidadInicial = 10, CantidadDisponible = 10,
                CostoUnitario = 200, FechaEntrada = loteNuevo
            });
        });

        await ApplyAsync(EventFor(new TrasladoSalidaRegistrado
        {
            ProductoId = ProductoId,
            SucursalOrigenId = SucursalId, SucursalDestinoId = 2,
            Cantidad = 7, CostoUnitario = 100, CostoTotal = 700,
            NumeroTraslado = "TRL-100"
        }));

        var lotes = await QueryAsync(db => db.LotesInventario.AsNoTracking()
            .Where(l => l.SucursalId == SucursalId)
            .OrderBy(l => l.FechaEntrada).ToListAsync());

        lotes[0].CantidadDisponible.Should().Be(3, "FIFO consume el lote más antiguo primero");
        lotes[1].CantidadDisponible.Should().Be(10, "el lote nuevo no se toca");
    }

    [Fact]
    public async Task TrasladoSalida_UEPS_ConsumeLoteMasRecientePrimero()
    {
        var loteViejo = DateTime.UtcNow.AddDays(-10);
        var loteNuevo = DateTime.UtcNow.AddDays(-1);

        await SeedAsync(db =>
        {
            db.Sucursales.Add(new Sucursal
            {
                Id = SucursalId, EmpresaId = 1, Nombre = "Origen",
                Direccion = "x", Telefono = "y",
                MetodoCosteo = MetodoCosteo.UEPS
            });
            db.Stock.Add(new Stock
            {
                ProductoId = ProductoId, SucursalId = SucursalId,
                Cantidad = 20, CostoPromedio = 100
            });
            db.LotesInventario.Add(new LoteInventario
            {
                ProductoId = ProductoId, SucursalId = SucursalId,
                CantidadInicial = 10, CantidadDisponible = 10,
                CostoUnitario = 100, FechaEntrada = loteViejo
            });
            db.LotesInventario.Add(new LoteInventario
            {
                ProductoId = ProductoId, SucursalId = SucursalId,
                CantidadInicial = 10, CantidadDisponible = 10,
                CostoUnitario = 200, FechaEntrada = loteNuevo
            });
        });

        await ApplyAsync(EventFor(new TrasladoSalidaRegistrado
        {
            ProductoId = ProductoId,
            SucursalOrigenId = SucursalId, SucursalDestinoId = 2,
            Cantidad = 7, CostoUnitario = 200, CostoTotal = 1400,
            NumeroTraslado = "TRL-200"
        }));

        var lotes = await QueryAsync(db => db.LotesInventario.AsNoTracking()
            .Where(l => l.SucursalId == SucursalId)
            .OrderBy(l => l.FechaEntrada).ToListAsync());

        lotes[0].CantidadDisponible.Should().Be(10, "UEPS no toca el lote viejo si hay disponible nuevo");
        lotes[1].CantidadDisponible.Should().Be(3);
    }

    [Fact]
    public async Task TrasladoSalida_ReduceStockYRegistraMovimiento()
    {
        await SeedAsync(db =>
        {
            db.Sucursales.Add(new Sucursal
            {
                Id = SucursalId, EmpresaId = 1, Nombre = "Origen",
                Direccion = "x", Telefono = "y",
                MetodoCosteo = MetodoCosteo.PromedioPonderado
            });
            db.Stock.Add(new Stock
            {
                ProductoId = ProductoId, SucursalId = SucursalId,
                Cantidad = 50, CostoPromedio = 100
            });
            db.LotesInventario.Add(new LoteInventario
            {
                ProductoId = ProductoId, SucursalId = SucursalId,
                CantidadInicial = 50, CantidadDisponible = 50,
                CostoUnitario = 100, FechaEntrada = DateTime.UtcNow.AddDays(-3)
            });
        });

        await ApplyAsync(EventFor(new TrasladoSalidaRegistrado
        {
            ProductoId = ProductoId,
            SucursalOrigenId = SucursalId, SucursalDestinoId = 2,
            Cantidad = 12, CostoUnitario = 100, CostoTotal = 1200,
            NumeroTraslado = "TRL-300", Observaciones = "test"
        }));

        var stock = await QueryAsync(db => db.Stock.AsNoTracking()
            .FirstAsync(s => s.SucursalId == SucursalId));
        stock.Cantidad.Should().Be(38);

        var mov = await QueryAsync(db => db.MovimientosInventario.AsNoTracking()
            .FirstAsync(m => m.Referencia == "TRL-300"));
        mov.TipoMovimiento.Should().Be(TipoMovimiento.TransferenciaSalida);
        mov.Cantidad.Should().Be(12);
        mov.CostoTotal.Should().Be(1200);
        mov.SucursalDestinoId.Should().Be(2);
    }
}
