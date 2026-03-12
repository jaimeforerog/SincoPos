using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using POS.Application.DTOs;

namespace POS.IntegrationTests;

/// <summary>
/// Tests de integración para el Motor de Impuestos Universal (Tax Engine).
/// Verifica: CRUD de Impuestos, CRUD de Retenciones,
/// cálculo correcto de impuestos en ventas y flag de factura electrónica.
/// </summary>
[Collection("POS")]
public class ImpuestosTests
{
    private readonly HttpClient _client;
    private readonly CustomWebApplicationFactory _factory;
    private readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };

    private int SucPp => _factory.SucursalPPId;
    private int CatId => _factory.CategoriaTestId;

    public ImpuestosTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }
    

    // ── Helpers ───────────────────────────────────────────────────────────────

    private async Task<int> CrearImpuesto(string nombre, string tipo, decimal porcentaje,
        bool aplicaSobreBase = true, decimal? valorFijo = null)
    {
        var response = await _client.PostAsJsonAsync("/api/v1/Impuestos", new
        {
            nombre, tipo, porcentaje,
            valorFijo,
            codigoCuentaContable = "2408",
            aplicaSobreBase,
            codigoPais = "CO",
            descripcion = $"Test: {nombre}"
        });
        response.StatusCode.Should().Be(HttpStatusCode.Created,
            $"Crear impuesto deberia ser exitoso. Body: {await response.Content.ReadAsStringAsync()}");
        var result = await response.Content.ReadFromJsonAsync<JsonElement>(_jsonOptions);
        return result.GetProperty("id").GetInt32();
    }

    private async Task<Guid> CrearProductoConImpuesto(string codigo, int? impuestoId,
        decimal precio = 1000m, decimal costo = 500m)
    {
        var response = await _client.PostAsJsonAsync("/api/v1/Productos", new
        {
            codigoBarras = codigo,
            nombre = $"Prod {codigo}",
            descripcion = "Test impuestos",
            categoriaId = CatId,
            precioVenta = precio,
            precioCosto = costo,
            impuestoId
        });
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<ProductoDto>(_jsonOptions);
        return result!.Id;
    }

    private async Task RegistrarStock(Guid productoId, int sucursalId, decimal cantidad)
    {
        var response = await _client.PostAsJsonAsync("/api/v1/Inventario/entrada", new
        {
            productoId, sucursalId, cantidad, costoUnitario = 500m,
            terceroId = _factory.TerceroTestId,
            referencia = $"IMP-{Guid.NewGuid():N}"[..20],
            observaciones = "Stock para test impuestos"
        });
        response.EnsureSuccessStatusCode();
    }

    private async Task<int> CrearYAbrirCaja(string nombre)
    {
        var crear = await _client.PostAsJsonAsync("/api/v1/Cajas", new { nombre, sucursalId = SucPp });
        crear.EnsureSuccessStatusCode();
        var caja = await crear.Content.ReadFromJsonAsync<CajaDto>(_jsonOptions);
        var abrir = await _client.PostAsJsonAsync($"/api/v1/Cajas/{caja!.Id}/abrir", new { montoApertura = 500_000m });
        abrir.EnsureSuccessStatusCode();
        return caja.Id;
    }

    // ═══════════════════════════════════════════════════════
    //  CRUD DE IMPUESTOS
    // ═══════════════════════════════════════════════════════

    [Fact]
    public async Task CrearImpuesto_IVA19_RetornaCreated()
    {
        // Act
        var impuestoId = await CrearImpuesto("IVA Test 19%", "IVA", 0.19m);

        // Assert
        impuestoId.Should().BeGreaterThan(0);

        var response = await _client.GetAsync($"/api/v1/Impuestos/{impuestoId}");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var imp = await response.Content.ReadFromJsonAsync<JsonElement>(_jsonOptions);
        imp.GetProperty("nombre").GetString().Should().Be("IVA Test 19%");
        imp.GetProperty("porcentaje").GetDecimal().Should().Be(0.19m);
        imp.GetProperty("tipo").GetString().Should().Be("IVA");
    }

    [Fact]
    public async Task ObtenerImpuestos_RetornaListaActivos()
    {
        // Act
        var response = await _client.GetAsync("/api/v1/Impuestos");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var lista = await response.Content.ReadFromJsonAsync<JsonElement[]>(_jsonOptions);

        // Assert: debe incluir los seeds (Exento 0%, IVA 5%, IVA 19%, INC 8%, Bolsa)
        lista.Should().NotBeNullOrEmpty();
        lista!.Should().Contain(i => i.GetProperty("tipo").GetString() == "IVA");
    }

    [Fact]
    public async Task EditarImpuesto_ActualizaNombreYPorcentaje()
    {
        // Arrange
        var id = await CrearImpuesto("IVA Edit Test", "IVA", 0.05m);

        // Act
        var editResponse = await _client.PutAsJsonAsync($"/api/v1/Impuestos/{id}", new
        {
            nombre = "IVA Edit Actualizado",
            porcentaje = (decimal?)0.19m,
            descripcion = "Editado en test"
        });
        editResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Assert
        var get = await _client.GetAsync($"/api/v1/Impuestos/{id}");
        var updated = await get.Content.ReadFromJsonAsync<JsonElement>(_jsonOptions);
        updated.GetProperty("nombre").GetString().Should().Be("IVA Edit Actualizado");
        updated.GetProperty("porcentaje").GetDecimal().Should().Be(0.19m);
    }

    [Fact]
    public async Task DesactivarImpuesto_SinProductos_RetornaNoContent()
    {
        // Arrange
        var id = await CrearImpuesto("IVA Para Desactivar", "IVA", 0.19m);

        // Act
        var response = await _client.DeleteAsync($"/api/v1/Impuestos/{id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Verificar que ya no aparece en el listado
        var get = await _client.GetAsync($"/api/v1/Impuestos/{id}");
        get.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DesactivarImpuesto_ConProductosActivos_RetornaBadRequest()
    {
        // Arrange: crear impuesto y asignarlo a un producto
        var impId = await CrearImpuesto("IVA Protegido", "IVA", 0.19m);
        await CrearProductoConImpuesto("IMP-PROT-001", impId);

        // Act: intentar desactivar
        var response = await _client.DeleteAsync($"/api/v1/Impuestos/{impId}");

        // Assert: debe rechazarse — ya está en uso
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ═══════════════════════════════════════════════════════
    //  CÁLCULO DE IMPUESTOS EN VENTAS
    // ═══════════════════════════════════════════════════════

    [Fact]
    public async Task VentaConIVA19_CalculaImpuestoCorrectamente()
    {
        // Arrange: producto con IVA 19% (seed: Id = 3)
        var productoId = await CrearProductoConImpuesto("IMP-IVA-001", impuestoId: 3, precio: 1000m);
        await RegistrarStock(productoId, SucPp, 50);
        var cajaId = await CrearYAbrirCaja("Caja IVA Test");

        // Act: vender 5 unidades a $1000
        var response = await _client.PostAsJsonAsync("/api/v1/Ventas", new
        {
            sucursalId = SucPp, cajaId, metodoPago = 0, montoPagado = 10000m,
            lineas = new[] { new { productoId, cantidad = 5m, precioUnitario = (decimal?)null, descuento = 0m } }
        });
        response.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Body: {await response.Content.ReadAsStringAsync()}");

        var venta = await response.Content.ReadFromJsonAsync<VentaDto>(_jsonOptions);

        // Assert: Subtotal = 5000, Impuesto = 5000 × 0.19 = 950, Total = 5950
        venta.Should().NotBeNull();
        venta.Subtotal.Should().Be(5000m);
        venta.Impuestos.Should().Be(950m, "IVA 19% sobre base 5000");
        venta.Total.Should().Be(5950m);
        venta.Detalles[0].MontoImpuesto.Should().Be(950m);
        venta.Detalles[0].PorcentajeImpuesto.Should().Be(0.19m);
    }

    [Fact]
    public async Task VentaConProductoExento_ImpuestoCero()
    {
        // Arrange: producto con Exento 0% (seed: Id = 1)
        var productoId = await CrearProductoConImpuesto("IMP-EXENTO-001", impuestoId: 1, precio: 2000m);
        await RegistrarStock(productoId, SucPp, 100);
        var cajaId = await CrearYAbrirCaja("Caja Exento Test");

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/Ventas", new
        {
            sucursalId = SucPp, cajaId, metodoPago = 0, montoPagado = 20000m,
            lineas = new[] { new { productoId, cantidad = 3m, precioUnitario = (decimal?)null, descuento = 0m } }
        });
        response.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Body: {await response.Content.ReadAsStringAsync()}");

        var venta = await response.Content.ReadFromJsonAsync<VentaDto>(_jsonOptions);

        // Assert: Exento → Impuesto = 0
        venta!.Impuestos.Should().Be(0m, "producto exento no debe generar impuesto");
        venta.Total.Should().Be(6000m, "3 × 2000 = 6000 sin impuestos");
    }

    [Fact]
    public async Task VentaConProductoSinImpuesto_ImpuestoCero()
    {
        // Arrange: producto sin ImpuestoId asignado
        var productoId = await CrearProductoConImpuesto("IMP-NULL-001", impuestoId: null, precio: 1500m);
        await RegistrarStock(productoId, SucPp, 50);
        var cajaId = await CrearYAbrirCaja("Caja Sin Impuesto Test");

        var response = await _client.PostAsJsonAsync("/api/v1/Ventas", new
        {
            sucursalId = SucPp, cajaId, metodoPago = 0, montoPagado = 10000m,
            lineas = new[] { new { productoId, cantidad = 2m, precioUnitario = (decimal?)null, descuento = 0m } }
        });
        response.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Body: {await response.Content.ReadAsStringAsync()}");

        var venta = await response.Content.ReadFromJsonAsync<VentaDto>(_jsonOptions);
        venta!.Impuestos.Should().Be(0m);
    }

    // ═══════════════════════════════════════════════════════
    //  FACTURA ELECTRÓNICA (5 UVT)
    // ═══════════════════════════════════════════════════════

    [Fact]
    public async Task VentaMayor5UVT_RequiereFacturaElectronica()
    {
        // UVT 2026 = $47.065 → 5 UVT = $235.325
        // Venta de 3 productos a $100.000 = $300.000 > 5 UVT

        var productoId = await CrearProductoConImpuesto("IMP-5UVT-001", impuestoId: null, precio: 100_000m, costo: 50_000m);
        await RegistrarStock(productoId, SucPp, 50);
        var cajaId = await CrearYAbrirCaja("Caja 5UVT Test");

        var response = await _client.PostAsJsonAsync("/api/v1/Ventas", new
        {
            sucursalId = SucPp, cajaId, metodoPago = 0, montoPagado = 500_000m,
            lineas = new[] { new { productoId, cantidad = 3m, precioUnitario = (decimal?)null, descuento = 0m } }
        });
        response.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Body: {await response.Content.ReadAsStringAsync()}");

        var venta = await response.Content.ReadFromJsonAsync<VentaDto>(_jsonOptions);

        // Assert: total $300.000 > $235.325 → debe requerir factura
        venta!.RequiereFacturaElectronica.Should().BeTrue(
            "venta de $300.000 supera el umbral de 5 UVT ($235.325)");
    }

    [Fact]
    public async Task VentaMenor5UVT_NoRequiereFacturaElectronica()
    {
        // Venta de 1 producto a $10.000 = bien por debajo de 5 UVT
        var productoId = await CrearProductoConImpuesto("IMP-MINI-001", impuestoId: null, precio: 10_000m, costo: 5_000m);
        await RegistrarStock(productoId, SucPp, 100);
        var cajaId = await CrearYAbrirCaja("Caja Mini Test");

        var response = await _client.PostAsJsonAsync("/api/v1/Ventas", new
        {
            sucursalId = SucPp, cajaId, metodoPago = 0, montoPagado = 20_000m,
            lineas = new[] { new { productoId, cantidad = 1m, precioUnitario = (decimal?)null, descuento = 0m } }
        });
        response.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Body: {await response.Content.ReadAsStringAsync()}");

        var venta = await response.Content.ReadFromJsonAsync<VentaDto>(_jsonOptions);
        venta!.RequiereFacturaElectronica.Should().BeFalse();
    }

    // ═══════════════════════════════════════════════════════
    //  RETENCIONES
    // ═══════════════════════════════════════════════════════

    [Fact]
    public async Task GetRetenciones_RetornaLista()
    {
        var response = await _client.GetAsync("/api/v1/Retenciones");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var lista = await response.Content.ReadFromJsonAsync<JsonElement[]>(_jsonOptions);
        lista.Should().NotBeNullOrEmpty("los seeds deben incluir al menos 1 retención (ReteFuente)");
    }
}
