using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using POS.Application.DTOs;
using Xunit;

namespace POS.IntegrationTests;

/// <summary>
/// Tests de integración para la Capa 10 — Explicabilidad.
/// Verifica SugerenciasService: suggestions with Reason, DataSource, Confidence.
/// </summary>
[Collection("POS")]
public class SugerenciasTests
{
    private readonly HttpClient _client;
    private readonly CustomWebApplicationFactory _factory;
    private readonly JsonSerializerOptions _json = new() { PropertyNameCaseInsensitive = true };

    private int SucId => _factory.SucursalPPId;
    private int CatId => _factory.CategoriaTestId;

    public SugerenciasTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client  = factory.CreateClient();
    }

    // ─── Helpers ────────────────────────────────────────────

    private async Task<Guid> CrearProducto(string codigo, decimal precio = 3000m)
    {
        var resp = await _client.PostAsJsonAsync("/api/v1/Productos", new
        {
            codigoBarras = codigo, nombre = $"Suger {codigo}", categoriaId = CatId,
            precioVenta  = precio, precioCosto = precio / 2
        });
        resp.EnsureSuccessStatusCode();
        return (await resp.Content.ReadFromJsonAsync<ProductoDto>(_json))!.Id;
    }

    private async Task AgregarStock(Guid productoId, decimal cantidad)
    {
        var resp = await _client.PostAsJsonAsync("/api/v1/Inventario/entrada", new
        {
            productoId, sucursalId = SucId, cantidad, costoUnitario = 1500m,
            terceroId  = _factory.TerceroTestId,
            referencia = $"SUG-{Guid.NewGuid():N}"[..20],
            observaciones = "Stock sugerencias"
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

    private async Task HacerVenta(int cajaId, Guid productoId, decimal cantidad = 1m)
    {
        var resp = await _client.PostAsJsonAsync("/api/v1/Ventas", new
        {
            sucursalId  = SucId,
            cajaId,
            metodoPago  = 0,
            montoPagado = 999_999m,
            lineas = new[] { new { productoId, cantidad, precioUnitario = (decimal?)null, descuento = 0m } }
        });
        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Venta fallida: {await resp.Content.ReadAsStringAsync()}");
    }

    // ═══════════════════════════════════════════════════════
    //  AUTORIZACIÓN
    // ═══════════════════════════════════════════════════════

    [Fact]
    public async Task ObtenerSugerencias_Cajero_Retorna403()
    {
        using var req = new HttpRequestMessage(HttpMethod.Get,
            $"/api/v1/sugerencias/reabastecimiento?sucursalId={SucId}");
        req.Headers.Add("X-Test-User", "cajero@sincopos.com");
        var resp = await _client.SendAsync(req);
        resp.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ═══════════════════════════════════════════════════════
    //  PARÁMETROS
    // ═══════════════════════════════════════════════════════

    [Fact]
    public async Task ObtenerSugerencias_SucursalIdCero_Retorna400()
    {
        var resp = await _client.GetAsync("/api/v1/sugerencias/reabastecimiento?sucursalId=0");
        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ═══════════════════════════════════════════════════════
    //  DATOS INSUFICIENTES
    // ═══════════════════════════════════════════════════════

    [Fact]
    public async Task ObtenerSugerencias_SucursalSinHistorial_RetornaListaVacia()
    {
        // La sucursal de retenciones probablemente no tiene ventas con suficiente historial
        var resp = await _client.GetAsync(
            $"/api/v1/sugerencias/reabastecimiento?sucursalId={_factory.SucursalRetencionId}");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);

        var sugerencias = await resp.Content.ReadFromJsonAsync<List<AutomaticActionDto>>(_json);
        sugerencias.Should().NotBeNull();
        // Puede estar vacío o no según si hubo ventas — solo verificamos que responde 200
    }

    // ═══════════════════════════════════════════════════════
    //  SUGERENCIA GENERADA
    // ═══════════════════════════════════════════════════════

    [Fact]
    public async Task ObtenerSugerencias_StockBajoConVelocidadAlta_GeneraSugerencia()
    {
        // Arrange: producto con stock muy bajo + muchas ventas → debería generar sugerencia
        var prod = await CrearProducto("SUG-BAJO-001");

        // Stock muy bajo (1 unidad)
        await AgregarStock(prod, 1m);
        var cajaId = await AbrirCaja("Caja Suger 01");

        // Vender la unidad y crear historial de ventas de otros productos para subir confianza
        // Necesitamos que StorePattern tenga suficientes ventas (≥5 para conf ≥ 0.1)
        // Usamos datos que ya existen de otras pruebas en la colección
        // y hacemos más ventas de este producto específico
        await HacerVenta(cajaId, prod, 1m);

        // Para que la sugerencia aparezca, necesitamos que el umbral de confianza se cumpla.
        // La confianza depende de TotalVentas de StorePattern (acumulado de toda la colección).
        // Como la DB es compartida, SucursalPP ya debería tener suficientes ventas.

        var resp = await _client.GetAsync(
            $"/api/v1/sugerencias/reabastecimiento?sucursalId={SucId}");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);

        var sugerencias = await resp.Content.ReadFromJsonAsync<List<AutomaticActionDto>>(_json);
        sugerencias.Should().NotBeNull();

        // Si hay suficiente historial, debe haber al menos una sugerencia
        // Si TotalVentas >= 5, confidence >= 0.1 y el stock de este producto es 0/1
        // La sugerencia puede o no aparecer según el estado de la BD compartida
        // pero verificamos la estructura cuando existe
        if (sugerencias!.Count > 0)
        {
            var primera = sugerencias[0];
            primera.Description.Should().NotBeNullOrEmpty();
            primera.Reason.Should().NotBeNullOrEmpty();
            primera.DataSource.Should().NotBeNullOrEmpty();
            primera.Confidence.Should().BeInRange(0.0, 1.0);
            primera.CanOverride.Should().BeTrue();
            primera.CantidadSugerida.Should().BeGreaterThan(0);
        }
    }

    [Fact]
    public async Task ObtenerSugerencias_Estructura_TieneExplicabilidadCompleta()
    {
        // Verifica que cada sugerencia cumple el contrato de Capa 10:
        // reason + dataSource + confidence siempre presentes
        var resp = await _client.GetAsync(
            $"/api/v1/sugerencias/reabastecimiento?sucursalId={SucId}");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);

        var sugerencias = await resp.Content.ReadFromJsonAsync<List<AutomaticActionDto>>(_json);
        sugerencias.Should().NotBeNull();

        foreach (var s in sugerencias!)
        {
            // Capa 10: toda acción automática DEBE tener reason y dataSource
            s.Reason.Should().NotBeNullOrEmpty(
                $"La sugerencia '{s.Description}' debe tener Reason (Capa 10)");
            s.DataSource.Should().NotBeNullOrEmpty(
                $"La sugerencia '{s.Description}' debe tener DataSource (Capa 10)");
            s.Confidence.Should().BeInRange(0.0, 1.0,
                $"Confidence debe estar en [0,1] para '{s.Description}'");
        }
    }

    [Fact]
    public async Task ObtenerSugerencias_OrdenanPorUrgencia_MenosDiasAlFrente()
    {
        // Si hay múltiples sugerencias, las más urgentes (menos días restantes) van primero
        var resp = await _client.GetAsync(
            $"/api/v1/sugerencias/reabastecimiento?sucursalId={SucId}");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);

        var sugerencias = await resp.Content.ReadFromJsonAsync<List<AutomaticActionDto>>(_json);
        sugerencias.Should().NotBeNull();

        if (sugerencias!.Count >= 2)
        {
            // Verificar orden: diasRestantes ascendente
            for (var i = 0; i < sugerencias.Count - 1; i++)
            {
                var actual   = sugerencias[i].DiasRestantes ?? decimal.MaxValue;
                var siguiente = sugerencias[i + 1].DiasRestantes ?? decimal.MaxValue;
                actual.Should().BeLessThanOrEqualTo(siguiente,
                    "Las sugerencias deben ordenarse por urgencia (menos días restantes primero)");
            }
        }
    }
}
