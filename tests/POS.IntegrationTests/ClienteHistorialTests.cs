using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using POS.Application.DTOs;
using Xunit;

namespace POS.IntegrationTests;

/// <summary>
/// Tests de integración para la Capa 4 — Dependencias inteligentes.
/// Verifica ClienteHistorialProjection: historial acumulado de compras por cliente.
/// </summary>
[Collection("POS")]
public class ClienteHistorialTests
{
    private readonly HttpClient _client;
    private readonly CustomWebApplicationFactory _factory;
    private readonly JsonSerializerOptions _json = new() { PropertyNameCaseInsensitive = true };

    private int SucId => _factory.SucursalPPId;
    private int CatId => _factory.CategoriaTestId;

    public ClienteHistorialTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client  = factory.CreateClient();
    }

    // ─── Helpers ────────────────────────────────────────────────────────────

    private async Task<Guid> CrearProducto(string codigo, decimal precio = 4000m)
    {
        var resp = await _client.PostAsJsonAsync("/api/v1/Productos", new
        {
            codigoBarras = codigo, nombre = $"HistCli {codigo}", categoriaId = CatId,
            precioVenta  = precio, precioCosto = precio / 2
        });
        resp.EnsureSuccessStatusCode();
        return (await resp.Content.ReadFromJsonAsync<ProductoDto>(_json))!.Id;
    }

    private async Task AgregarStock(Guid productoId, decimal cantidad)
    {
        var resp = await _client.PostAsJsonAsync("/api/v1/Inventario/entrada", new
        {
            productoId, sucursalId = SucId, cantidad, costoUnitario = 2000m,
            terceroId  = _factory.TerceroTestId,
            referencia = $"HIST-{Guid.NewGuid():N}"[..20],
            observaciones = "Stock historial"
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

    private async Task HacerVenta(int cajaId, int clienteId, IEnumerable<(Guid productoId, decimal cantidad)> lineas)
    {
        var resp = await _client.PostAsJsonAsync("/api/v1/Ventas", new
        {
            sucursalId  = SucId,
            cajaId,
            clienteId,
            metodoPago  = 0,
            montoPagado = 999_999m,
            lineas = lineas.Select(l => new { productoId = l.productoId, cantidad = l.cantidad, precioUnitario = (decimal?)null, descuento = 0m }).ToArray()
        });
        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Venta fallida: {await resp.Content.ReadAsStringAsync()}");
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  AUTORIZACIÓN
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task ObtenerHistorial_Cajero_Retorna200()
    {
        // Cajero puede consultar historial (policy "Cajero")
        using var req = new HttpRequestMessage(HttpMethod.Get,
            $"/api/v1/Terceros/{_factory.TerceroTestId}/historial");
        req.Headers.Add("X-Test-User", "cajero@sincopos.com");
        var resp = await _client.SendAsync(req);
        // Puede ser 200 o 204 según si hay historial — pero nunca 403
        resp.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NoContent);
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  SIN HISTORIAL
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task ObtenerHistorial_ClienteSinVentas_Retorna204()
    {
        // Crear cliente nuevo sin ninguna venta
        var crearResp = await _client.PostAsJsonAsync("/api/v1/Terceros", new
        {
            TipoIdentificacion   = "CC",
            Identificacion       = $"HN-{Guid.NewGuid():N}"[..12],
            Nombre               = "Cliente Sin Historial Test",
            TipoTercero          = "Cliente"
        });
        crearResp.EnsureSuccessStatusCode();
        var tercero = await crearResp.Content.ReadFromJsonAsync<TerceroDto>(_json);

        var resp = await _client.GetAsync($"/api/v1/Terceros/{tercero!.Id}/historial");
        resp.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  PROYECCIÓN ACUMULA CORRECTAMENTE
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task ObtenerHistorial_TrasVentas_AcumulaTotalComprasYGasto()
    {
        // Arrange: producto + stock + caja + cliente
        var prod    = await CrearProducto("HIST-ACUM-001", precio: 5000m);
        await AgregarStock(prod, 50m);
        var cajaId  = await AbrirCaja("Caja Hist 01");

        // Crear cliente específico para este test
        var crearResp = await _client.PostAsJsonAsync("/api/v1/Terceros", new
        {
            TipoIdentificacion = "CC",
            Identificacion     = $"HA-{Guid.NewGuid():N}"[..12],
            Nombre             = "Cliente Historial Acum",
            TipoTercero        = "Cliente"
        });
        crearResp.EnsureSuccessStatusCode();
        var tercero = await crearResp.Content.ReadFromJsonAsync<TerceroDto>(_json);
        var clienteId = tercero!.Id;

        // Act: 2 ventas del mismo cliente
        await HacerVenta(cajaId, clienteId, [(prod, 2m)]);
        await HacerVenta(cajaId, clienteId, [(prod, 3m)]);

        // Assert
        var resp = await _client.GetAsync($"/api/v1/Terceros/{clienteId}/historial");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);

        var historial = await resp.Content.ReadFromJsonAsync<ClienteHistorialDto>(_json);
        historial.Should().NotBeNull();
        historial!.TotalCompras.Should().Be(2);
        historial.TotalGastado.Should().BeGreaterThan(0m);
        historial.GastoPromedio.Should().BeGreaterThan(0m);
        historial.UltimaVisita.Should().NotBeNull();
        historial.PrimeraVisita.Should().NotBeNull();
    }

    [Fact]
    public async Task ObtenerHistorial_TrasVentas_AcumulaTopProductos()
    {
        // Arrange
        var prodA = await CrearProducto("HIST-TOP-A01", precio: 3000m);
        var prodB = await CrearProducto("HIST-TOP-B01", precio: 2000m);
        await AgregarStock(prodA, 30m);
        await AgregarStock(prodB, 30m);
        var cajaId = await AbrirCaja("Caja Hist 02");

        var crearResp = await _client.PostAsJsonAsync("/api/v1/Terceros", new
        {
            TipoIdentificacion = "CC",
            Identificacion     = $"TP-{Guid.NewGuid():N}"[..12],
            Nombre             = "Cliente Top Productos",
            TipoTercero        = "Cliente"
        });
        crearResp.EnsureSuccessStatusCode();
        var tercero   = await crearResp.Content.ReadFromJsonAsync<TerceroDto>(_json);
        var clienteId = tercero!.Id;

        // Act: vender A más veces que B
        await HacerVenta(cajaId, clienteId, [(prodA, 3m), (prodB, 1m)]);
        await HacerVenta(cajaId, clienteId, [(prodA, 2m)]);

        // Assert
        var resp = await _client.GetAsync($"/api/v1/Terceros/{clienteId}/historial");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);

        var historial = await resp.Content.ReadFromJsonAsync<ClienteHistorialDto>(_json);
        historial!.TopProductos.Should().NotBeEmpty();

        // El producto A debe estar primero (más unidades compradas)
        historial.TopProductos[0].ProductoId.Should().Be(prodA.ToString());
        historial.TopProductos[0].CantidadTotal.Should().Be(5); // 3 + 2
    }

    [Fact]
    public async Task ObtenerHistorial_Estructura_TieneFrecuenciasVisita()
    {
        // Una venta deja huella en VisitasPorDiaSemana y VisitasPorHora
        var prod = await CrearProducto("HIST-FREQ-001", precio: 2500m);
        await AgregarStock(prod, 20m);
        var cajaId = await AbrirCaja("Caja Hist 03");

        var crearResp = await _client.PostAsJsonAsync("/api/v1/Terceros", new
        {
            TipoIdentificacion = "CC",
            Identificacion     = $"FQ-{Guid.NewGuid():N}"[..12],
            Nombre             = "Cliente Frecuencia",
            TipoTercero        = "Cliente"
        });
        crearResp.EnsureSuccessStatusCode();
        var tercero   = await crearResp.Content.ReadFromJsonAsync<TerceroDto>(_json);
        var clienteId = tercero!.Id;

        await HacerVenta(cajaId, clienteId, [(prod, 1m)]);

        var resp = await _client.GetAsync($"/api/v1/Terceros/{clienteId}/historial");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);

        var historial = await resp.Content.ReadFromJsonAsync<ClienteHistorialDto>(_json);
        historial!.VisitasPorDiaSemana.Should().NotBeEmpty("debe haber al menos un día registrado");
        historial.VisitasPorHora.Should().NotBeEmpty("debe haber al menos una hora registrada");
        historial.VisitasPorDiaSemana.Values.Sum().Should().BeGreaterThan(0);
    }
}
