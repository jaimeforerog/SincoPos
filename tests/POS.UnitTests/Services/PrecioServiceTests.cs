using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NSubstitute;
using POS.Application.Services;
using POS.Infrastructure.Data;
using POS.Infrastructure.Data.Entities;
using POS.Infrastructure.Services;

namespace POS.UnitTests.Services;

/// <summary>
/// Tests de unidad para PrecioService usando EF Core InMemory + NSubstitute.
/// Verifica la cascada: PrecioSucursal → Producto.PrecioVenta → Costo × (1 + Margen).
/// </summary>
public sealed class PrecioServiceTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly PrecioService _sut;

    // IDs fijos para todos los tests
    private static readonly Guid ProductoId = Guid.NewGuid();
    private const int SucursalId = 1;
    private const int CategoriaId = 10;

    public PrecioServiceTests()
    {
        // NSubstitute: EmpresaId=null → filtros globales pasan todo sin short-circuit EF Core
        var empresaProvider = Substitute.For<ICurrentEmpresaProvider>();
        empresaProvider.EmpresaId.Returns((int?)null);

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _db = new AppDbContext(
            options,
            Substitute.For<IHttpContextAccessor>(),
            empresaProvider,
            Substitute.For<ILogger<AppDbContext>>());
        _sut = new PrecioService(_db);

        SeedCategoria();
    }

    // ── Cascada 1: precio por sucursal ─────────────────────────────────────

    [Fact]
    public async Task ResolverPrecio_CuandoExistePrecioSucursal_RetornaPrecioSucursal()
    {
        SeedProducto(precioVenta: 10_000m);
        SeedPrecioSucursal(precioVenta: 15_000m, precioMinimo: 12_000m);

        var resultado = await _sut.ResolverPrecio(ProductoId, SucursalId);

        resultado.PrecioVenta.Should().Be(15_000m);
        resultado.PrecioMinimo.Should().Be(12_000m);
        resultado.Origen.Should().Be("Sucursal");
    }

    // ── Cascada 2: precio base del producto ────────────────────────────────

    [Fact]
    public async Task ResolverPrecio_SinPrecioSucursal_RetornaPrecioProductoCuandoEsPositivo()
    {
        SeedProducto(precioVenta: 8_000m);
        // Sin PrecioSucursal

        var resultado = await _sut.ResolverPrecio(ProductoId, SucursalId);

        resultado.PrecioVenta.Should().Be(8_000m);
        resultado.PrecioMinimo.Should().BeNull();
        resultado.Origen.Should().Be("Producto");
    }

    // ── Cascada 3: costo × (1 + margen) ───────────────────────────────────

    [Fact]
    public async Task ResolverPrecio_SinPrecioSucursalNiPrecioVenta_CalculaDesdeMargen()
    {
        SeedProducto(precioVenta: 0m, precioCosto: 5_000m); // margen categoría = 30%
        SeedStock(costoPromedio: 6_000m);                   // stock prevalece sobre costo producto

        var resultado = await _sut.ResolverPrecio(ProductoId, SucursalId);

        // 6000 × 1.30 = 7800
        resultado.PrecioVenta.Should().Be(7_800m);
        resultado.Origen.Should().Be("Margen");
    }

    [Fact]
    public async Task ResolverPrecio_SinPrecioSucursalNiPrecioVentaNiStock_UsaCostoProducto()
    {
        SeedProducto(precioVenta: 0m, precioCosto: 5_000m);
        // Sin stock → usa PrecioCosto del producto

        var resultado = await _sut.ResolverPrecio(ProductoId, SucursalId);

        // 5000 × 1.30 = 6500
        resultado.PrecioVenta.Should().Be(6_500m);
        resultado.Origen.Should().Be("Margen");
    }

    // ── Edge cases ─────────────────────────────────────────────────────────

    [Fact]
    public async Task ResolverPrecio_ProductoInexistente_LanzaInvalidOperationException()
    {
        var idInexistente = Guid.NewGuid();

        Func<Task> act = () => _sut.ResolverPrecio(idInexistente, SucursalId);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage($"*{idInexistente}*");
    }

    // ── Helpers ────────────────────────────────────────────────────────────

    private void SeedCategoria() =>
        _db.Categorias.Add(new Categoria
        {
            Id = CategoriaId,
            Nombre = "Test",
            MargenGanancia = 0.30m,
            RutaCompleta = "Test",
        });

    private void SeedProducto(decimal precioVenta, decimal precioCosto = 0m)
    {
        _db.Productos.Add(new Producto
        {
            Id = ProductoId,
            CodigoBarras = "1234567890",
            Nombre = "Producto Test",
            CategoriaId = CategoriaId,
            PrecioVenta = precioVenta,
            PrecioCosto = precioCosto,
            Activo = true,
        });
        _db.SaveChanges();
    }

    private void SeedPrecioSucursal(decimal precioVenta, decimal? precioMinimo = null)
    {
        _db.PreciosSucursal.Add(new PrecioSucursal
        {
            ProductoId = ProductoId,
            SucursalId = SucursalId,
            PrecioVenta = precioVenta,
            PrecioMinimo = precioMinimo,
        });
        _db.SaveChanges();
    }

    private void SeedStock(decimal costoPromedio)
    {
        _db.Stock.Add(new Stock
        {
            ProductoId = ProductoId,
            SucursalId = SucursalId,
            CostoPromedio = costoPromedio,
        });
        _db.SaveChanges();
    }

    public void Dispose() => _db.Dispose();
}
