using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using POS.Application.DTOs;

namespace POS.IntegrationTests;

/// <summary>
/// Pruebas de integracion para movimientos de inventario con Event Sourcing.
/// Cada sucursal tiene un metodo de costeo diferente (IDs asignados por DB).
/// </summary>
[Collection("POS")]
public class InventarioCosteoTests
{
    private readonly HttpClient _client;
    private readonly CustomWebApplicationFactory _factory;
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    // IDs de sucursales (asignados por DB en seed)
    private int SucPp => _factory.SucursalPPId;
    private int SucFIFO => _factory.SucursalFIFOId;
    private int SucLIFO => _factory.SucursalLIFOId;
    private int CatId => _factory.CategoriaTestId;
    private int TerceroId => _factory.TerceroTestId;

    public InventarioCosteoTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    // ─── Helpers ────────────────────────────────────────────

    private async Task<Guid> CrearProductoTest(string codigo)
    {
        var dto = new
        {
            codigoBarras = codigo,
            nombre = $"Producto {codigo}",
            descripcion = "Test costeo",
            categoriaId = CatId,
            precioVenta = 1000m,
            precioCosto = 500m
        };
        var response = await _client.PostAsJsonAsync("/api/v1/Productos", dto);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<ProductoDto>(_jsonOptions);
        return result!.Id;
    }

    private async Task<JsonElement> RegistrarEntrada(Guid productoId, int sucursalId,
        decimal cantidad, decimal costoUnitario, string referencia)
    {
        var dto = new
        {
            productoId, sucursalId, cantidad, costoUnitario,
            terceroId = TerceroId, referencia, observaciones = $"Test entrada {referencia}"
        };
        var response = await _client.PostAsJsonAsync("/api/v1/Inventario/entrada", dto);
        response.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Entrada {referencia} deberia ser exitosa. Body: {await response.Content.ReadAsStringAsync()}");
        return await response.Content.ReadFromJsonAsync<JsonElement>(_jsonOptions);
    }

    private async Task<JsonElement> RegistrarDevolucion(Guid productoId, int sucursalId,
        decimal cantidad, string referencia)
    {
        var dto = new
        {
            productoId, sucursalId, cantidad,
            terceroId = TerceroId, referencia, observaciones = $"Test devolucion {referencia}"
        };
        var response = await _client.PostAsJsonAsync("/api/v1/Inventario/devolucion-proveedor", dto);
        response.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Devolucion {referencia} deberia ser exitosa. Body: {await response.Content.ReadAsStringAsync()}");
        return await response.Content.ReadFromJsonAsync<JsonElement>(_jsonOptions);
    }

    private async Task<StockDto?> ObtenerStock(Guid productoId, int sucursalId)
    {
        var response = await _client.GetFromJsonAsync<List<StockDto>>(
            $"/api/v1/Inventario?productoId={productoId}&sucursalId={sucursalId}",
            _jsonOptions);
        return response?.FirstOrDefault();
    }

    private async Task<List<MovimientoInventarioDto>> ObtenerMovimientos(
        Guid productoId, int sucursalId)
    {
        return await _client.GetFromJsonAsync<List<MovimientoInventarioDto>>(
            $"/api/v1/Inventario/movimientos?productoId={productoId}&sucursalId={sucursalId}",
            _jsonOptions) ?? [];
    }

    // ═══════════════════════════════════════════════════════
    //  PROMEDIO PONDERADO
    // ═══════════════════════════════════════════════════════

    [Fact]
    public async Task PromedioPonderado_EntradaUnica_CostoIgualAlUnitario()
    {
        var productoId = await CrearProductoTest("PP-001");
        await RegistrarEntrada(productoId, SucPp, 100, 50, "FC-PP-001");

        var stock = await ObtenerStock(productoId, SucPp);
        stock.Should().NotBeNull();
        stock.Cantidad.Should().Be(100);
        stock.CostoPromedio.Should().Be(50);
    }

    [Fact]
    public async Task PromedioPonderado_MultipleEntradas_CostoPromedioCalculado()
    {
        var productoId = await CrearProductoTest("PP-002");
        await RegistrarEntrada(productoId, SucPp, 100, 50, "FC-PP-002A");
        await RegistrarEntrada(productoId, SucPp, 200, 80, "FC-PP-002B");

        // (100*50 + 200*80) / 300 = 21000/300 = 70
        var stock = await ObtenerStock(productoId, SucPp);
        stock!.Cantidad.Should().Be(300);
        stock.CostoPromedio.Should().BeApproximately(70m, 0.01m);
    }

    [Fact]
    public async Task PromedioPonderado_TresEntradas_Acumulativo()
    {
        var productoId = await CrearProductoTest("PP-003");
        await RegistrarEntrada(productoId, SucPp, 50, 100, "FC-PP-003A");
        await RegistrarEntrada(productoId, SucPp, 150, 200, "FC-PP-003B");
        await RegistrarEntrada(productoId, SucPp, 100, 150, "FC-PP-003C");

        // (50*100 + 150*200 + 100*150) / 300 = 50000/300 = 166.67
        var stock = await ObtenerStock(productoId, SucPp);
        stock!.Cantidad.Should().Be(300);
        stock.CostoPromedio.Should().BeApproximately(166.67m, 0.01m);
    }

    [Fact]
    public async Task PromedioPonderado_DevolucionNoCambiaCosto()
    {
        var productoId = await CrearProductoTest("PP-004");
        await RegistrarEntrada(productoId, SucPp, 100, 50, "FC-PP-004A");
        await RegistrarEntrada(productoId, SucPp, 100, 100, "FC-PP-004B");
        // avg = (100*50 + 100*100)/200 = 75

        await RegistrarDevolucion(productoId, SucPp, 50, "DEV-PP-001");

        var stock = await ObtenerStock(productoId, SucPp);
        stock!.Cantidad.Should().Be(150);
        stock.CostoPromedio.Should().BeApproximately(75m, 0.01m);
    }

    // ═══════════════════════════════════════════════════════
    //  PEPS - FIFO
    // ═══════════════════════════════════════════════════════

    [Fact]
    public async Task PEPS_EntradaUnica_StockYCosto()
    {
        var productoId = await CrearProductoTest("FIFO-001");
        await RegistrarEntrada(productoId, SucFIFO, 100, 50, "FC-FIFO-001");

        var stock = await ObtenerStock(productoId, SucFIFO);
        stock.Should().NotBeNull();
        stock.Cantidad.Should().Be(100);
        stock.CostoPromedio.Should().Be(50);
    }

    [Fact]
    public async Task PEPS_MultipleEntradas_CostoReflectaLotes()
    {
        var productoId = await CrearProductoTest("FIFO-002");
        await RegistrarEntrada(productoId, SucFIFO, 100, 40, "FC-FIFO-002A");
        await RegistrarEntrada(productoId, SucFIFO, 100, 60, "FC-FIFO-002B");

        var stock = await ObtenerStock(productoId, SucFIFO);
        stock!.Cantidad.Should().Be(200);
        stock.CostoPromedio.Should().BeApproximately(50m, 0.01m);
    }

    [Fact]
    public async Task PEPS_DevolucionReduceStock()
    {
        var productoId = await CrearProductoTest("FIFO-003");
        await RegistrarEntrada(productoId, SucFIFO, 80, 30, "FC-FIFO-003A");
        await RegistrarEntrada(productoId, SucFIFO, 120, 50, "FC-FIFO-003B");

        await RegistrarDevolucion(productoId, SucFIFO, 60, "DEV-FIFO-001");

        var stock = await ObtenerStock(productoId, SucFIFO);
        stock!.Cantidad.Should().Be(140);
    }

    [Fact]
    public async Task PEPS_TresLotes_VerificaTrazabilidad()
    {
        var productoId = await CrearProductoTest("FIFO-004");
        await RegistrarEntrada(productoId, SucFIFO, 50, 100, "FC-FIFO-004A");
        await RegistrarEntrada(productoId, SucFIFO, 50, 200, "FC-FIFO-004B");
        await RegistrarEntrada(productoId, SucFIFO, 50, 300, "FC-FIFO-004C");

        await RegistrarDevolucion(productoId, SucFIFO, 60, "DEV-FIFO-002");

        var stock = await ObtenerStock(productoId, SucFIFO);
        stock!.Cantidad.Should().Be(90);

        var movimientos = await ObtenerMovimientos(productoId, SucFIFO);
        movimientos.Should().HaveCount(4);
        movimientos.Should().Contain(m => m.TipoMovimiento == "DevolucionProveedor");
    }

    // ═══════════════════════════════════════════════════════
    //  UEPS - LIFO
    // ═══════════════════════════════════════════════════════

    [Fact]
    public async Task UEPS_EntradaUnica_StockYCosto()
    {
        var productoId = await CrearProductoTest("LIFO-001");
        await RegistrarEntrada(productoId, SucLIFO, 100, 75, "FC-LIFO-001");

        var stock = await ObtenerStock(productoId, SucLIFO);
        stock.Should().NotBeNull();
        stock.Cantidad.Should().Be(100);
        stock.CostoPromedio.Should().Be(75);
    }

    [Fact]
    public async Task UEPS_MultipleEntradas_StockSuma()
    {
        var productoId = await CrearProductoTest("LIFO-002");
        await RegistrarEntrada(productoId, SucLIFO, 100, 40, "FC-LIFO-002A");
        await RegistrarEntrada(productoId, SucLIFO, 100, 80, "FC-LIFO-002B");

        var stock = await ObtenerStock(productoId, SucLIFO);
        stock!.Cantidad.Should().Be(200);
        stock.CostoPromedio.Should().BeApproximately(60m, 0.01m);
    }

    [Fact]
    public async Task UEPS_DevolucionReduceStock()
    {
        var productoId = await CrearProductoTest("LIFO-003");
        await RegistrarEntrada(productoId, SucLIFO, 80, 25, "FC-LIFO-003A");
        await RegistrarEntrada(productoId, SucLIFO, 120, 55, "FC-LIFO-003B");

        await RegistrarDevolucion(productoId, SucLIFO, 70, "DEV-LIFO-001");

        var stock = await ObtenerStock(productoId, SucLIFO);
        stock!.Cantidad.Should().Be(130);
    }

    [Fact]
    public async Task UEPS_TresLotes_VerificaTrazabilidad()
    {
        var productoId = await CrearProductoTest("LIFO-004");
        await RegistrarEntrada(productoId, SucLIFO, 50, 100, "FC-LIFO-004A");
        await RegistrarEntrada(productoId, SucLIFO, 50, 200, "FC-LIFO-004B");
        await RegistrarEntrada(productoId, SucLIFO, 50, 300, "FC-LIFO-004C");

        await RegistrarDevolucion(productoId, SucLIFO, 60, "DEV-LIFO-002");

        var stock = await ObtenerStock(productoId, SucLIFO);
        stock!.Cantidad.Should().Be(90);

        var movimientos = await ObtenerMovimientos(productoId, SucLIFO);
        movimientos.Should().HaveCount(4);
    }


    // ═══════════════════════════════════════════════════════
    //  CROSS-BRANCH
    // ═══════════════════════════════════════════════════════

    [Fact]
    public async Task MismoProducto_DistintasSucursales_CostosIndependientes()
    {
        var productoId = await CrearProductoTest("CROSS-001");

        await RegistrarEntrada(productoId, SucPp, 100, 50, "FC-CROSS-PP");
        await RegistrarEntrada(productoId, SucFIFO, 100, 80, "FC-CROSS-FIFO");
        await RegistrarEntrada(productoId, SucLIFO, 100, 120, "FC-CROSS-LIFO");

        var stockPP = await ObtenerStock(productoId, SucPp);
        var stockFIFO = await ObtenerStock(productoId, SucFIFO);
        var stockLIFO = await ObtenerStock(productoId, SucLIFO);

        stockPP!.Cantidad.Should().Be(100);
        stockPP.CostoPromedio.Should().Be(50);
        stockFIFO!.Cantidad.Should().Be(100);
        stockFIFO.CostoPromedio.Should().Be(80);
        stockLIFO!.Cantidad.Should().Be(100);
        stockLIFO.CostoPromedio.Should().Be(120);
    }

    [Fact]
    public async Task MismoProducto_EntradasDistintas_CostosPorMetodo()
    {
        var productoId = await CrearProductoTest("CROSS-002");

        // Sucursal PP: 100@50 + 100@100 = avg 75
        await RegistrarEntrada(productoId, SucPp, 100, 50, "FC-CR2-PP-A");
        await RegistrarEntrada(productoId, SucPp, 100, 100, "FC-CR2-PP-B");

        // Sucursal FIFO: 100@50 + 100@100 = avg 75 (mismo resultado que PP en este caso)
        await RegistrarEntrada(productoId, SucFIFO, 100, 50, "FC-CR2-FIFO-A");
        await RegistrarEntrada(productoId, SucFIFO, 100, 100, "FC-CR2-FIFO-B");

        var stockPP = await ObtenerStock(productoId, SucPp);
        var stockFIFO = await ObtenerStock(productoId, SucFIFO);

        stockPP!.CostoPromedio.Should().BeApproximately(75m, 0.01m);
        stockFIFO!.CostoPromedio.Should().BeApproximately(75m, 0.01m);
    }

    // ═══════════════════════════════════════════════════════
    //  EVENT SOURCING: trazabilidad
    // ═══════════════════════════════════════════════════════

    [Fact]
    public async Task EventSourcing_MovimientosInmutables()
    {
        var productoId = await CrearProductoTest("ES-001");
        await RegistrarEntrada(productoId, SucPp, 100, 50, "FC-ES-001A");
        await RegistrarEntrada(productoId, SucPp, 50, 80, "FC-ES-001B");
        await RegistrarDevolucion(productoId, SucPp, 25, "DEV-ES-001");

        var movimientos = await ObtenerMovimientos(productoId, SucPp);

        movimientos.Should().HaveCount(3);
        movimientos[0].TipoMovimiento.Should().Be("DevolucionProveedor");
        movimientos[1].TipoMovimiento.Should().Be("EntradaManual");
        movimientos[2].TipoMovimiento.Should().Be("EntradaManual");
        movimientos.Should().OnlyContain(m => m.NombreProducto != "");
    }

    [Fact]
    public async Task EventSourcing_AjusteInventario()
    {
        var productoId = await CrearProductoTest("ES-002");
        await RegistrarEntrada(productoId, SucPp, 100, 50, "FC-ES-002");

        var ajusteDto = new { productoId, sucursalId = SucPp, cantidadNueva = 80m, observaciones = "Conteo fisico" };
        var response = await _client.PostAsJsonAsync("/api/v1/Inventario/ajuste", ajusteDto);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var stock = await ObtenerStock(productoId, SucPp);
        stock!.Cantidad.Should().Be(80);

        var movimientos = await ObtenerMovimientos(productoId, SucPp);
        movimientos.Should().HaveCount(2);
        movimientos.Should().Contain(m => m.TipoMovimiento == "AjusteNegativo");
    }

    // ═══════════════════════════════════════════════════════
    //  VALIDACIONES DE NEGOCIO
    // ═══════════════════════════════════════════════════════

    [Fact]
    public async Task DevolucionSinStock_RetornaBadRequest()
    {
        var productoId = await CrearProductoTest("VAL-001");
        await RegistrarEntrada(productoId, SucPp, 10, 50, "FC-VAL-001");

        var dto = new
        {
            productoId, sucursalId = SucPp, cantidad = 999m,
            terceroId = TerceroId, referencia = "DEV-VAL-001"
        };
        var response = await _client.PostAsJsonAsync("/api/v1/Inventario/devolucion-proveedor", dto);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task EntradaSinProducto_RetornaBadRequest()
    {
        var dto = new
        {
            productoId = Guid.NewGuid(),
            sucursalId = SucPp, cantidad = 100m, costoUnitario = 50m,
            terceroId = TerceroId, referencia = "FC-NOEXISTE"
        };
        var response = await _client.PostAsJsonAsync("/api/v1/Inventario/entrada", dto);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task StockMinimo_Alertas()
    {
        var productoId = await CrearProductoTest("ALERTA-001");
        await RegistrarEntrada(productoId, SucPp, 5, 50, "FC-ALERTA-001");

        var response = await _client.PutAsync(
            $"/api/v1/Inventario/stock-minimo?productoId={productoId}&sucursalId={SucPp}&stockMinimo=10",
            null);
        response.EnsureSuccessStatusCode();

        var alertas = await _client.GetFromJsonAsync<List<AlertaStockDto>>(
            $"/api/v1/Inventario/alertas?sucursalId={SucPp}", _jsonOptions) ?? [];
        alertas.Should().Contain(a => a.ProductoId == productoId);
    }
}
