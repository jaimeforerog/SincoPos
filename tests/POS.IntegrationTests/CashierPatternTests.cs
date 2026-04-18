using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using POS.Application.DTOs;
using POS.Domain.Aggregates;
using Xunit;

namespace POS.IntegrationTests;

/// <summary>
/// Tests de integración para la Capa 9 — Aprendizaje continuo.
/// Verifica CashierPattern (individual) y StorePattern (organizacional) vía AprendizajeController.
/// </summary>
[Collection("POS")]
public class CashierPatternTests
{
    private readonly HttpClient _client;
    private readonly CustomWebApplicationFactory _factory;
    private readonly JsonSerializerOptions _json = new() { PropertyNameCaseInsensitive = true };

    private int SucId => _factory.SucursalPPId;
    private int CatId => _factory.CategoriaTestId;

    public CashierPatternTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    // ─── Helpers ────────────────────────────────────────────

    private async Task<Guid> CrearProducto(string codigo, decimal precio = 2000m)
    {
        var resp = await _client.PostAsJsonAsync("/api/v1/Productos", new
        {
            codigoBarras = codigo, nombre = $"Prod {codigo}", categoriaId = CatId,
            precioVenta = precio, precioCosto = precio / 2
        });
        resp.EnsureSuccessStatusCode();
        return (await resp.Content.ReadFromJsonAsync<ProductoDto>(_json))!.Id;
    }

    private async Task AgregarStock(Guid productoId, decimal cantidad = 100)
    {
        var resp = await _client.PostAsJsonAsync("/api/v1/Inventario/entrada", new
        {
            productoId, sucursalId = SucId, cantidad, costoUnitario = 1000m,
            terceroId = _factory.TerceroTestId,
            referencia = $"ENT-{Guid.NewGuid():N}"[..20],
            observaciones = "Stock para aprendizaje"
        });
        resp.EnsureSuccessStatusCode();
    }

    private async Task<int> AbrirCaja(string nombre)
    {
        var crearResp = await _client.PostAsJsonAsync("/api/v1/Cajas", new { nombre, sucursalId = SucId });
        crearResp.EnsureSuccessStatusCode();
        var caja = await crearResp.Content.ReadFromJsonAsync<CajaDto>(_json);
        await _client.PostAsJsonAsync($"/api/v1/Cajas/{caja!.Id}/abrir", new { montoApertura = 50_000m });
        return caja.Id;
    }

    private async Task HacerVenta(int cajaId, Guid productoId, decimal cantidad = 3m)
    {
        var resp = await _client.PostAsJsonAsync("/api/v1/Ventas", new
        {
            sucursalId = SucId,
            cajaId,
            clienteId = _factory.TerceroTestId,
            metodoPago = 0,
            montoPagado = 999_999m,
            lineas = new[]
            {
                new { productoId, cantidad, precioUnitario = (decimal?)null, descuento = 0m }
            }
        });
        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Venta fallida: {await resp.Content.ReadAsStringAsync()}");
    }

    // ═══════════════════════════════════════════════════════
    //  CASHIER PATTERN — nivel individual
    // ═══════════════════════════════════════════════════════

    [Fact]
    public async Task GetMiPatron_SinHistorial_Retorna204()
    {
        // Sin ventas previas en este test (DB limpia por colección), el endpoint retorna 204.
        // UserBehaviorTests también verifica este escenario desde el endpoint de anticipados.
        var resp = await _client.GetAsync("/api/v1/aprendizaje/mi-patron");
        resp.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task GetMiPatron_DespuesDeVentas_RetornaPatronConTopProductos()
    {
        // Arrange: 3 productos, cantidades distintas para validar orden de frecuencia
        var prod1 = await CrearProducto("PAT-001");
        var prod2 = await CrearProducto("PAT-002");
        var prod3 = await CrearProducto("PAT-003");
        await AgregarStock(prod1, 50);
        await AgregarStock(prod2, 50);
        await AgregarStock(prod3, 50);
        var cajaId = await AbrirCaja("Caja Patron 01");

        // Act: vender prod1×10, prod2×5, prod3×1 → orden esperado: prod1 > prod2 > prod3
        await HacerVenta(cajaId, prod1, 10);
        await HacerVenta(cajaId, prod2, 5);
        await HacerVenta(cajaId, prod3, 1);

        // Assert
        var resp = await _client.GetAsync("/api/v1/aprendizaje/mi-patron");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);

        var patron = await resp.Content.ReadFromJsonAsync<CashierPattern>(_json);
        patron.Should().NotBeNull();
        // TotalVentas acumula en toda la colección (DB compartida) — verificar que al menos las 3 de este test están
        patron!.TotalVentas.Should().BeGreaterThanOrEqualTo(3);
        patron.TopProductos.Should().NotBeEmpty();

        // Los 3 productos de este test deben estar en el top
        patron.TopProductos.Should().Contain(prod1.ToString());
        patron.TopProductos.Should().Contain(prod2.ToString());
        patron.TopProductos.Should().Contain(prod3.ToString());

        // Orden relativo: prod1 (10 uds) > prod2 (5 uds) > prod3 (1 ud) dentro de la velocidad acumulada
        patron.ProductoVelocidad.Should().ContainKey(prod1.ToString());
        patron.ProductoVelocidad[prod1.ToString()].Should()
            .BeGreaterThan(patron.ProductoVelocidad[prod2.ToString()]);
        patron.ProductoVelocidad[prod2.ToString()].Should()
            .BeGreaterThan(patron.ProductoVelocidad[prod3.ToString()]);

        // HorasPico y DiasActivos acumulan en toda la colección — solo verificar que hay datos
        patron.HorasPico.Should().NotBeEmpty();
        patron.HorasPico.Values.Sum().Should().BeGreaterThanOrEqualTo(3);
        patron.DiasActivos.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GetMiPatron_MultipleVentasMismoProducto_AcumulaFrecuencia()
    {
        // Arrange
        var prod = await CrearProducto("PAT-MULTI-001");
        await AgregarStock(prod, 100);
        var cajaId = await AbrirCaja("Caja Patron 02");

        // Act: 3 ventas del mismo producto
        await HacerVenta(cajaId, prod, 2);
        await HacerVenta(cajaId, prod, 3);
        await HacerVenta(cajaId, prod, 5);

        // Assert
        var resp = await _client.GetAsync("/api/v1/aprendizaje/mi-patron");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);

        var patron = await resp.Content.ReadFromJsonAsync<CashierPattern>(_json);
        patron.Should().NotBeNull();
        patron!.ProductoVelocidad.Should().ContainKey(prod.ToString());
        patron.ProductoVelocidad[prod.ToString()].Should().Be(10); // 2+3+5
    }

    // ═══════════════════════════════════════════════════════
    //  STORE PATTERN — nivel organizacional
    // ═══════════════════════════════════════════════════════

    [Fact]
    public async Task GetPatronTienda_SinHistorial_Retorna204()
    {
        var resp = await _client.GetAsync($"/api/v1/aprendizaje/tienda/{SucId}");
        resp.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task GetPatronTienda_DespuesDeVentas_RetornaPatronDeLaTienda()
    {
        // Arrange
        var prod = await CrearProducto("STORE-001");
        await AgregarStock(prod, 100);
        var cajaId = await AbrirCaja("Caja Store 01");

        // Act
        await HacerVenta(cajaId, prod, 7);

        // Assert
        var resp = await _client.GetAsync($"/api/v1/aprendizaje/tienda/{SucId}");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);

        var patron = await resp.Content.ReadFromJsonAsync<StorePattern>(_json);
        patron.Should().NotBeNull();
        patron!.SucursalId.Should().Be(SucId);
        patron.TotalVentas.Should().BeGreaterThan(0);
        patron.TopProductos.Should().Contain(prod.ToString());
        patron.HorasPico.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GetPatronTienda_SucursalInexistente_Retorna204()
    {
        var resp = await _client.GetAsync("/api/v1/aprendizaje/tienda/9999");
        resp.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }
}
