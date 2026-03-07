using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using POS.Application.DTOs;

namespace POS.IntegrationTests;

/// <summary>
/// Tests de integración para el módulo de Reportes.
/// Verifica: reporte de ventas, inventario valorizado, reporte de caja, dashboard y top productos.
/// </summary>
[Collection("POS")]
public class ReportesTests
{
    private readonly HttpClient _client;
    private readonly CustomWebApplicationFactory _factory;
    private readonly JsonSerializerOptions _json = new() { PropertyNameCaseInsensitive = true };

    private int SucId => _factory.SucursalPPId;
    private int CatId => _factory.CategoriaTestId;
    private int TercId => _factory.TerceroTestId;

    public ReportesTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    // ─── Helpers ────────────────────────────────────────────────

    private async Task<Guid> CrearProducto(string codigo, decimal precioVenta = 2000m, decimal precioCosto = 1000m)
    {
        var r = await _client.PostAsJsonAsync("/api/v1/Productos", new
        {
            codigoBarras = codigo, nombre = $"Prod {codigo}",
            categoriaId = CatId, precioVenta, precioCosto
        });
        r.EnsureSuccessStatusCode();
        return (await r.Content.ReadFromJsonAsync<ProductoDto>(_json))!.Id;
    }

    private async Task RegistrarEntrada(Guid productoId, decimal cantidad, decimal costo = 1000m)
    {
        var r = await _client.PostAsJsonAsync("/api/v1/Inventario/entrada", new
        {
            productoId, sucursalId = SucId, cantidad, costoUnitario = costo,
            terceroId = TercId, referencia = $"REP-{Guid.NewGuid():N}"[..20]
        });
        r.EnsureSuccessStatusCode();
    }

    private async Task<int> CrearAbrirCaja(string nombre)
    {
        var r1 = await _client.PostAsJsonAsync("/api/v1/Cajas", new { nombre, sucursalId = SucId });
        r1.EnsureSuccessStatusCode();
        var caja = await r1.Content.ReadFromJsonAsync<CajaDto>(_json);
        var r2 = await _client.PostAsJsonAsync($"/api/v1/Cajas/{caja!.Id}/abrir", new { montoApertura = 50_000m });
        r2.EnsureSuccessStatusCode();
        return caja.Id;
    }

    private async Task<int> CrearVenta(int cajaId, Guid productoId, int cantidad = 2)
    {
        var r = await _client.PostAsJsonAsync("/api/v1/Ventas", new
        {
            sucursalId = SucId,
            cajaId,
            metodoPago = 0,  // 0 = Efectivo
            montoPagado = 999_999m,
            lineas = new[] { new { productoId, cantidad = (decimal)cantidad, precioUnitario = (decimal?)null, descuento = 0m } }
        });
        r.EnsureSuccessStatusCode();
        var venta = await r.Content.ReadFromJsonAsync<VentaDto>(_json);
        return venta!.Id;
    }

    // ─── Reporte de Ventas ────────────────────────────────────────

    [Fact]
    public async Task ReporteVentas_SinVentas_RetornaReporteVacio()
    {
        var desde = DateTime.UtcNow.AddYears(-10).ToString("yyyy-MM-ddTHH:mm:ssZ");
        var hasta = DateTime.UtcNow.AddYears(-9).ToString("yyyy-MM-ddTHH:mm:ssZ");

        var r = await _client.GetAsync($"/api/v1/Reportes/ventas?fechaDesde={desde}&fechaHasta={hasta}");

        r.StatusCode.Should().Be(HttpStatusCode.OK);
        var reporte = await r.Content.ReadFromJsonAsync<ReporteVentasDto>(_json);
        reporte!.TotalVentas.Should().Be(0);
        reporte.CantidadVentas.Should().Be(0);
        reporte.VentasPorMetodoPago.Should().BeEmpty();
        reporte.VentasPorDia.Should().BeEmpty();
    }

    [Fact]
    public async Task ReporteVentas_ConVentas_CalculaTotalesCorrectamente()
    {
        // Arrange
        var productoCod = $"REP-V-{Guid.NewGuid():N}"[..15];
        var productoId = await CrearProducto(productoCod, precioVenta: 3000m, precioCosto: 1500m);
        await RegistrarEntrada(productoId, 20, costo: 1500m);
        var cajaId = await CrearAbrirCaja($"Caja-RepVentas-{productoCod}");
        await CrearVenta(cajaId, productoId, cantidad: 3); // Total = 9000

        var desde = DateTime.UtcNow.AddMinutes(-5).ToString("yyyy-MM-ddTHH:mm:ssZ");
        var hasta = DateTime.UtcNow.AddMinutes(5).ToString("yyyy-MM-ddTHH:mm:ssZ");

        // Act
        var r = await _client.GetAsync($"/api/v1/Reportes/ventas?fechaDesde={desde}&fechaHasta={hasta}&sucursalId={SucId}");

        // Assert
        r.StatusCode.Should().Be(HttpStatusCode.OK);
        var reporte = await r.Content.ReadFromJsonAsync<ReporteVentasDto>(_json);
        reporte!.CantidadVentas.Should().BeGreaterThanOrEqualTo(1);
        reporte.TotalVentas.Should().BeGreaterThan(0);
        reporte.TicketPromedio.Should().BeGreaterThan(0);
        reporte.CostoTotal.Should().BeGreaterThan(0);
        reporte.UtilidadTotal.Should().BeGreaterThan(0);
        reporte.VentasPorMetodoPago.Should().ContainSingle(m => m.Metodo == "Efectivo");
        reporte.VentasPorDia.Should().HaveCountGreaterThan(0);
    }

    [Fact]
    public async Task ReporteVentas_FechaInvalida_Retorna400()
    {
        var desde = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ");
        var hasta = DateTime.UtcNow.AddDays(-1).ToString("yyyy-MM-ddTHH:mm:ssZ");

        var r = await _client.GetAsync($"/api/v1/Reportes/ventas?fechaDesde={desde}&fechaHasta={hasta}");

        r.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ─── Inventario Valorizado ────────────────────────────────────────

    [Fact]
    public async Task InventarioValorizado_ConStock_RetornaProductosYTotales()
    {
        // Arrange
        var cod = $"REP-INV-{Guid.NewGuid():N}"[..15];
        var productoId = await CrearProducto(cod, precioVenta: 5000m, precioCosto: 2000m);
        await RegistrarEntrada(productoId, 10, costo: 2000m);

        // Act
        var r = await _client.GetAsync($"/api/v1/Reportes/inventario-valorizado?sucursalId={SucId}&soloConStock=true");

        // Assert
        r.StatusCode.Should().Be(HttpStatusCode.OK);
        var reporte = await r.Content.ReadFromJsonAsync<ReporteInventarioValorizadoDto>(_json);
        reporte!.TotalProductos.Should().BeGreaterThan(0);
        reporte.TotalUnidades.Should().BeGreaterThan(0);
        reporte.TotalCosto.Should().BeGreaterThan(0);
        reporte.TotalVenta.Should().BeGreaterThan(0);
        reporte.UtilidadPotencial.Should().BeGreaterThan(0);
        reporte.Productos.Should().Contain(p => p.ProductoId == productoId);

        var prod = reporte.Productos.First(p => p.ProductoId == productoId);
        prod.Cantidad.Should().Be(10);
        prod.CostoPromedio.Should().Be(2000m);
        prod.CostoTotal.Should().Be(20_000m);
        prod.ValorVenta.Should().Be(50_000m);
    }

    [Fact]
    public async Task InventarioValorizado_SinFiltros_RetornaTodosLosProductos()
    {
        var r = await _client.GetAsync("/api/v1/Reportes/inventario-valorizado");

        r.StatusCode.Should().Be(HttpStatusCode.OK);
        var reporte = await r.Content.ReadFromJsonAsync<ReporteInventarioValorizadoDto>(_json);
        reporte.Should().NotBeNull();
        reporte!.TotalCosto.Should().Be(reporte.Productos.Sum(p => p.CostoTotal));
        reporte.TotalVenta.Should().Be(reporte.Productos.Sum(p => p.ValorVenta));
    }

    // ─── Reporte de Caja ────────────────────────────────────────

    [Fact]
    public async Task ReporteCaja_CajaInexistente_Retorna404()
    {
        var r = await _client.GetAsync("/api/v1/Reportes/caja/999999");
        r.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ReporteCaja_CajaAbiertaConVentas_RetornaTotalesCorrectamente()
    {
        // Arrange
        var cod = $"REP-CAJA-{Guid.NewGuid():N}"[..14];
        var productoId = await CrearProducto(cod, precioVenta: 4000m, precioCosto: 2000m);
        await RegistrarEntrada(productoId, 20, costo: 2000m);
        var cajaId = await CrearAbrirCaja($"Caja-Rep-{cod}");
        await CrearVenta(cajaId, productoId, cantidad: 2); // 2 x 4000 = 8000

        // Act
        var r = await _client.GetAsync($"/api/v1/Reportes/caja/{cajaId}");

        // Assert
        r.StatusCode.Should().Be(HttpStatusCode.OK);
        var reporte = await r.Content.ReadFromJsonAsync<ReporteCajaDto>(_json);
        reporte!.CajaId.Should().Be(cajaId);
        reporte.SucursalId.Should().Be(SucId);
        reporte.TotalVentas.Should().BeGreaterThan(0);
        reporte.TotalVentasEfectivo.Should().Be(reporte.TotalVentas); // Todas en efectivo
        reporte.TotalVentasTarjeta.Should().Be(0);
        reporte.Ventas.Should().HaveCountGreaterThan(0);
        reporte.Ventas.First().MetodoPago.Should().Be("Efectivo");
        reporte.MontoCierre.Should().BeNull(); // No cerrada aún
    }

    [Fact]
    public async Task ReporteCaja_VentasCalculanUtilidad()
    {
        // Arrange
        var cod = $"REP-UTIL-{Guid.NewGuid():N}"[..14];
        var productoId = await CrearProducto(cod, precioVenta: 6000m, precioCosto: 3000m);
        await RegistrarEntrada(productoId, 10, costo: 3000m);
        var cajaId = await CrearAbrirCaja($"Caja-Util-{cod}");
        await CrearVenta(cajaId, productoId, cantidad: 1); // Venta: 6000, Costo: 3000

        var r = await _client.GetAsync($"/api/v1/Reportes/caja/{cajaId}");

        r.StatusCode.Should().Be(HttpStatusCode.OK);
        var reporte = await r.Content.ReadFromJsonAsync<ReporteCajaDto>(_json);
        var venta = reporte!.Ventas.First();
        venta.CostoTotal.Should().BeGreaterThan(0);
        venta.Utilidad.Should().BeGreaterThan(0);
        venta.Utilidad.Should().Be(venta.Total - venta.CostoTotal);
    }

    // ─── Dashboard ────────────────────────────────────────

    [Fact]
    public async Task Dashboard_SinFiltros_RetornaEstructuraCompleta()
    {
        var r = await _client.GetAsync("/api/v1/Reportes/dashboard");

        r.StatusCode.Should().Be(HttpStatusCode.OK);
        var dashboard = await r.Content.ReadFromJsonAsync<DashboardDto>(_json);
        dashboard.Should().NotBeNull();
        dashboard!.MetricasDelDia.Should().NotBeNull();
        dashboard.VentasPorHora.Should().NotBeNull();
        dashboard.TopProductos.Should().NotBeNull();
        dashboard.AlertasStock.Should().NotBeNull();
    }

    [Fact]
    public async Task Dashboard_ConSucursal_FiltraPorSucursal()
    {
        var r = await _client.GetAsync($"/api/v1/Reportes/dashboard?sucursalId={SucId}");
        r.StatusCode.Should().Be(HttpStatusCode.OK);
        var dashboard = await r.Content.ReadFromJsonAsync<DashboardDto>(_json);
        dashboard.Should().NotBeNull();
    }

    [Fact]
    public async Task Dashboard_MetricasTotalesConsistentes()
    {
        // Arrange: crear venta del día para que metricas no sean vacías
        var cod = $"DASH-{Guid.NewGuid():N}"[..14];
        var productoId = await CrearProducto(cod, precioVenta: 3000m, precioCosto: 1000m);
        await RegistrarEntrada(productoId, 10, costo: 1000m);
        var cajaId = await CrearAbrirCaja($"Caja-Dash-{cod}");
        await CrearVenta(cajaId, productoId, cantidad: 2);

        var r = await _client.GetAsync($"/api/v1/Reportes/dashboard?sucursalId={SucId}");

        r.StatusCode.Should().Be(HttpStatusCode.OK);
        var dashboard = await r.Content.ReadFromJsonAsync<DashboardDto>(_json);
        var m = dashboard!.MetricasDelDia;
        m.CantidadVentas.Should().BeGreaterThan(0);
        m.VentasTotales.Should().BeGreaterThan(0);
        m.ProductosVendidos.Should().BeGreaterThan(0);
        m.UtilidadDelDia.Should().BeGreaterThan(0);
        m.TicketPromedio.Should().Be(m.VentasTotales / m.CantidadVentas);
    }

    // ─── Top Productos ────────────────────────────────────────

    [Fact]
    public async Task TopProductos_ConVentas_RetornaOrdenadosPorCantidad()
    {
        // Arrange: dos productos con distinta cantidad vendida
        var cod1 = $"TOP1-{Guid.NewGuid():N}"[..14];
        var cod2 = $"TOP2-{Guid.NewGuid():N}"[..14];
        var prod1 = await CrearProducto(cod1, precioVenta: 2000m, precioCosto: 800m);
        var prod2 = await CrearProducto(cod2, precioVenta: 2000m, precioCosto: 800m);
        await RegistrarEntrada(prod1, 20, costo: 800m);
        await RegistrarEntrada(prod2, 20, costo: 800m);
        var cajaId = await CrearAbrirCaja($"Caja-Top-{cod1}");
        await CrearVenta(cajaId, prod1, cantidad: 5);
        await CrearVenta(cajaId, prod2, cantidad: 2);

        var desde = DateTime.UtcNow.AddMinutes(-5).ToString("yyyy-MM-ddTHH:mm:ssZ");
        var hasta = DateTime.UtcNow.AddMinutes(5).ToString("yyyy-MM-ddTHH:mm:ssZ");

        var r = await _client.GetAsync(
            $"/api/v1/Reportes/top-productos?fechaDesde={desde}&fechaHasta={hasta}&sucursalId={SucId}&limite=10");

        r.StatusCode.Should().Be(HttpStatusCode.OK);
        var top = await r.Content.ReadFromJsonAsync<List<TopProductoDto>>(_json);
        top.Should().NotBeEmpty();

        // Verificar orden descendente por cantidad
        for (int i = 0; i < top!.Count - 1; i++)
            top[i].CantidadVendida.Should().BeGreaterThanOrEqualTo(top[i + 1].CantidadVendida);
    }

    [Fact]
    public async Task TopProductos_FechaInvalida_Retorna400()
    {
        var desde = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ");
        var hasta = DateTime.UtcNow.AddDays(-1).ToString("yyyy-MM-ddTHH:mm:ssZ");

        var r = await _client.GetAsync($"/api/v1/Reportes/top-productos?fechaDesde={desde}&fechaHasta={hasta}");
        r.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task TopProductos_CodigoBarrasYCategoriaPresentesEnResultado()
    {
        var cod = $"TOP-CB-{Guid.NewGuid():N}"[..14];
        var productoId = await CrearProducto(cod, precioVenta: 2500m, precioCosto: 1000m);
        await RegistrarEntrada(productoId, 10, costo: 1000m);
        var cajaId = await CrearAbrirCaja($"Caja-CB-{cod}");
        await CrearVenta(cajaId, productoId, cantidad: 1);

        var desde = DateTime.UtcNow.AddMinutes(-5).ToString("yyyy-MM-ddTHH:mm:ssZ");
        var hasta = DateTime.UtcNow.AddMinutes(5).ToString("yyyy-MM-ddTHH:mm:ssZ");

        var r = await _client.GetAsync(
            $"/api/v1/Reportes/top-productos?fechaDesde={desde}&fechaHasta={hasta}&sucursalId={SucId}&limite=1000");
        r.StatusCode.Should().Be(HttpStatusCode.OK);
        var top = await r.Content.ReadFromJsonAsync<List<TopProductoDto>>(_json);

        var prod = top!.FirstOrDefault(p => p.ProductoId == productoId);
        prod.Should().NotBeNull();
        prod!.CodigoBarras.Should().NotBeNullOrEmpty();
        prod.Categoria.Should().NotBeNullOrEmpty();
    }
}
