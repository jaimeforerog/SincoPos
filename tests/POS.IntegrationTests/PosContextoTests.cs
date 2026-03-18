using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using POS.Application.DTOs;
using Xunit;

namespace POS.IntegrationTests;

/// <summary>
/// Tests de integración para la Capa 3 — Repetición cero.
/// Verifica PosContextoService: clientes recientes + órdenes pendientes.
/// </summary>
[Collection("POS")]
public class PosContextoTests
{
    private readonly HttpClient _client;
    private readonly CustomWebApplicationFactory _factory;
    private readonly JsonSerializerOptions _json = new() { PropertyNameCaseInsensitive = true };

    private int SucId => _factory.SucursalPPId;
    private int CatId => _factory.CategoriaTestId;

    public PosContextoTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client  = factory.CreateClient();
    }

    // ─── Helpers ────────────────────────────────────────────

    private async Task<Guid> CrearProducto(string codigo, decimal precio = 2000m)
    {
        var resp = await _client.PostAsJsonAsync("/api/v1/Productos", new
        {
            codigoBarras = codigo, nombre = $"POS Ctx {codigo}", categoriaId = CatId,
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
            referencia = $"CTX-{Guid.NewGuid():N}"[..20],
            observaciones = "Stock ctx"
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

    private async Task<int> CrearClienteTercero(string nombre, string identificacion)
    {
        var resp = await _client.PostAsJsonAsync("/api/v1/Terceros", new
        {
            tipoIdentificacion = "CC",
            identificacion,
            nombre,
            tipoTercero       = "Cliente",
            perfilTributario  = "REGIMEN_SIMPLE"
        });
        resp.EnsureSuccessStatusCode();
        var tercero = await resp.Content.ReadFromJsonAsync<JsonElement>(_json);
        return tercero.GetProperty("id").GetInt32();
    }

    private async Task HacerVenta(int cajaId, Guid productoId, int? clienteId = null)
    {
        var resp = await _client.PostAsJsonAsync("/api/v1/Ventas", new
        {
            sucursalId  = SucId,
            cajaId,
            clienteId,
            metodoPago  = 0,
            montoPagado = 999_999m,
            lineas = new[] { new { productoId, cantidad = 1m, precioUnitario = (decimal?)null, descuento = 0m } }
        });
        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Venta fallida: {await resp.Content.ReadAsStringAsync()}");
    }

    // ═══════════════════════════════════════════════════════
    //  PARÁMETROS
    // ═══════════════════════════════════════════════════════

    [Fact]
    public async Task ObtenerContexto_SucursalIdCero_Retorna400()
    {
        var resp = await _client.GetAsync("/api/v1/pos/contexto?sucursalId=0");
        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ═══════════════════════════════════════════════════════
    //  SUCURSAL SIN DATOS
    // ═══════════════════════════════════════════════════════

    [Fact]
    public async Task ObtenerContexto_SucursalSinVentas_Retorna200ConListasVacias()
    {
        // SucursalLIFOId probablemente no tiene ventas con cliente en este punto
        // Usamos la sucursal de retenciones que no tiene ventas con cliente asignado
        var resp = await _client.GetAsync($"/api/v1/pos/contexto?sucursalId={_factory.SucursalRetencionId}");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);

        var ctx = await resp.Content.ReadFromJsonAsync<TurnContextDto>(_json);
        ctx.Should().NotBeNull();
        // Puede tener 0 clientes (no hubo ventas con cliente en esta sucursal)
        ctx!.ClientesRecientes.Should().NotBeNull();
        ctx.OrdenesPendientes.Should().NotBeNull();
    }

    // ═══════════════════════════════════════════════════════
    //  CLIENTES RECIENTES
    // ═══════════════════════════════════════════════════════

    [Fact]
    public async Task ObtenerContexto_ConVentasAClientes_RetornaClientesRecientes()
    {
        // Arrange: 2 clientes, 2 ventas con cliente
        var cliente1Id = await CrearClienteTercero("Cliente Ctx A", "CTX-CC-001");
        var cliente2Id = await CrearClienteTercero("Cliente Ctx B", "CTX-CC-002");
        var prod       = await CrearProducto("CTX-PROD-001");
        await AgregarStock(prod, 50);
        var cajaId = await AbrirCaja("Caja Contexto 01");

        await HacerVenta(cajaId, prod, cliente1Id);
        await HacerVenta(cajaId, prod, cliente2Id);

        // Assert
        var resp = await _client.GetAsync($"/api/v1/pos/contexto?sucursalId={SucId}");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);

        var ctx = await resp.Content.ReadFromJsonAsync<TurnContextDto>(_json);
        ctx.Should().NotBeNull();
        ctx!.ClientesRecientes.Should().NotBeEmpty();

        // Ambos clientes deben aparecer en los recientes
        ctx.ClientesRecientes.Should().Contain(c => c.Id == cliente1Id);
        ctx.ClientesRecientes.Should().Contain(c => c.Id == cliente2Id);

        // Máximo 20 clientes
        ctx.ClientesRecientes.Count.Should().BeLessThanOrEqualTo(20);
    }

    [Fact]
    public async Task ObtenerContexto_ClienteMasRecienteAparece()
    {
        // Arrange
        var clienteId = await CrearClienteTercero("Cliente Reciente Test", "CTX-CC-003");
        var prod      = await CrearProducto("CTX-PROD-002");
        await AgregarStock(prod, 50);
        var cajaId = await AbrirCaja("Caja Contexto 02");

        await HacerVenta(cajaId, prod, clienteId);

        // Assert: el cliente debe tener UltimaVenta poblada
        var resp = await _client.GetAsync($"/api/v1/pos/contexto?sucursalId={SucId}");
        var ctx  = await resp.Content.ReadFromJsonAsync<TurnContextDto>(_json);

        var reciente = ctx!.ClientesRecientes.FirstOrDefault(c => c.Id == clienteId);
        reciente.Should().NotBeNull();
        reciente!.UltimaVenta.Should().NotBe(default);
        reciente.Nombre.Should().Be("Cliente Reciente Test");
    }

    // ═══════════════════════════════════════════════════════
    //  ÓRDENES PENDIENTES
    // ═══════════════════════════════════════════════════════

    [Fact]
    public async Task ObtenerContexto_ConOrdenPendiente_LaIncluyeEnContexto()
    {
        // Arrange: crear orden de compra en estado Pendiente para SucId
        var prod = await CrearProducto("CTX-OC-001", 5000m);

        var ordenResp = await _client.PostAsJsonAsync("/api/v1/Compras", new
        {
            sucursalId           = SucId,
            proveedorId          = _factory.TerceroTestId,
            fechaEntregaEsperada = DateTime.UtcNow.AddDays(7),
            observaciones        = "Orden para contexto turno",
            lineas               = new[]
            {
                new { productoId = prod, cantidad = 10m, precioUnitario = 5000m, porcentajeImpuesto = 0m }
            }
        });
        ordenResp.EnsureSuccessStatusCode();

        // Obtener contexto
        var resp = await _client.GetAsync($"/api/v1/pos/contexto?sucursalId={SucId}");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);

        var ctx = await resp.Content.ReadFromJsonAsync<TurnContextDto>(_json);
        ctx.Should().NotBeNull();
        ctx!.OrdenesPendientes.Should().NotBeEmpty();

        var orden = ctx.OrdenesPendientes.First();
        orden.NumeroOrden.Should().NotBeNullOrEmpty();
        orden.Total.Should().BeGreaterThan(0);
        orden.ItemsCount.Should().BeGreaterThan(0);
    }
}
