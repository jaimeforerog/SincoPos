using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using POS.Application.DTOs;

namespace POS.IntegrationTests;

/// <summary>
/// Tests de integración para el módulo POS (Cajas + flujo de venta completo).
/// Verifica: CRUD de cajas, apertura/cierre, flujo end-to-end POS, y validaciones de negocio.
/// </summary>
[Collection("POS")]
public class PosTests
{
    private readonly HttpClient _client;
    private readonly CustomWebApplicationFactory _factory;
    private readonly JsonSerializerOptions _json = new() { PropertyNameCaseInsensitive = true };

    private int SucId => _factory.SucursalPPId;
    private int CatId => _factory.CategoriaTestId;
    private int TercId => _factory.TerceroTestId;

    public PosTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    // ─── Helpers ────────────────────────────────────────────────

    private async Task<CajaDto> CrearCaja(string nombre)
    {
        var r = await _client.PostAsJsonAsync("/api/v1/Cajas", new { nombre, sucursalId = SucId });
        r.EnsureSuccessStatusCode();
        return (await r.Content.ReadFromJsonAsync<CajaDto>(_json))!;
    }

    private async Task AbrirCaja(int cajaId, decimal montoApertura = 100_000m)
    {
        var r = await _client.PostAsJsonAsync($"/api/v1/Cajas/{cajaId}/abrir", new { montoApertura });
        r.EnsureSuccessStatusCode();
    }

    private async Task<Guid> CrearProducto(string codigo, decimal precioVenta = 5000m, decimal precioCosto = 2000m)
    {
        var r = await _client.PostAsJsonAsync("/api/v1/Productos", new
        {
            codigoBarras = codigo, nombre = $"Prod {codigo}",
            categoriaId = CatId, precioVenta, precioCosto
        });
        r.EnsureSuccessStatusCode();
        return (await r.Content.ReadFromJsonAsync<ProductoDto>(_json))!.Id;
    }

    private async Task RegistrarEntrada(Guid productoId, decimal cantidad, decimal costo = 2000m)
    {
        var r = await _client.PostAsJsonAsync("/api/v1/Inventario/entrada", new
        {
            productoId, sucursalId = SucId, cantidad, costoUnitario = costo,
            terceroId = TercId, referencia = $"POS-{Guid.NewGuid():N}"[..20]
        });
        r.EnsureSuccessStatusCode();
    }

    private async Task<VentaDto> HacerVenta(int cajaId, Guid productoId, int cantidad = 1)
    {
        var r = await _client.PostAsJsonAsync("/api/v1/Ventas", new
        {
            sucursalId = SucId, cajaId,
            metodoPago = 0,  // 0 = Efectivo
            montoPagado = 999_999m,
            lineas = new[] { new { productoId, cantidad = (decimal)cantidad, precioUnitario = (decimal?)null, descuento = 0m } }
        });
        r.EnsureSuccessStatusCode();
        return (await r.Content.ReadFromJsonAsync<VentaDto>(_json))!;
    }

    // ─── CRUD de Cajas ────────────────────────────────────────────

    [Fact]
    public async Task CrearCaja_DatosValidos_Retorna201()
    {
        var nombre = $"Caja-New-{Guid.NewGuid():N}"[..20];

        var r = await _client.PostAsJsonAsync("/api/v1/Cajas", new { nombre, sucursalId = SucId });

        r.StatusCode.Should().Be(HttpStatusCode.Created);
        var caja = await r.Content.ReadFromJsonAsync<CajaDto>(_json);
        caja!.Nombre.Should().Be(nombre);
        caja.SucursalId.Should().Be(SucId);
        caja.Estado.Should().Be("Cerrada");
        caja.Activa.Should().BeTrue();
    }

    [Fact]
    public async Task CrearCaja_NombreDuplicadoEnMismaSucursal_Retorna409()
    {
        var nombre = $"Caja-Dup-{Guid.NewGuid():N}"[..20];
        await CrearCaja(nombre);

        var r = await _client.PostAsJsonAsync("/api/v1/Cajas", new { nombre, sucursalId = SucId });

        r.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task ObtenerCaja_Existente_Retorna200()
    {
        var caja = await CrearCaja($"Caja-Get-{Guid.NewGuid():N}"[..20]);

        var r = await _client.GetAsync($"/api/v1/Cajas/{caja.Id}");

        r.StatusCode.Should().Be(HttpStatusCode.OK);
        var resultado = await r.Content.ReadFromJsonAsync<CajaDto>(_json);
        resultado!.Id.Should().Be(caja.Id);
    }

    [Fact]
    public async Task ObtenerCaja_Inexistente_Retorna404()
    {
        var r = await _client.GetAsync("/api/v1/Cajas/999999");
        r.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ListarCajas_FiltroPorSucursal_RetornaSoloDeSucursal()
    {
        await CrearCaja($"Caja-List-{Guid.NewGuid():N}"[..20]);

        var r = await _client.GetAsync($"/api/v1/Cajas?sucursalId={SucId}");

        r.StatusCode.Should().Be(HttpStatusCode.OK);
        var cajas = await r.Content.ReadFromJsonAsync<List<CajaDto>>(_json);
        cajas.Should().NotBeEmpty();
        cajas!.All(c => c.SucursalId == SucId).Should().BeTrue();
    }

    [Fact]
    public async Task DesactivarCaja_CajaExistente_Retorna204()
    {
        var caja = await CrearCaja($"Caja-Del-{Guid.NewGuid():N}"[..20]);

        var r = await _client.DeleteAsync($"/api/v1/Cajas/{caja.Id}");

        r.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task DesactivarCaja_CajaAbierta_Retorna409()
    {
        var caja = await CrearCaja($"Caja-DelAbr-{Guid.NewGuid():N}"[..18]);
        await AbrirCaja(caja.Id);

        var r = await _client.DeleteAsync($"/api/v1/Cajas/{caja.Id}");

        r.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    // ─── Apertura y Cierre ────────────────────────────────────────────

    [Fact]
    public async Task AbrirCaja_CajaCerrada_CambiaEstadoAbierta()
    {
        var caja = await CrearCaja($"Caja-Abr-{Guid.NewGuid():N}"[..20]);

        var r = await _client.PostAsJsonAsync($"/api/v1/Cajas/{caja.Id}/abrir",
            new { montoApertura = 50_000m });

        r.StatusCode.Should().Be(HttpStatusCode.OK);
        var cajaActual = await (await _client.GetAsync($"/api/v1/Cajas/{caja.Id}"))
            .Content.ReadFromJsonAsync<CajaDto>(_json);
        cajaActual!.Estado.Should().Be("Abierta");
        cajaActual.MontoApertura.Should().Be(50_000m);
    }

    [Fact]
    public async Task AbrirCaja_YaAbierta_Retorna409()
    {
        var caja = await CrearCaja($"Caja-AbrDup-{Guid.NewGuid():N}"[..18]);
        await AbrirCaja(caja.Id);

        var r = await _client.PostAsJsonAsync($"/api/v1/Cajas/{caja.Id}/abrir",
            new { montoApertura = 10_000m });

        r.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task CerrarCaja_CajaAbierta_CambiaEstadoCerrada()
    {
        var caja = await CrearCaja($"Caja-Cerr-{Guid.NewGuid():N}"[..19]);
        await AbrirCaja(caja.Id, 80_000m);

        var r = await _client.PostAsJsonAsync($"/api/v1/Cajas/{caja.Id}/cerrar",
            new { montoReal = 80_000m, observaciones = "Cuadre correcto" });

        r.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await r.Content.ReadFromJsonAsync<JsonElement>(_json);
        body.GetProperty("diferencia").GetDecimal().Should().Be(0);
        body.GetProperty("cuadra").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task CerrarCaja_ConDiferencia_ReportaDiferencia()
    {
        var caja = await CrearCaja($"Caja-Diff-{Guid.NewGuid():N}"[..19]);
        await AbrirCaja(caja.Id, 100_000m);

        var r = await _client.PostAsJsonAsync($"/api/v1/Cajas/{caja.Id}/cerrar",
            new { montoReal = 95_000m });

        r.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await r.Content.ReadFromJsonAsync<JsonElement>(_json);
        body.GetProperty("diferencia").GetDecimal().Should().Be(-5_000m);
        body.GetProperty("cuadra").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public async Task CerrarCaja_YaCerrada_Retorna409()
    {
        var caja = await CrearCaja($"Caja-CerrDup-{Guid.NewGuid():N}"[..17]);

        var r = await _client.PostAsJsonAsync($"/api/v1/Cajas/{caja.Id}/cerrar",
            new { montoReal = 0m });

        r.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    // ─── Flujo POS Completo ────────────────────────────────────────────

    [Fact]
    public async Task FlujoPos_Completo_AperturVentaCierre()
    {
        // Arrange
        var cod = $"POS-FC-{Guid.NewGuid():N}"[..15];
        var productoId = await CrearProducto(cod, precioVenta: 10_000m, precioCosto: 4_000m);
        await RegistrarEntrada(productoId, 10, costo: 4_000m);
        var caja = await CrearCaja($"Caja-POS-{cod}");
        await AbrirCaja(caja.Id, 50_000m);

        // Act: venta de 2 unidades
        var venta = await HacerVenta(caja.Id, productoId, cantidad: 2);

        // Assert venta
        venta.Id.Should().BeGreaterThan(0);
        venta.Total.Should().Be(20_000m);
        venta.NumeroVenta.Should().StartWith("V-");

        // Verificar stock decrementó
        var stockR = await _client.GetAsync(
            $"/api/v1/Inventario?sucursalId={SucId}&productoId={productoId}");
        stockR.StatusCode.Should().Be(HttpStatusCode.OK);
        var stock = await stockR.Content.ReadFromJsonAsync<List<StockDto>>(_json);
        stock!.Single().Cantidad.Should().Be(8);

        // Cerrar caja con monto esperado (apertura + efectivo venta)
        var montoEsperado = 50_000m + 20_000m;
        var cierreR = await _client.PostAsJsonAsync($"/api/v1/Cajas/{caja.Id}/cerrar",
            new { montoReal = montoEsperado });
        cierreR.StatusCode.Should().Be(HttpStatusCode.OK);
        var cierre = await cierreR.Content.ReadFromJsonAsync<JsonElement>(_json);
        cierre.GetProperty("cuadra").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task FlujoPos_VentaConCajaCerrada_Retorna400()
    {
        var cod = $"POS-CC-{Guid.NewGuid():N}"[..15];
        var productoId = await CrearProducto(cod);
        await RegistrarEntrada(productoId, 10);
        var caja = await CrearCaja($"Caja-Cerr-{cod}");
        // No abrir la caja

        var r = await _client.PostAsJsonAsync("/api/v1/Ventas", new
        {
            sucursalId = SucId, cajaId = caja.Id,
            metodoPago = "Efectivo",
            items = new[] { new { productoId, cantidad = 1, precioUnitario = 5000m } }
        });

        r.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task FlujoPos_MultiplesMétodosPago_CajaRegistraCorrectamente()
    {
        // Arrange: producto + caja abierta
        var cod = $"POS-MP-{Guid.NewGuid():N}"[..15];
        var productoId = await CrearProducto(cod, precioVenta: 8_000m, precioCosto: 3_000m);
        await RegistrarEntrada(productoId, 20, costo: 3_000m);
        var caja = await CrearCaja($"Caja-MP-{cod}");
        await AbrirCaja(caja.Id, 100_000m);

        // Venta 1: Efectivo
        await _client.PostAsJsonAsync("/api/v1/Ventas", new
        {
            sucursalId = SucId, cajaId = caja.Id, metodoPago = 0, montoPagado = 10_000m,
            lineas = new[] { new { productoId, cantidad = 1m, precioUnitario = (decimal?)8000m, descuento = 0m } }
        });

        // Venta 2: Tarjeta
        await _client.PostAsJsonAsync("/api/v1/Ventas", new
        {
            sucursalId = SucId, cajaId = caja.Id, metodoPago = 1, montoPagado = 8_000m,
            lineas = new[] { new { productoId, cantidad = 1m, precioUnitario = (decimal?)8000m, descuento = 0m } }
        });

        // Verificar reporte de caja
        var r = await _client.GetAsync($"/api/v1/Reportes/caja/{caja.Id}");
        r.StatusCode.Should().Be(HttpStatusCode.OK);
        var reporte = await r.Content.ReadFromJsonAsync<ReporteCajaDto>(_json);
        reporte!.TotalVentasEfectivo.Should().Be(8_000m);
        reporte.TotalVentasTarjeta.Should().Be(8_000m);
        reporte.TotalVentas.Should().Be(16_000m);
        reporte.Ventas.Should().HaveCount(2);
    }

    [Fact]
    public async Task FlujoPos_CierreCajaActualizaMontoConVentas()
    {
        // Arrange
        var cod = $"POS-CM-{Guid.NewGuid():N}"[..15];
        var productoId = await CrearProducto(cod, precioVenta: 15_000m, precioCosto: 5_000m);
        await RegistrarEntrada(productoId, 5, costo: 5_000m);
        var caja = await CrearCaja($"Caja-CM-{cod}");
        await AbrirCaja(caja.Id, 200_000m);

        // Venta en efectivo: 15000
        await HacerVenta(caja.Id, productoId, cantidad: 1);

        // El monto esperado al cierre = apertura (200k) + efectivo (15k)
        var r = await _client.PostAsJsonAsync($"/api/v1/Cajas/{caja.Id}/cerrar",
            new { montoReal = 215_000m });

        r.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await r.Content.ReadFromJsonAsync<JsonElement>(_json);
        body.GetProperty("montoEsperado").GetDecimal().Should().Be(215_000m);
        body.GetProperty("diferencia").GetDecimal().Should().Be(0);
    }

    // ─── Validaciones POS ────────────────────────────────────────────

    [Fact]
    public async Task AbrirCaja_SucursalInexistente_Retorna400()
    {
        var r = await _client.PostAsJsonAsync("/api/v1/Cajas", new { nombre = "X", sucursalId = 999999 });
        r.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ListarCajas_IncluirInactivas_MuestraTodasLasCajas()
    {
        var nombre = $"Caja-Inact-{Guid.NewGuid():N}"[..18];
        var caja = await CrearCaja(nombre);
        await _client.DeleteAsync($"/api/v1/Cajas/{caja.Id}");

        var r = await _client.GetAsync($"/api/v1/Cajas?sucursalId={SucId}&incluirInactivas=true");
        r.StatusCode.Should().Be(HttpStatusCode.OK);
        var cajas = await r.Content.ReadFromJsonAsync<List<CajaDto>>(_json);
        cajas.Should().Contain(c => c.Id == caja.Id && !c.Activa);
    }
}
