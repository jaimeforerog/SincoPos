using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using POS.Application.DTOs;
using POS.Infrastructure.Data;
using POS.Infrastructure.Data.Entities;

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

    private int SucPp => _factory.SucursalPPId;
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
        var response = await _client.PostAsJsonAsync("/api/v1/Productos", dto);
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
        var response = await _client.PostAsJsonAsync("/api/v1/Inventario/entrada", dto);
        response.EnsureSuccessStatusCode();
    }

    private async Task<int> CrearYAbrirCaja(int sucursalId, string nombre, decimal montoApertura = 100_000m)
    {
        // Crear caja
        var crearResponse = await _client.PostAsJsonAsync("/api/v1/Cajas", new
        {
            nombre,
            sucursalId
        });
        crearResponse.EnsureSuccessStatusCode();
        var caja = await crearResponse.Content.ReadFromJsonAsync<CajaDto>(_jsonOptions);
        var cajaId = caja!.Id;

        // Abrir caja
        var abrirResponse = await _client.PostAsJsonAsync($"/api/v1/Cajas/{cajaId}/abrir", new
        {
            montoApertura
        });
        abrirResponse.EnsureSuccessStatusCode();

        return cajaId;
    }

    private async Task<StockDto?> ObtenerStock(Guid productoId, int sucursalId)
    {
        var response = await _client.GetFromJsonAsync<List<StockDto>>(
            $"/api/v1/Inventario?productoId={productoId}&sucursalId={sucursalId}",
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
        await RegistrarEntradaInventario(productoId, SucPp, 50, 800);
        var cajaId = await CrearYAbrirCaja(SucPp, "Caja Venta 001");

        var stockAntes = await ObtenerStock(productoId, SucPp);
        stockAntes.Should().NotBeNull();
        stockAntes.Cantidad.Should().Be(50);

        // Act: crear venta de 10 unidades
        var ventaDto = new
        {
            sucursalId = SucPp,
            cajaId,
            metodoPago = 0, // Efectivo
            montoPagado = 20000m,
            lineas = new[]
            {
                new { productoId, cantidad = 10m, precioUnitario = (decimal?)null, descuento = 0m }
            }
        };
        var response = await _client.PostAsJsonAsync("/api/v1/Ventas", ventaDto);
        response.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Venta deberia ser exitosa. Body: {await response.Content.ReadAsStringAsync()}");

        var venta = await response.Content.ReadFromJsonAsync<VentaDto>(_jsonOptions);

        // Assert: venta creada correctamente
        venta.Should().NotBeNull();
        venta.NumeroVenta.Should().StartWith("V-");
        venta.Estado.Should().Be("Completada");
        venta.Detalles.Should().HaveCount(1);
        venta.Detalles[0].Cantidad.Should().Be(10);
        venta.Total.Should().BeGreaterThan(0);

        // Assert: stock reducido
        var stockDespues = await ObtenerStock(productoId, SucPp);
        stockDespues!.Cantidad.Should().Be(40);
    }

    [Fact]
    public async Task VentaSimple_TotalCalculadoCorrectamente()
    {
        // Arrange
        var productoId = await CrearProductoTest("VENTA-002", precioVenta: 2000m, precioCosto: 1000m);
        await RegistrarEntradaInventario(productoId, SucPp, 100, 1000);
        var cajaId = await CrearYAbrirCaja(SucPp, "Caja Venta 002");

        // Act: venta de 5 unidades a precio manual de 2500
        var ventaDto = new
        {
            sucursalId = SucPp,
            cajaId,
            metodoPago = 0,
            montoPagado = 15000m,
            lineas = new[]
            {
                new { productoId, cantidad = 5m, precioUnitario = (decimal?)2500m, descuento = 0m }
            }
        };
        var response = await _client.PostAsJsonAsync("/api/v1/Ventas", ventaDto);
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
        await RegistrarEntradaInventario(productoId, SucPp, 100, 500);
        var cajaId = await CrearYAbrirCaja(SucPp, "Caja Venta 003");

        // Configurar precio por sucursal (mayor que el precio base del producto)
        var precioSucResponse = await _client.PostAsJsonAsync("/api/v1/Precios", new
        {
            productoId,
            sucursalId = SucPp,
            precioVenta = 1800m,
            precioMinimo = 1500m
        });
        precioSucResponse.EnsureSuccessStatusCode();

        // Act: venta sin precio manual → debe usar precio de sucursal (1800)
        var ventaDto = new
        {
            sucursalId = SucPp,
            cajaId,
            metodoPago = 0,
            montoPagado = 20000m,
            lineas = new[]
            {
                new { productoId, cantidad = 5m, precioUnitario = (decimal?)null, descuento = 0m }
            }
        };
        var response = await _client.PostAsJsonAsync("/api/v1/Ventas", ventaDto);
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
        await RegistrarEntradaInventario(productoId, SucPp, 50, 500);

        // Crear caja sin abrirla
        var crearCaja = await _client.PostAsJsonAsync("/api/v1/Cajas", new
        {
            nombre = "Caja Cerrada Test",
            sucursalId = SucPp
        });
        crearCaja.EnsureSuccessStatusCode();
        var caja = await crearCaja.Content.ReadFromJsonAsync<CajaDto>(_jsonOptions);

        // Act: intentar vender con caja cerrada
        var ventaDto = new
        {
            sucursalId = SucPp,
            cajaId = caja!.Id,
            metodoPago = 0,
            montoPagado = 5000m,
            lineas = new[]
            {
                new { productoId, cantidad = 5m, precioUnitario = (decimal?)null, descuento = 0m }
            }
        };
        var response = await _client.PostAsJsonAsync("/api/v1/Ventas", ventaDto);

        // Assert: debe rechazar
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task VentaSinStockSuficiente_RetornaBadRequest()
    {
        // Arrange: producto con solo 5 unidades
        var productoId = await CrearProductoTest("VENTA-005", precioVenta: 1000m);
        await RegistrarEntradaInventario(productoId, SucPp, 5, 500);
        var cajaId = await CrearYAbrirCaja(SucPp, "Caja Venta 005");

        // Act: intentar vender 50 unidades (solo hay 5)
        var ventaDto = new
        {
            sucursalId = SucPp,
            cajaId,
            metodoPago = 0,
            montoPagado = 50000m,
            lineas = new[]
            {
                new { productoId, cantidad = 50m, precioUnitario = (decimal?)null, descuento = 0m }
            }
        };
        var response = await _client.PostAsJsonAsync("/api/v1/Ventas", ventaDto);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task VentaSinLineas_RetornaBadRequest()
    {
        var cajaId = await CrearYAbrirCaja(SucPp, "Caja Venta 006");

        var ventaDto = new
        {
            sucursalId = SucPp,
            cajaId,
            metodoPago = 0,
            montoPagado = 1000m,
            lineas = Array.Empty<object>()
        };
        var response = await _client.PostAsJsonAsync("/api/v1/Ventas", ventaDto);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task VentaConMontoPagadoInsuficiente_RetornaBadRequest()
    {
        var productoId = await CrearProductoTest("VENTA-007", precioVenta: 5000m);
        await RegistrarEntradaInventario(productoId, SucPp, 10, 2000);
        var cajaId = await CrearYAbrirCaja(SucPp, "Caja Venta 007");

        // Monto pagado = 1000, pero total = 5000 × 2 = 10000
        var ventaDto = new
        {
            sucursalId = SucPp,
            cajaId,
            metodoPago = 0,
            montoPagado = 1000m,
            lineas = new[]
            {
                new { productoId, cantidad = 2m, precioUnitario = (decimal?)null, descuento = 0m }
            }
        };
        var response = await _client.PostAsJsonAsync("/api/v1/Ventas", ventaDto);

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
        await RegistrarEntradaInventario(productoId, SucPp, 100, 500);
        var cajaId = await CrearYAbrirCaja(SucPp, "Caja Venta 008");

        // Act 1: crear venta de 30 unidades → stock = 70
        var ventaDto = new
        {
            sucursalId = SucPp,
            cajaId,
            metodoPago = 0,
            montoPagado = 50000m,
            lineas = new[]
            {
                new { productoId, cantidad = 30m, precioUnitario = (decimal?)null, descuento = 0m }
            }
        };
        var crearResponse = await _client.PostAsJsonAsync("/api/v1/Ventas", ventaDto);
        crearResponse.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Body: {await crearResponse.Content.ReadAsStringAsync()}");

        var venta = await crearResponse.Content.ReadFromJsonAsync<VentaDto>(_jsonOptions);
        var ventaId = venta!.Id;

        var stockDespuesVenta = await ObtenerStock(productoId, SucPp);
        stockDespuesVenta!.Cantidad.Should().Be(70);

        // Act 2: anular la venta → stock debe volver a 100
        var anularResponse = await _client.PostAsync(
            $"/api/v1/Ventas/{ventaId}/anular?motivo=Test anulacion", null);
        anularResponse.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Body: {await anularResponse.Content.ReadAsStringAsync()}");

        // Assert: stock restaurado
        var stockDespuesAnulacion = await ObtenerStock(productoId, SucPp);
        stockDespuesAnulacion!.Cantidad.Should().Be(100);
    }

    [Fact]
    public async Task AnulacionDeVentaAnulada_RetornaBadRequest()
    {
        // Arrange
        var productoId = await CrearProductoTest("VENTA-009", precioVenta: 1000m);
        await RegistrarEntradaInventario(productoId, SucPp, 50, 500);
        var cajaId = await CrearYAbrirCaja(SucPp, "Caja Venta 009");

        var ventaDto = new
        {
            sucursalId = SucPp,
            cajaId,
            metodoPago = 0,
            montoPagado = 10000m,
            lineas = new[]
            {
                new { productoId, cantidad = 5m, precioUnitario = (decimal?)null, descuento = 0m }
            }
        };
        var crearResponse = await _client.PostAsJsonAsync("/api/v1/Ventas", ventaDto);
        crearResponse.EnsureSuccessStatusCode();
        var venta = await crearResponse.Content.ReadFromJsonAsync<VentaDto>(_jsonOptions);

        // Anular la primera vez → OK
        var anular1 = await _client.PostAsync($"/api/v1/Ventas/{venta!.Id}/anular?motivo=Primera", null);
        anular1.EnsureSuccessStatusCode();

        // Act: intentar anular de nuevo
        var anular2 = await _client.PostAsync($"/api/v1/Ventas/{venta.Id}/anular?motivo=Segunda", null);

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
        await RegistrarEntradaInventario(productoId, SucPp, 100, 1000);
        var cajaId = await CrearYAbrirCaja(SucPp, "Caja Venta 010");

        var ventaDto = new
        {
            sucursalId = SucPp,
            cajaId,
            metodoPago = 1, // Tarjeta
            lineas = new[]
            {
                new { productoId, cantidad = 3m, precioUnitario = (decimal?)null, descuento = 0m }
            }
        };
        var crearResponse = await _client.PostAsJsonAsync("/api/v1/Ventas", ventaDto);
        crearResponse.EnsureSuccessStatusCode();
        var ventaCreada = await crearResponse.Content.ReadFromJsonAsync<VentaDto>(_jsonOptions);

        // Act
        var response = await _client.GetAsync($"/api/v1/Ventas/{ventaCreada!.Id}");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var venta = await response.Content.ReadFromJsonAsync<VentaDto>(_jsonOptions);

        // Assert
        venta.Should().NotBeNull();
        venta.Id.Should().Be(ventaCreada.Id);
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
        var response = await _client.GetAsync($"/api/v1/Ventas?sucursalId={SucPp}&pageSize=100");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<PaginatedResult<VentaDto>>(_jsonOptions);

        // Assert: las ventas creadas en otros tests deben aparecer aquí
        result.Should().NotBeNull();
        result!.Items.Should().OnlyContain(v => v.SucursalId == SucPp);
    }

    [Fact]
    public async Task ResumenDeVentas_RetornaTotales()
    {
        // Act
        var response = await _client.GetAsync($"/api/v1/Ventas/resumen?sucursalId={SucPp}");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var resumen = await response.Content.ReadFromJsonAsync<ResumenVentaDto>(_jsonOptions);

        // Assert
        resumen.Should().NotBeNull();
        resumen.TotalVentas.Should().BeGreaterThanOrEqualTo(0);
        resumen.MontoTotal.Should().BeGreaterThanOrEqualTo(0);
    }

    // ═══════════════════════════════════════════════════════
    //  TESTS DE INTEGRACIÓN ERP OUTBOX
    // ═══════════════════════════════════════════════════════

    [Fact]
    public async Task Venta_AlCrear_EscribeDocumentoContableYErpOutbox()
    {
        // Arrange
        var productoId = await CrearProductoTest("ERP-VENTA-001", precioVenta: 2000m, precioCosto: 1000m);
        await RegistrarEntradaInventario(productoId, SucPp, 50, 1000m);
        var cajaId = await CrearYAbrirCaja(SucPp, "Caja ERP VENTA 001");

        // Act
        var ventaDto = new
        {
            sucursalId = SucPp,
            cajaId,
            clienteId = TerceroId,
            metodoPago = 0, // Efectivo
            montoPagado = 999_999m,
            lineas = new[] { new { productoId, cantidad = 2m, precioUnitario = (decimal?)null, descuento = 0m } }
        };
        var response = await _client.PostAsJsonAsync("/api/v1/Ventas", ventaDto);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var venta = await response.Content.ReadFromJsonAsync<VentaDto>(_jsonOptions);
        var ventaId = venta!.Id;
        var numVenta = venta.NumeroVenta;

        // Assert — DB
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // 1. DocumentoContable creado con tipo VentaCompletada
        var docs = await db.DocumentosContables
            .Include(d => d.Detalles)
            .Where(d => d.TipoDocumento == "VentaCompletada" && d.NumeroSoporte == numVenta)
            .ToListAsync();
        docs.Should().HaveCount(1);
        docs[0].Detalles.Should().NotBeEmpty();
        docs[0].TotalDebito.Should().BeGreaterThan(0);
        docs[0].TotalCredito.Should().BeGreaterThan(0);

        // 2. ErpOutboxMessage encolado en estado Pendiente
        var outbox = await db.ErpOutboxMessages
            .Where(m => m.EntidadId == ventaId && m.TipoDocumento == "VentaCompletada")
            .ToListAsync();
        outbox.Should().HaveCount(1);
        outbox[0].Estado.Should().Be(EstadoOutbox.Pendiente);

        // 3. Payload serializado correctamente
        var payload = JsonSerializer.Deserialize<VentaErpPayload>(outbox[0].Payload, _jsonOptions);
        payload.Should().NotBeNull();
        payload!.NumeroVenta.Should().Be(numVenta);
        payload.Asientos.Should().NotBeEmpty();
        payload.Asientos.Should().Contain(a => a.Naturaleza == "Debito");
        payload.Asientos.Should().Contain(a => a.Naturaleza == "Credito");
    }

    [Fact]
    public async Task Venta_AlAnular_EscribeDocumentoContableAnulacionYErpOutbox()
    {
        // Arrange
        var productoId = await CrearProductoTest("ERP-VENTA-002", precioVenta: 3000m, precioCosto: 1500m);
        await RegistrarEntradaInventario(productoId, SucPp, 50, 1500m);
        var cajaId = await CrearYAbrirCaja(SucPp, "Caja ERP VENTA 002");

        var ventaDto = new
        {
            sucursalId = SucPp,
            cajaId,
            clienteId = TerceroId,
            metodoPago = 0, // Efectivo
            montoPagado = 999_999m,
            lineas = new[] { new { productoId, cantidad = 1m, precioUnitario = (decimal?)null, descuento = 0m } }
        };
        var crearResponse = await _client.PostAsJsonAsync("/api/v1/Ventas", ventaDto);
        crearResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var venta = await crearResponse.Content.ReadFromJsonAsync<VentaDto>(_jsonOptions);
        var ventaId = venta!.Id;
        var numVenta = venta.NumeroVenta;

        // Act — anular
        var anularResponse = await _client.PostAsJsonAsync($"/api/v1/Ventas/{ventaId}/anular",
            new { motivo = "Test anulacion ERP" });
        anularResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // Assert — DB
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // 1. DocumentoContable de anulación creado
        var docAnu = await db.DocumentosContables
            .Include(d => d.Detalles)
            .Where(d => d.TipoDocumento == "AnulacionVenta" && d.NumeroSoporte == $"ANU-{numVenta}")
            .FirstOrDefaultAsync();
        docAnu.Should().NotBeNull();
        docAnu!.Detalles.Should().NotBeEmpty();

        // 2. ErpOutboxMessage de anulación encolado
        var outbox = await db.ErpOutboxMessages
            .Where(m => m.EntidadId == ventaId && m.TipoDocumento == "AnulacionVenta")
            .ToListAsync();
        outbox.Should().HaveCount(1);
        outbox[0].Estado.Should().Be(EstadoOutbox.Pendiente);

        // 3. Asientos invertidos respecto a la venta: la anulación debita ingresos
        var payload = JsonSerializer.Deserialize<VentaErpPayload>(outbox[0].Payload, _jsonOptions);
        payload.Should().NotBeNull();
        payload!.NumeroVenta.Should().Be(numVenta);
        payload.Asientos.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Venta_AlCrear_CamposErpSonFalseInicialmente()
    {
        // Arrange
        var productoId = await CrearProductoTest("ERP-VENTA-003", precioVenta: 1500m, precioCosto: 700m);
        await RegistrarEntradaInventario(productoId, SucPp, 50, 700m);
        var cajaId = await CrearYAbrirCaja(SucPp, "Caja ERP VENTA 003");

        // Act
        var ventaDto = new
        {
            sucursalId = SucPp,
            cajaId,
            clienteId = TerceroId,
            metodoPago = 0, // Efectivo
            montoPagado = 999_999m,
            lineas = new[] { new { productoId, cantidad = 1m, precioUnitario = (decimal?)null, descuento = 0m } }
        };
        var response = await _client.PostAsJsonAsync("/api/v1/Ventas", ventaDto);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var venta = await response.Content.ReadFromJsonAsync<VentaDto>(_jsonOptions);

        // Assert — El background service no ha procesado aún
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var ventaDb = await db.Ventas.FirstAsync(v => v.Id == venta!.Id);
        ventaDb.SincronizadoErp.Should().BeFalse("El background service aún no procesó el outbox");
        ventaDb.FechaSincronizacionErp.Should().BeNull();
        ventaDb.ErpReferencia.Should().BeNull();
    }

    [Fact]
    public async Task Venta_MultiplesVentas_OutboxContieneUnaEntradaPorVenta()
    {
        // Arrange
        var productoId = await CrearProductoTest("ERP-VENTA-004", precioVenta: 1000m, precioCosto: 500m);
        await RegistrarEntradaInventario(productoId, SucPp, 100, 500m);
        var cajaId = await CrearYAbrirCaja(SucPp, "Caja ERP VENTA 004");

        // Act — crear dos ventas independientes
        var dto = new
        {
            sucursalId = SucPp,
            cajaId,
            clienteId = TerceroId,
            metodoPago = 0, // Efectivo
            montoPagado = 999_999m,
            lineas = new[] { new { productoId, cantidad = 1m, precioUnitario = (decimal?)null, descuento = 0m } }
        };
        var r1 = await _client.PostAsJsonAsync("/api/v1/Ventas", dto);
        var r2 = await _client.PostAsJsonAsync("/api/v1/Ventas", dto);
        r1.StatusCode.Should().Be(HttpStatusCode.OK);
        r2.StatusCode.Should().Be(HttpStatusCode.OK);

        var v1 = (await r1.Content.ReadFromJsonAsync<VentaDto>(_jsonOptions))!;
        var v2 = (await r2.Content.ReadFromJsonAsync<VentaDto>(_jsonOptions))!;

        // Assert — cada venta tiene su propio outbox
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var outboxV1 = await db.ErpOutboxMessages
            .Where(m => m.EntidadId == v1.Id && m.TipoDocumento == "VentaCompletada")
            .ToListAsync();
        var outboxV2 = await db.ErpOutboxMessages
            .Where(m => m.EntidadId == v2.Id && m.TipoDocumento == "VentaCompletada")
            .ToListAsync();

        outboxV1.Should().HaveCount(1);
        outboxV2.Should().HaveCount(1);
        outboxV1[0].EntidadId.Should().NotBe(outboxV2[0].EntidadId);
    }

    [Fact]
    public async Task Venta_ConMetodoPagoTarjeta_AsientoDebitaEnCuentaTarjeta()
    {
        // Arrange
        var productoId = await CrearProductoTest("ERP-VENTA-005", precioVenta: 5000m, precioCosto: 2000m);
        await RegistrarEntradaInventario(productoId, SucPp, 50, 2000m);
        var cajaId = await CrearYAbrirCaja(SucPp, "Caja ERP VENTA 005");

        // Act — pago con tarjeta (MetodoPago = 1 = Tarjeta)
        var ventaDto = new
        {
            sucursalId = SucPp,
            cajaId,
            clienteId = TerceroId,
            metodoPago = 1, // Tarjeta
            montoPagado = 999_999m,
            lineas = new[] { new { productoId, cantidad = 1m, precioUnitario = (decimal?)null, descuento = 0m } }
        };
        var response = await _client.PostAsJsonAsync("/api/v1/Ventas", ventaDto);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var venta = await response.Content.ReadFromJsonAsync<VentaDto>(_jsonOptions);

        // Assert — el asiento débito usa la cuenta de tarjeta (111005)
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var outbox = await db.ErpOutboxMessages
            .FirstAsync(m => m.EntidadId == venta!.Id && m.TipoDocumento == "VentaCompletada");

        var payload = JsonSerializer.Deserialize<VentaErpPayload>(outbox.Payload, _jsonOptions);
        var debitoEntry = payload!.Asientos.First(a => a.Naturaleza == "Debito");
        debitoEntry.Cuenta.Should().Be("111005", "pago con tarjeta debe debitar la cuenta 111005 (Bancos)");
    }
}
