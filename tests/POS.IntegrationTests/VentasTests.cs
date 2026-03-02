using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using POS.Application.DTOs;

namespace POS.IntegrationTests;

/// <summary>
/// Pruebas de integración para el módulo de Ventas.
/// Verifica: venta simple, precio por sucursal, validaciones (caja cerrada, sin stock), y anulación.
/// </summary>
[Collection("POS")]
public class VentasTests
{
    private readonly HttpClient _client;
    private readonly CustomWebApplicationFactory _factory;
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private int SucPP => _factory.SucursalPPId;
    private int CatId => _factory.CategoriaTestId;
    private int TerceroId => _factory.TerceroTestId;

    public VentasTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    // ─── Helpers ────────────────────────────────────────────

    private async Task<Guid> CrearProductoTest(string codigo, decimal precioVenta = 1000m, decimal precioCosto = 500m)
    {
        var dto = new
        {
            codigoBarras = codigo,
            nombre = $"Producto {codigo}",
            descripcion = "Test ventas",
            categoriaId = CatId,
            precioVenta,
            precioCosto
        };
        var response = await _client.PostAsJsonAsync("/api/Productos", dto);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<ProductoDto>(_jsonOptions);
        return result!.Id;
    }

    private async Task RegistrarEntradaInventario(Guid productoId, int sucursalId,
        decimal cantidad, decimal costoUnitario)
    {
        var dto = new
        {
            productoId, sucursalId, cantidad, costoUnitario,
            terceroId = TerceroId, referencia = $"FC-VENTA-{Guid.NewGuid():N}"[..20],
            observaciones = "Entrada para test de ventas"
        };
        var response = await _client.PostAsJsonAsync("/api/Inventario/entrada", dto);
        response.EnsureSuccessStatusCode();
    }

    private async Task<int> CrearYAbrirCaja(int sucursalId, string nombre, decimal montoApertura = 100_000m)
    {
        // Crear caja
        var crearResponse = await _client.PostAsJsonAsync("/api/Cajas", new
        {
            nombre,
            sucursalId
        });
        crearResponse.EnsureSuccessStatusCode();
        var caja = await crearResponse.Content.ReadFromJsonAsync<CajaDto>(_jsonOptions);
        var cajaId = caja!.Id;

        // Abrir caja
        var abrirResponse = await _client.PostAsJsonAsync($"/api/Cajas/{cajaId}/abrir", new
        {
            montoApertura
        });
        abrirResponse.EnsureSuccessStatusCode();

        return cajaId;
    }

    private async Task<StockDto?> ObtenerStock(Guid productoId, int sucursalId)
    {
        var response = await _client.GetFromJsonAsync<List<StockDto>>(
            $"/api/Inventario?productoId={productoId}&sucursalId={sucursalId}",
            _jsonOptions);
        return response?.FirstOrDefault();
    }

    // ═══════════════════════════════════════════════════════
    //  VENTA SIMPLE
    // ═══════════════════════════════════════════════════════

    [Fact]
    public async Task VentaSimple_StockSeReduceCorrectamente()
    {
        // Arrange: producto + stock + caja abierta
        var productoId = await CrearProductoTest("VENTA-001", precioVenta: 1500m, precioCosto: 800m);
        await RegistrarEntradaInventario(productoId, SucPP, 50, 800);
        var cajaId = await CrearYAbrirCaja(SucPP, "Caja Venta 001");

        var stockAntes = await ObtenerStock(productoId, SucPP);
        stockAntes.Should().NotBeNull();
        stockAntes!.Cantidad.Should().Be(50);

        // Act: crear venta de 10 unidades
        var ventaDto = new
        {
            sucursalId = SucPP,
            cajaId,
            metodoPago = 0, // Efectivo
            montoPagado = 20000m,
            lineas = new[]
            {
                new { productoId, cantidad = 10m, precioUnitario = (decimal?)null, descuento = 0m }
            }
        };
        var response = await _client.PostAsJsonAsync("/api/Ventas", ventaDto);
        response.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Venta deberia ser exitosa. Body: {await response.Content.ReadAsStringAsync()}");

        var venta = await response.Content.ReadFromJsonAsync<VentaDto>(_jsonOptions);

        // Assert: venta creada correctamente
        venta.Should().NotBeNull();
        venta!.NumeroVenta.Should().StartWith("V-");
        venta.Estado.Should().Be("Completada");
        venta.Detalles.Should().HaveCount(1);
        venta.Detalles[0].Cantidad.Should().Be(10);
        venta.Total.Should().BeGreaterThan(0);

        // Assert: stock reducido
        var stockDespues = await ObtenerStock(productoId, SucPP);
        stockDespues!.Cantidad.Should().Be(40);
    }

    [Fact]
    public async Task VentaSimple_TotalCalculadoCorrectamente()
    {
        // Arrange
        var productoId = await CrearProductoTest("VENTA-002", precioVenta: 2000m, precioCosto: 1000m);
        await RegistrarEntradaInventario(productoId, SucPP, 100, 1000);
        var cajaId = await CrearYAbrirCaja(SucPP, "Caja Venta 002");

        // Act: venta de 5 unidades a precio manual de 2500
        var ventaDto = new
        {
            sucursalId = SucPP,
            cajaId,
            metodoPago = 0,
            montoPagado = 15000m,
            lineas = new[]
            {
                new { productoId, cantidad = 5m, precioUnitario = (decimal?)2500m, descuento = 0m }
            }
        };
        var response = await _client.PostAsJsonAsync("/api/Ventas", ventaDto);
        response.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Body: {await response.Content.ReadAsStringAsync()}");

        var venta = await response.Content.ReadFromJsonAsync<VentaDto>(_jsonOptions);

        // Assert: 5 × 2500 = 12500
        venta!.Subtotal.Should().Be(12500m);
        venta.Total.Should().Be(12500m);
        venta.Cambio.Should().Be(2500m); // 15000 - 12500
    }

    // ═══════════════════════════════════════════════════════
    //  PRECIO POR SUCURSAL
    // ═══════════════════════════════════════════════════════

    [Fact]
    public async Task VentaConPrecioSucursal_UsaPrecioCorrecto()
    {
        // Arrange
        var productoId = await CrearProductoTest("VENTA-003", precioVenta: 1000m, precioCosto: 500m);
        await RegistrarEntradaInventario(productoId, SucPP, 100, 500);
        var cajaId = await CrearYAbrirCaja(SucPP, "Caja Venta 003");

        // Configurar precio por sucursal (mayor que el precio base del producto)
        var precioSucResponse = await _client.PostAsJsonAsync("/api/Precios", new
        {
            productoId,
            sucursalId = SucPP,
            precioVenta = 1800m,
            precioMinimo = 1500m
        });
        precioSucResponse.EnsureSuccessStatusCode();

        // Act: venta sin precio manual → debe usar precio de sucursal (1800)
        var ventaDto = new
        {
            sucursalId = SucPP,
            cajaId,
            metodoPago = 0,
            montoPagado = 20000m,
            lineas = new[]
            {
                new { productoId, cantidad = 5m, precioUnitario = (decimal?)null, descuento = 0m }
            }
        };
        var response = await _client.PostAsJsonAsync("/api/Ventas", ventaDto);
        response.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Body: {await response.Content.ReadAsStringAsync()}");

        var venta = await response.Content.ReadFromJsonAsync<VentaDto>(_jsonOptions);

        // Assert: debe usar precio de sucursal 1800, no el del producto 1000
        venta!.Detalles[0].PrecioUnitario.Should().Be(1800m);
        venta.Subtotal.Should().Be(9000m); // 5 × 1800
    }

    // ═══════════════════════════════════════════════════════
    //  VALIDACIONES
    // ═══════════════════════════════════════════════════════

    [Fact]
    public async Task VentaConCajaCerrada_RetornaBadRequest()
    {
        // Arrange: producto + stock, caja creada pero NO abierta
        var productoId = await CrearProductoTest("VENTA-004", precioVenta: 1000m);
        await RegistrarEntradaInventario(productoId, SucPP, 50, 500);

        // Crear caja sin abrirla
        var crearCaja = await _client.PostAsJsonAsync("/api/Cajas", new
        {
            nombre = "Caja Cerrada Test",
            sucursalId = SucPP
        });
        crearCaja.EnsureSuccessStatusCode();
        var caja = await crearCaja.Content.ReadFromJsonAsync<CajaDto>(_jsonOptions);

        // Act: intentar vender con caja cerrada
        var ventaDto = new
        {
            sucursalId = SucPP,
            cajaId = caja!.Id,
            metodoPago = 0,
            montoPagado = 5000m,
            lineas = new[]
            {
                new { productoId, cantidad = 5m, precioUnitario = (decimal?)null, descuento = 0m }
            }
        };
        var response = await _client.PostAsJsonAsync("/api/Ventas", ventaDto);

        // Assert: debe rechazar
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task VentaSinStockSuficiente_RetornaBadRequest()
    {
        // Arrange: producto con solo 5 unidades
        var productoId = await CrearProductoTest("VENTA-005", precioVenta: 1000m);
        await RegistrarEntradaInventario(productoId, SucPP, 5, 500);
        var cajaId = await CrearYAbrirCaja(SucPP, "Caja Venta 005");

        // Act: intentar vender 50 unidades (solo hay 5)
        var ventaDto = new
        {
            sucursalId = SucPP,
            cajaId,
            metodoPago = 0,
            montoPagado = 50000m,
            lineas = new[]
            {
                new { productoId, cantidad = 50m, precioUnitario = (decimal?)null, descuento = 0m }
            }
        };
        var response = await _client.PostAsJsonAsync("/api/Ventas", ventaDto);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task VentaSinLineas_RetornaBadRequest()
    {
        var cajaId = await CrearYAbrirCaja(SucPP, "Caja Venta 006");

        var ventaDto = new
        {
            sucursalId = SucPP,
            cajaId,
            metodoPago = 0,
            montoPagado = 1000m,
            lineas = Array.Empty<object>()
        };
        var response = await _client.PostAsJsonAsync("/api/Ventas", ventaDto);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task VentaConMontoPagadoInsuficiente_RetornaBadRequest()
    {
        var productoId = await CrearProductoTest("VENTA-007", precioVenta: 5000m);
        await RegistrarEntradaInventario(productoId, SucPP, 10, 2000);
        var cajaId = await CrearYAbrirCaja(SucPP, "Caja Venta 007");

        // Monto pagado = 1000, pero total = 5000 × 2 = 10000
        var ventaDto = new
        {
            sucursalId = SucPP,
            cajaId,
            metodoPago = 0,
            montoPagado = 1000m,
            lineas = new[]
            {
                new { productoId, cantidad = 2m, precioUnitario = (decimal?)null, descuento = 0m }
            }
        };
        var response = await _client.PostAsJsonAsync("/api/Ventas", ventaDto);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ═══════════════════════════════════════════════════════
    //  ANULACIÓN
    // ═══════════════════════════════════════════════════════

    [Fact]
    public async Task AnulacionDeVenta_StockSeRestaura()
    {
        // Arrange: producto con 100 unidades
        var productoId = await CrearProductoTest("VENTA-008", precioVenta: 1000m, precioCosto: 500m);
        await RegistrarEntradaInventario(productoId, SucPP, 100, 500);
        var cajaId = await CrearYAbrirCaja(SucPP, "Caja Venta 008");

        // Act 1: crear venta de 30 unidades → stock = 70
        var ventaDto = new
        {
            sucursalId = SucPP,
            cajaId,
            metodoPago = 0,
            montoPagado = 50000m,
            lineas = new[]
            {
                new { productoId, cantidad = 30m, precioUnitario = (decimal?)null, descuento = 0m }
            }
        };
        var crearResponse = await _client.PostAsJsonAsync("/api/Ventas", ventaDto);
        crearResponse.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Body: {await crearResponse.Content.ReadAsStringAsync()}");

        var venta = await crearResponse.Content.ReadFromJsonAsync<VentaDto>(_jsonOptions);
        var ventaId = venta!.Id;

        var stockDespuesVenta = await ObtenerStock(productoId, SucPP);
        stockDespuesVenta!.Cantidad.Should().Be(70);

        // Act 2: anular la venta → stock debe volver a 100
        var anularResponse = await _client.PostAsync(
            $"/api/Ventas/{ventaId}/anular?motivo=Test anulacion", null);
        anularResponse.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Body: {await anularResponse.Content.ReadAsStringAsync()}");

        // Assert: stock restaurado
        var stockDespuesAnulacion = await ObtenerStock(productoId, SucPP);
        stockDespuesAnulacion!.Cantidad.Should().Be(100);
    }

    [Fact]
    public async Task AnulacionDeVentaAnulada_RetornaBadRequest()
    {
        // Arrange
        var productoId = await CrearProductoTest("VENTA-009", precioVenta: 1000m);
        await RegistrarEntradaInventario(productoId, SucPP, 50, 500);
        var cajaId = await CrearYAbrirCaja(SucPP, "Caja Venta 009");

        var ventaDto = new
        {
            sucursalId = SucPP,
            cajaId,
            metodoPago = 0,
            montoPagado = 10000m,
            lineas = new[]
            {
                new { productoId, cantidad = 5m, precioUnitario = (decimal?)null, descuento = 0m }
            }
        };
        var crearResponse = await _client.PostAsJsonAsync("/api/Ventas", ventaDto);
        crearResponse.EnsureSuccessStatusCode();
        var venta = await crearResponse.Content.ReadFromJsonAsync<VentaDto>(_jsonOptions);

        // Anular la primera vez → OK
        var anular1 = await _client.PostAsync($"/api/Ventas/{venta!.Id}/anular?motivo=Primera", null);
        anular1.EnsureSuccessStatusCode();

        // Act: intentar anular de nuevo
        var anular2 = await _client.PostAsync($"/api/Ventas/{venta.Id}/anular?motivo=Segunda", null);

        // Assert
        anular2.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ═══════════════════════════════════════════════════════
    //  CONSULTAS
    // ═══════════════════════════════════════════════════════

    [Fact]
    public async Task ObtenerVentaPorId_RetornaDetalleCompleto()
    {
        // Arrange
        var productoId = await CrearProductoTest("VENTA-010", precioVenta: 2000m, precioCosto: 1000m);
        await RegistrarEntradaInventario(productoId, SucPP, 100, 1000);
        var cajaId = await CrearYAbrirCaja(SucPP, "Caja Venta 010");

        var ventaDto = new
        {
            sucursalId = SucPP,
            cajaId,
            metodoPago = 1, // Tarjeta
            lineas = new[]
            {
                new { productoId, cantidad = 3m, precioUnitario = (decimal?)null, descuento = 0m }
            }
        };
        var crearResponse = await _client.PostAsJsonAsync("/api/Ventas", ventaDto);
        crearResponse.EnsureSuccessStatusCode();
        var ventaCreada = await crearResponse.Content.ReadFromJsonAsync<VentaDto>(_jsonOptions);

        // Act
        var response = await _client.GetAsync($"/api/Ventas/{ventaCreada!.Id}");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var venta = await response.Content.ReadFromJsonAsync<VentaDto>(_jsonOptions);

        // Assert
        venta.Should().NotBeNull();
        venta!.Id.Should().Be(ventaCreada.Id);
        venta.NumeroVenta.Should().NotBeEmpty();
        venta.MetodoPago.Should().Be("Tarjeta");
        venta.Detalles.Should().HaveCount(1);
        venta.Detalles[0].NombreProducto.Should().Contain("VENTA-010");
        venta.Detalles[0].MargenGanancia.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task ListarVentas_FiltrosPorSucursal()
    {
        // Act
        var response = await _client.GetAsync($"/api/Ventas?sucursalId={SucPP}&limite=100");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var ventas = await response.Content.ReadFromJsonAsync<List<VentaDto>>(_jsonOptions);

        // Assert: las ventas creadas en otros tests deben aparecer aquí
        ventas.Should().NotBeNull();
        ventas!.Should().OnlyContain(v => v.SucursalId == SucPP);
    }

    [Fact]
    public async Task ResumenDeVentas_RetornaTotales()
    {
        // Act
        var response = await _client.GetAsync($"/api/Ventas/resumen?sucursalId={SucPP}");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var resumen = await response.Content.ReadFromJsonAsync<ResumenVentaDto>(_jsonOptions);

        // Assert
        resumen.Should().NotBeNull();
        resumen!.TotalVentas.Should().BeGreaterThanOrEqualTo(0);
        resumen.MontoTotal.Should().BeGreaterThanOrEqualTo(0);
    }
}
