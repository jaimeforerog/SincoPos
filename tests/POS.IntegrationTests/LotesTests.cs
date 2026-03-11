using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using POS.Application.DTOs;

namespace POS.IntegrationTests;

/// <summary>
/// Pruebas de integración para el módulo de Lotes/Vencimientos.
/// Verifica: FEFO en ventas, snapshot de número de lote, alertas de vencimiento y API de lotes.
/// </summary>
[Collection("POS")]
public class LotesTests
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

    public LotesTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    // ─── Helpers ────────────────────────────────────────────

    private async Task<Guid> CrearProductoTest(string codigo, bool manejaLotes = false,
        decimal precioVenta = 2000m, decimal precioCosto = 1000m)
    {
        var dto = new
        {
            codigoBarras = codigo,
            nombre = $"Producto {codigo}",
            descripcion = "Test lotes",
            categoriaId = CatId,
            precioVenta,
            precioCosto,
            manejaLotes
        };
        var response = await _client.PostAsJsonAsync("/api/v1/Productos", dto);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<ProductoDto>(_jsonOptions);
        return result!.Id;
    }

    private async Task RegistrarEntradaConLote(Guid productoId, int sucursalId,
        decimal cantidad, decimal costoUnitario, string referencia,
        string? numeroLote = null, string? fechaVencimiento = null)
    {
        var dto = new
        {
            productoId, sucursalId, cantidad, costoUnitario,
            terceroId = TerceroId, referencia,
            observaciones = $"Entrada con lote {referencia}",
            numeroLote,
            fechaVencimiento
        };
        var response = await _client.PostAsJsonAsync("/api/v1/Inventario/entrada", dto);
        response.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Entrada deberia ser exitosa. Body: {await response.Content.ReadAsStringAsync()}");
    }

    private async Task<int> CrearYAbrirCaja(int sucursalId, string nombre)
    {
        var crearResponse = await _client.PostAsJsonAsync("/api/v1/Cajas", new { nombre, sucursalId });
        crearResponse.EnsureSuccessStatusCode();
        var caja = await crearResponse.Content.ReadFromJsonAsync<CajaDto>(_jsonOptions);

        var abrirResponse = await _client.PostAsJsonAsync($"/api/v1/Cajas/{caja!.Id}/abrir", new { montoApertura = 100_000m });
        abrirResponse.EnsureSuccessStatusCode();

        return caja.Id;
    }

    private async Task<VentaDto> CrearVenta(Guid productoId, int sucursalId, int cajaId, decimal cantidad)
    {
        var dto = new
        {
            sucursalId,
            cajaId,
            metodoPago = 0,
            montoPagado = 999_999m,
            lineas = new[]
            {
                new { productoId, cantidad, precioUnitario = (decimal?)null, descuento = 0m }
            }
        };
        var response = await _client.PostAsJsonAsync("/api/v1/Ventas", dto);
        response.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Venta deberia ser exitosa. Body: {await response.Content.ReadAsStringAsync()}");
        return (await response.Content.ReadFromJsonAsync<VentaDto>(_jsonOptions))!;
    }

    private async Task<List<JsonElement>> ObtenerLotes(Guid productoId, int sucursalId, bool soloVigentes = true)
    {
        var url = $"/api/v1/Lotes?productoId={productoId}&sucursalId={sucursalId}&soloVigentes={soloVigentes}";
        return await _client.GetFromJsonAsync<List<JsonElement>>(url, _jsonOptions) ?? [];
    }

    // ═══════════════════════════════════════════════════════
    //  FEFO — First Expired, First Out
    // ═══════════════════════════════════════════════════════

    [Fact]
    public async Task FEFO_TresLotes_ConsumePorOrdenDeVencimiento()
    {
        // Arrange: producto con lotes, 3 lotes con diferentes fechas de vencimiento
        var productoId = await CrearProductoTest("FEFO-001", manejaLotes: true);
        var cajaId = await CrearYAbrirCaja(SucPp, "Caja FEFO 001");

        // Ingresamos en orden "incorrecto" para verificar que FEFO los ordena bien
        await RegistrarEntradaConLote(productoId, SucPp, 10, 1000, "FC-FEFO-001C",
            numeroLote: "LOTE-C", fechaVencimiento: "2026-12-31"); // vence último
        await RegistrarEntradaConLote(productoId, SucPp, 10, 1000, "FC-FEFO-001A",
            numeroLote: "LOTE-A", fechaVencimiento: "2026-04-01"); // vence primero
        await RegistrarEntradaConLote(productoId, SucPp, 10, 1000, "FC-FEFO-001B",
            numeroLote: "LOTE-B", fechaVencimiento: "2026-08-15"); // vence segundo

        // Act: vender 10 unidades → debe consumir LOTE-A (vence primero)
        var venta = await CrearVenta(productoId, SucPp, cajaId, 10);

        // Assert: snapshot del lote consumido es LOTE-A
        venta.Detalles.Should().HaveCount(1);
        venta.Detalles[0].NumeroLote.Should().Be("LOTE-A");
    }

    [Fact]
    public async Task FEFO_VentaParcial_ConsumeUnSoloLote()
    {
        // Arrange
        var productoId = await CrearProductoTest("FEFO-002", manejaLotes: true);
        var cajaId = await CrearYAbrirCaja(SucPp, "Caja FEFO 002");

        await RegistrarEntradaConLote(productoId, SucPp, 20, 1000, "FC-FEFO-002A",
            numeroLote: "LOTE-2A", fechaVencimiento: "2026-05-01");
        await RegistrarEntradaConLote(productoId, SucPp, 20, 1000, "FC-FEFO-002B",
            numeroLote: "LOTE-2B", fechaVencimiento: "2026-11-01");

        // Act: vender 5 → solo consume del primer lote
        var venta = await CrearVenta(productoId, SucPp, cajaId, 5);

        // Assert: lote correcto, y lotes restantes
        venta.Detalles[0].NumeroLote.Should().Be("LOTE-2A");

        var lotes = await ObtenerLotes(productoId, SucPp);
        var lote2A = lotes.FirstOrDefault(l => l.GetProperty("numeroLote").GetString() == "LOTE-2A");
        lote2A.GetProperty("cantidadDisponible").GetDecimal().Should().Be(15);
    }

    [Fact]
    public async Task FEFO_VentaAbarcaDosLotes_ConsumeCorrecto()
    {
        // Arrange: lote A con 10 unidades, lote B con 20
        var productoId = await CrearProductoTest("FEFO-003", manejaLotes: true);
        var cajaId = await CrearYAbrirCaja(SucPp, "Caja FEFO 003");

        await RegistrarEntradaConLote(productoId, SucPp, 10, 1000, "FC-FEFO-003A",
            numeroLote: "LOTE-3A", fechaVencimiento: "2026-06-01");
        await RegistrarEntradaConLote(productoId, SucPp, 20, 1000, "FC-FEFO-003B",
            numeroLote: "LOTE-3B", fechaVencimiento: "2026-12-01");

        // Act: vender 15 → agota lote A (10) + consume 5 de lote B
        var venta = await CrearVenta(productoId, SucPp, cajaId, 15);

        // Assert: venta exitosa, lote A agotado, lote B con 15 restantes
        venta.Estado.Should().Be("Completada");

        var lotes = await ObtenerLotes(productoId, SucPp, soloVigentes: false);
        var loteA = lotes.FirstOrDefault(l => l.GetProperty("numeroLote").GetString() == "LOTE-3A");
        var loteB = lotes.FirstOrDefault(l => l.GetProperty("numeroLote").GetString() == "LOTE-3B");

        loteA.GetProperty("cantidadDisponible").GetDecimal().Should().Be(0);
        loteB.GetProperty("cantidadDisponible").GetDecimal().Should().Be(15);
    }

    [Fact]
    public async Task ProductoSinLotes_VentaNormal_SinSnapshotLote()
    {
        // Arrange: producto que NO maneja lotes
        var productoId = await CrearProductoTest("FEFO-004", manejaLotes: false);
        var cajaId = await CrearYAbrirCaja(SucPp, "Caja FEFO 004");

        // Entrada estándar sin número de lote
        var dto = new
        {
            productoId, sucursalId = SucPp, cantidad = 20m, costoUnitario = 1000m,
            terceroId = TerceroId, referencia = "FC-FEFO-004"
        };
        var entradaResp = await _client.PostAsJsonAsync("/api/v1/Inventario/entrada", dto);
        entradaResp.EnsureSuccessStatusCode();

        // Act: vender → no debe tener numeroLote en el detalle
        var venta = await CrearVenta(productoId, SucPp, cajaId, 5);

        // Assert
        venta.Detalles[0].NumeroLote.Should().BeNullOrEmpty();
    }

    // ═══════════════════════════════════════════════════════
    //  SNAPSHOT DE LOTE EN DETALLE DE VENTA
    // ═══════════════════════════════════════════════════════

    [Fact]
    public async Task VentaConLotes_SnapshotNumeroLotePersiste()
    {
        // Arrange
        var productoId = await CrearProductoTest("SNAP-001", manejaLotes: true);
        var cajaId = await CrearYAbrirCaja(SucPp, "Caja SNAP 001");

        await RegistrarEntradaConLote(productoId, SucPp, 30, 1000, "FC-SNAP-001",
            numeroLote: "LOTE-SNAP-XYZ", fechaVencimiento: "2027-01-01");

        // Act
        var venta = await CrearVenta(productoId, SucPp, cajaId, 10);

        // Assert: leer la venta por ID para confirmar que el snapshot persiste
        var ventaGuardada = await _client.GetFromJsonAsync<VentaDto>(
            $"/api/v1/Ventas/{venta.Id}", _jsonOptions);

        ventaGuardada.Should().NotBeNull();
        ventaGuardada!.Detalles[0].NumeroLote.Should().Be("LOTE-SNAP-XYZ");
    }

    // ═══════════════════════════════════════════════════════
    //  API DE LOTES
    // ═══════════════════════════════════════════════════════

    [Fact]
    public async Task GetLotes_OrdenadosPorFechaVencimientoAsc()
    {
        // Arrange: registrar lotes en orden inverso al de vencimiento
        var productoId = await CrearProductoTest("LAPI-001", manejaLotes: true);

        await RegistrarEntradaConLote(productoId, SucPp, 5, 1000, "FC-LAPI-001C",
            numeroLote: "LOTE-C99", fechaVencimiento: "2027-06-30");
        await RegistrarEntradaConLote(productoId, SucPp, 5, 1000, "FC-LAPI-001A",
            numeroLote: "LOTE-A99", fechaVencimiento: "2026-03-01");
        await RegistrarEntradaConLote(productoId, SucPp, 5, 1000, "FC-LAPI-001B",
            numeroLote: "LOTE-B99", fechaVencimiento: "2026-09-15");

        // Act
        var lotes = await ObtenerLotes(productoId, SucPp);

        // Assert: deben venir ordenados por fecha vencimiento ASC
        lotes.Should().HaveCount(3);
        var fechas = lotes.Select(l => l.GetProperty("fechaVencimiento").GetString()).ToList();
        fechas.Should().BeInAscendingOrder();
    }

    [Fact]
    public async Task GetLotes_SoloVigentes_ExcluyeAgotados()
    {
        // Arrange
        var productoId = await CrearProductoTest("LAPI-002", manejaLotes: true);
        var cajaId = await CrearYAbrirCaja(SucPp, "Caja LAPI 002");

        await RegistrarEntradaConLote(productoId, SucPp, 5, 1000, "FC-LAPI-002A",
            numeroLote: "LOTE-VIG", fechaVencimiento: "2026-07-01");
        await RegistrarEntradaConLote(productoId, SucPp, 10, 1000, "FC-LAPI-002B",
            numeroLote: "LOTE-AGO", fechaVencimiento: "2026-12-01");

        // Agotar el primer lote
        await CrearVenta(productoId, SucPp, cajaId, 5);

        // Act: soloVigentes=true no debe retornar LOTE-VIG (agotado)
        var soloVigentes = await ObtenerLotes(productoId, SucPp, soloVigentes: true);
        var todos = await ObtenerLotes(productoId, SucPp, soloVigentes: false);

        // Assert
        soloVigentes.Should().HaveCount(1);
        soloVigentes[0].GetProperty("numeroLote").GetString().Should().Be("LOTE-AGO");
        todos.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetLotes_ProductoEndpoint_RetornaLotesDelProducto()
    {
        // Arrange
        var productoId = await CrearProductoTest("LAPI-003", manejaLotes: true);

        await RegistrarEntradaConLote(productoId, SucPp, 8, 1500, "FC-LAPI-003",
            numeroLote: "LOTE-PROD-01", fechaVencimiento: "2026-10-15");

        // Act: endpoint en ProductosController
        var response = await _client.GetAsync(
            $"/api/v1/Productos/{productoId}/lotes?sucursalId={SucPp}&soloVigentes=true");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var lotes = await response.Content.ReadFromJsonAsync<List<JsonElement>>(_jsonOptions) ?? [];
        lotes.Should().HaveCount(1);
        lotes[0].GetProperty("numeroLote").GetString().Should().Be("LOTE-PROD-01");
    }

    [Fact]
    public async Task PutLote_ActualizaNumeroYFecha()
    {
        // Arrange: crear lote con datos iniciales
        var productoId = await CrearProductoTest("LAPI-004", manejaLotes: true);

        await RegistrarEntradaConLote(productoId, SucPp, 10, 1000, "FC-LAPI-004",
            numeroLote: "LOTE-ORIG", fechaVencimiento: "2026-05-01");

        var lotes = await ObtenerLotes(productoId, SucPp);
        lotes.Should().HaveCount(1);
        var loteId = lotes[0].GetProperty("id").GetInt32();

        // Act: actualizar número de lote y fecha
        var updateDto = new { numeroLote = "LOTE-NUEVO", fechaVencimiento = "2027-03-31" };
        var response = await _client.PutAsJsonAsync($"/api/v1/Lotes/{loteId}", updateDto);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var actualizado = await response.Content.ReadFromJsonAsync<JsonElement>(_jsonOptions);
        actualizado.GetProperty("numeroLote").GetString().Should().Be("LOTE-NUEVO");
        actualizado.GetProperty("fechaVencimiento").GetString().Should().Be("2027-03-31");
    }

    // ═══════════════════════════════════════════════════════
    //  ALERTAS DE VENCIMIENTO
    // ═══════════════════════════════════════════════════════

    [Fact]
    public async Task GetProximosAVencer_RetornaLotesDentroDeVentana()
    {
        // Arrange: lote que vence en 15 días (dentro de ventana de 30)
        var productoId = await CrearProductoTest("ALERTA-L01", manejaLotes: true);
        var fechaProxima = DateOnly.FromDateTime(DateTime.Today.AddDays(15)).ToString("yyyy-MM-dd");

        await RegistrarEntradaConLote(productoId, SucPp, 10, 1000, "FC-ALERTA-L01",
            numeroLote: "LOTE-PROX", fechaVencimiento: fechaProxima);

        // Act
        var response = await _client.GetAsync(
            $"/api/v1/Lotes/proximos-vencer?sucursalId={SucPp}&diasAnticipacion=30");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var alertas = await response.Content.ReadFromJsonAsync<List<JsonElement>>(_jsonOptions) ?? [];
        alertas.Should().Contain(a => a.GetProperty("nombreProducto").GetString()!.Contains("ALERTA-L01"));
    }

    [Fact]
    public async Task GetAlertas_RetornaLotesProximosEnTodasLasSucursales()
    {
        // Arrange: lote que vence pronto en SucPp
        var productoId = await CrearProductoTest("ALERTA-L02", manejaLotes: true);
        var fechaProxima = DateOnly.FromDateTime(DateTime.Today.AddDays(7)).ToString("yyyy-MM-dd");

        await RegistrarEntradaConLote(productoId, SucPp, 5, 1000, "FC-ALERTA-L02",
            numeroLote: "LOTE-URGENTE", fechaVencimiento: fechaProxima);

        // Act: endpoint /alertas sin filtro de sucursal
        var response = await _client.GetAsync("/api/v1/Lotes/alertas");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var alertas = await response.Content.ReadFromJsonAsync<List<JsonElement>>(_jsonOptions) ?? [];
        alertas.Should().Contain(a => a.GetProperty("numeroLote").GetString() == "LOTE-URGENTE");
    }

    // ═══════════════════════════════════════════════════════
    //  RECEPCIÓN CON LOTES (vía ComprasController)
    // ═══════════════════════════════════════════════════════

    [Fact]
    public async Task RecepcionOrdenCompra_ConLote_CreaLoteConDatosCorrectos()
    {
        // Arrange: crear proveedor, producto y orden de compra
        var productoId = await CrearProductoTest("RECEP-L01", manejaLotes: true, precioCosto: 800m);

        // Crear orden de compra
        var ordenDto = new
        {
            sucursalId = SucPp,
            proveedorId = TerceroId,
            observaciones = "Test recepción con lote",
            detalles = new[]
            {
                new { productoId, cantidad = 20m, precioUnitario = 800m }
            }
        };
        var ordenResp = await _client.PostAsJsonAsync("/api/v1/Compras", ordenDto);
        ordenResp.EnsureSuccessStatusCode();
        var orden = await ordenResp.Content.ReadFromJsonAsync<JsonElement>(_jsonOptions);
        var ordenId = orden.GetProperty("id").GetInt32();

        // Act: recibir la orden con número de lote y fecha de vencimiento
        var recepcionDto = new
        {
            lineas = new[]
            {
                new
                {
                    productoId,
                    cantidadRecibida = 20m,
                    costoUnitarioFinal = 800m,
                    numeroLote = "LOTE-COMPRA-001",
                    fechaVencimiento = "2027-06-30"
                }
            }
        };
        var recepcionResp = await _client.PostAsJsonAsync(
            $"/api/v1/Compras/{ordenId}/recibir", recepcionDto);
        recepcionResp.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Recepción debería ser exitosa. Body: {await recepcionResp.Content.ReadAsStringAsync()}");

        // Assert: verificar que el lote fue creado con los datos correctos
        var lotes = await ObtenerLotes(productoId, SucPp);
        lotes.Should().HaveCount(1);
        lotes[0].GetProperty("numeroLote").GetString().Should().Be("LOTE-COMPRA-001");
        lotes[0].GetProperty("fechaVencimiento").GetString().Should().Be("2027-06-30");
        lotes[0].GetProperty("cantidadDisponible").GetDecimal().Should().Be(20);
    }
}
