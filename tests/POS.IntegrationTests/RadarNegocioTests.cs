using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using POS.Application.DTOs;
using POS.Domain.Aggregates;
using Xunit;

namespace POS.IntegrationTests;

/// <summary>
/// Tests de integración para la Capa 14 — Radar de Negocio.
/// Verifica BusinessRiskProjection (Marten) + RadarNegocioService (EF Core)
/// a través de RadarController.
/// </summary>
[Collection("POS")]
public class RadarNegocioTests
{
    private readonly HttpClient _client;
    private readonly CustomWebApplicationFactory _factory;
    private readonly JsonSerializerOptions _json = new() { PropertyNameCaseInsensitive = true };

    private int SucId => _factory.SucursalPPId;
    private int CatId => _factory.CategoriaTestId;

    public RadarNegocioTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client  = factory.CreateClient();
    }

    // ─── Helpers ────────────────────────────────────────────

    private async Task<Guid> CrearProducto(string codigo, decimal precio = 3000m)
    {
        var resp = await _client.PostAsJsonAsync("/api/v1/Productos", new
        {
            codigoBarras = codigo, nombre = $"Radar {codigo}", categoriaId = CatId,
            precioVenta  = precio, precioCosto = precio / 2
        });
        resp.EnsureSuccessStatusCode();
        return (await resp.Content.ReadFromJsonAsync<ProductoDto>(_json))!.Id;
    }

    private async Task AgregarStock(Guid productoId, decimal cantidad = 100)
    {
        var resp = await _client.PostAsJsonAsync("/api/v1/Inventario/entrada", new
        {
            productoId, sucursalId = SucId, cantidad, costoUnitario = 1500m,
            terceroId  = _factory.TerceroTestId,
            referencia = $"RDR-{Guid.NewGuid():N}"[..20],
            observaciones = "Stock radar"
        });
        resp.EnsureSuccessStatusCode();
    }

    private async Task<int> AbrirCaja(string nombre)
    {
        var crear = await _client.PostAsJsonAsync("/api/v1/Cajas", new { nombre, sucursalId = SucId });
        crear.EnsureSuccessStatusCode();
        var caja = await crear.Content.ReadFromJsonAsync<CajaDto>(_json);
        await _client.PostAsJsonAsync($"/api/v1/Cajas/{caja!.Id}/abrir", new { montoApertura = 50_000m });
        return caja.Id;
    }

    private async Task HacerVenta(int cajaId, Guid productoId, decimal cantidad = 2m)
    {
        var resp = await _client.PostAsJsonAsync("/api/v1/Ventas", new
        {
            sucursalId  = SucId,
            cajaId,
            clienteId   = _factory.TerceroTestId,
            metodoPago  = 0,
            montoPagado = 999_999m,
            lineas      = new[]
            {
                new { productoId, cantidad, precioUnitario = (decimal?)null, descuento = 0m }
            }
        });
        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Venta fallida: {await resp.Content.ReadAsStringAsync()}");
    }

    // ═══════════════════════════════════════════════════════
    //  AUTORIZACIÓN
    // ═══════════════════════════════════════════════════════

    [Fact]
    public async Task ObtenerRadar_Cajero_Retorna403()
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/radar/sucursal/{SucId}");
        req.Headers.Add("X-Test-User", "cajero@sincopos.com");
        var resp = await _client.SendAsync(req);
        resp.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task ObtenerPatron_Cajero_Retorna403()
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/radar/sucursal/{SucId}/patron");
        req.Headers.Add("X-Test-User", "cajero@sincopos.com");
        var resp = await _client.SendAsync(req);
        resp.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ═══════════════════════════════════════════════════════
    //  SUCURSAL INEXISTENTE
    // ═══════════════════════════════════════════════════════

    [Fact]
    public async Task ObtenerRadar_SucursalInexistente_Retorna204()
    {
        var resp = await _client.GetAsync("/api/v1/radar/sucursal/99999");
        resp.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task ObtenerPatron_SucursalSinVentas_Retorna204()
    {
        // SucursalLIFOId no tiene ventas si no se crearon en otros tests de la colección
        // Usamos un SucursalId de la factory que no tiene ventas en el contexto actual
        // (siempre es posible que SucursalFIFO tenga ventas de otros tests, así que
        //  usamos 99998 que sabemos que no existe)
        var resp = await _client.GetAsync("/api/v1/radar/sucursal/99998/patron");
        resp.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    // ═══════════════════════════════════════════════════════
    //  DATOS REALES
    // ═══════════════════════════════════════════════════════

    [Fact]
    public async Task ObtenerRadar_DespuesDeVentas_RetornaMetricasCorrectas()
    {
        // Arrange: 2 productos, precio conocido → validar VentasTotales
        var prod1 = await CrearProducto("RDR-A-001", 5000m);
        var prod2 = await CrearProducto("RDR-A-002", 2000m);
        await AgregarStock(prod1, 50);
        await AgregarStock(prod2, 50);
        var cajaId = await AbrirCaja("Caja Radar 01");

        // Act: 2 ventas
        await HacerVenta(cajaId, prod1, 1m);  // Total mínimo 5000
        await HacerVenta(cajaId, prod2, 1m);  // Total mínimo 2000

        // Assert
        var resp = await _client.GetAsync($"/api/v1/radar/sucursal/{SucId}");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);

        var radar = await resp.Content.ReadFromJsonAsync<RadarNegocioDto>(_json);
        radar.Should().NotBeNull();
        radar!.MetricasHoy.CantidadVentas.Should().BeGreaterThanOrEqualTo(2);
        radar.MetricasHoy.VentasTotales.Should().BeGreaterThan(0);
        radar.MetricasHoy.TicketPromedio.Should().BeGreaterThan(0);
        radar.VentasPorHora.Should().NotBeNull();
    }

    [Fact]
    public async Task ObtenerPatron_DespuesDeVentas_RetornaBusinessRadarConVelocidad()
    {
        // Arrange: producto único para validar velocidad exacta
        var prod = await CrearProducto("RDR-VEL-001");
        await AgregarStock(prod, 100);
        var cajaId = await AbrirCaja("Caja Radar 02");

        // Act: 2 ventas, 3 unidades cada una → velocidad esperada = 6
        await HacerVenta(cajaId, prod, 3m);
        await HacerVenta(cajaId, prod, 3m);

        // Assert
        var resp = await _client.GetAsync($"/api/v1/radar/sucursal/{SucId}/patron");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);

        var patron = await resp.Content.ReadFromJsonAsync<BusinessRadar>(_json);
        patron.Should().NotBeNull();
        patron!.SucursalId.Should().Be(SucId);
        patron.IngresosPorFecha.Should().NotBeEmpty();
        patron.ProductoVelocidad.Should().ContainKey(prod.ToString());
        // Velocidad exacta: 6 (3+3) — única en toda la colección porque el código es único
        patron.ProductoVelocidad[prod.ToString()].Should().Be(6m);
    }

    [Fact]
    public async Task ObtenerPatron_IngresosPorHora_SeAcumulaCorrectamente()
    {
        // Arrange
        var prod = await CrearProducto("RDR-HORA-001", 10_000m);
        await AgregarStock(prod, 100);
        var cajaId = await AbrirCaja("Caja Radar 03");

        // Act: 1 venta
        await HacerVenta(cajaId, prod, 1m);

        // Assert: debe haber al menos 1 registro en IngresosPorFechaHora
        var resp = await _client.GetAsync($"/api/v1/radar/sucursal/{SucId}/patron");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);

        var patron = await resp.Content.ReadFromJsonAsync<BusinessRadar>(_json);
        patron.Should().NotBeNull();
        patron!.IngresosPorFechaHora.Should().NotBeEmpty();
        // La suma total de IngresosPorFechaHora debe ser >= IngresosPorFecha (mismos datos)
        var totalHora  = patron.IngresosPorFechaHora.Values.Sum();
        var totalFecha = patron.IngresosPorFecha.Values.Sum();
        totalHora.Should().BeApproximately(totalFecha, 0.01m);
    }

    [Fact]
    public async Task ObtenerRadar_RiesgosStock_IncluirProductosBajoMinimo()
    {
        // Arrange: producto con stock mínimo alto (al umbral) para forzar alerta
        var prod = await CrearProducto("RDR-RIESGO-001", 1000m);

        // Agregar stock de 1 unidad
        var entradaResp = await _client.PostAsJsonAsync("/api/v1/Inventario/entrada", new
        {
            productoId  = prod,
            sucursalId  = SucId,
            cantidad    = 1m,
            costoUnitario = 500m,
            terceroId   = _factory.TerceroTestId,
            referencia  = "RDR-RIESGO-ENT",
            observaciones = "Stock mínimo para radar"
        });
        entradaResp.EnsureSuccessStatusCode();

        // Establecer stock mínimo = 10 (hace que 1 ud esté bajo mínimo)
        var minimoResp = await _client.PutAsync(
            $"/api/v1/Inventario/stock-minimo?productoId={prod}&sucursalId={SucId}&stockMinimo=10",
            null);
        minimoResp.EnsureSuccessStatusCode();

        // Assert: el radar debe incluirlo en RiesgosStock
        var resp = await _client.GetAsync($"/api/v1/radar/sucursal/{SucId}");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);

        var radar = await resp.Content.ReadFromJsonAsync<RadarNegocioDto>(_json);
        radar.Should().NotBeNull();
        radar!.RiesgosStock.Should().Contain(r => r.ProductoId == prod);
    }
}
