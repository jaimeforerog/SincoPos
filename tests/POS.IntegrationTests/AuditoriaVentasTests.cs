using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using POS.Application.DTOs;

namespace POS.IntegrationTests;

/// <summary>
/// Tests de integración para el endpoint de Auditoría de Ventas.
/// Requiere rol supervisor o admin — usa AuthenticatedWebApplicationFactory.
/// </summary>
[Collection("POS-Auth")]
public class AuditoriaVentasTests
{
    private readonly AuthenticatedWebApplicationFactory _factory;
    private readonly JsonSerializerOptions _json = new() { PropertyNameCaseInsensitive = true };

    private int SucId => _factory.SucursalPPId;
    private int CatId => _factory.CategoriaTestId;
    private int TercId => _factory.TerceroTestId;

    public AuditoriaVentasTests(AuthenticatedWebApplicationFactory factory)
    {
        _factory = factory;
    }

    // ─── Helpers ────────────────────────────────────────────────

    private async Task<Guid> CrearProducto(HttpClient client, string codigo,
        decimal precioVenta = 2000m, decimal precioCosto = 1000m)
    {
        var r = await client.PostAsJsonAsync("/api/v1/Productos", new
        {
            codigoBarras = codigo, nombre = $"Prod {codigo}",
            categoriaId = CatId, precioVenta, precioCosto
        });
        r.EnsureSuccessStatusCode();
        return (await r.Content.ReadFromJsonAsync<ProductoDto>(_json))!.Id;
    }

    private async Task RegistrarEntrada(HttpClient client, Guid productoId,
        decimal cantidad, decimal costo = 1000m)
    {
        var r = await client.PostAsJsonAsync("/api/v1/Inventario/entrada", new
        {
            productoId, sucursalId = SucId, cantidad, costoUnitario = costo,
            terceroId = TercId, referencia = $"AV-{Guid.NewGuid():N}"[..20]
        });
        r.EnsureSuccessStatusCode();
    }

    private async Task<int> CrearAbrirCaja(HttpClient client, string nombre)
    {
        var r1 = await client.PostAsJsonAsync("/api/v1/Cajas", new { nombre, sucursalId = SucId });
        r1.EnsureSuccessStatusCode();
        var caja = await r1.Content.ReadFromJsonAsync<CajaDto>(_json);
        var r2 = await client.PostAsJsonAsync($"/api/v1/Cajas/{caja!.Id}/abrir",
            new { montoApertura = 50_000m });
        r2.EnsureSuccessStatusCode();
        return caja.Id;
    }

    private async Task<int> CrearVenta(HttpClient client, int cajaId, Guid productoId,
        int cantidad = 2)
    {
        var r = await client.PostAsJsonAsync("/api/v1/Ventas", new
        {
            sucursalId = SucId,
            cajaId,
            clienteId = TercId,
            metodoPago = 0,
            montoPagado = 999_999m,
            lineas = new[]
            {
                new { productoId, cantidad = (decimal)cantidad, precioUnitario = (decimal?)null, descuento = 0m }
            }
        });
        r.EnsureSuccessStatusCode();
        return (await r.Content.ReadFromJsonAsync<VentaDto>(_json))!.Id;
    }

    // ─── Tests de autorización ────────────────────────────────────────

    [Fact]
    public async Task AuditoriaVentas_CajeroSinPermiso_Retorna403()
    {
        var cajeroClient = _factory.CreateAuthenticatedClient("cajero@sincopos.com");
        var desde = DateTime.UtcNow.AddDays(-1).ToString("yyyy-MM-ddTHH:mm:ssZ");
        var hasta = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ");

        var r = await cajeroClient.GetAsync(
            $"/api/v1/Reportes/auditoria-ventas?fechaDesde={desde}&fechaHasta={hasta}");

        r.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task HistorialVenta_CajeroSinPermiso_Retorna403()
    {
        var cajeroClient = _factory.CreateAuthenticatedClient("cajero@sincopos.com");

        var r = await cajeroClient.GetAsync("/api/v1/Reportes/auditoria-ventas/venta/1");

        r.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ─── Tests de validación ────────────────────────────────────────

    [Fact]
    public async Task AuditoriaVentas_FechaInvalida_Retorna400()
    {
        var adminClient = _factory.CreateAuthenticatedClient("admin@sincopos.com");
        var desde = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ");
        var hasta = DateTime.UtcNow.AddDays(-1).ToString("yyyy-MM-ddTHH:mm:ssZ");

        var r = await adminClient.GetAsync(
            $"/api/v1/Reportes/auditoria-ventas?fechaDesde={desde}&fechaHasta={hasta}");

        r.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ─── Tests de contenido ────────────────────────────────────────

    [Fact]
    public async Task AuditoriaVentas_PeriodoSinActividad_RetornaKpisEnCero()
    {
        var supervisorClient = _factory.CreateAuthenticatedClient("supervisor@sincopos.com");
        var desde = DateTime.UtcNow.AddYears(-10).ToString("yyyy-MM-ddTHH:mm:ssZ");
        var hasta = DateTime.UtcNow.AddYears(-9).ToString("yyyy-MM-ddTHH:mm:ssZ");

        var r = await supervisorClient.GetAsync(
            $"/api/v1/Reportes/auditoria-ventas?fechaDesde={desde}&fechaHasta={hasta}");

        r.StatusCode.Should().Be(HttpStatusCode.OK);
        var reporte = await r.Content.ReadFromJsonAsync<ReporteAuditoriaVentasDto>(_json);
        reporte!.Kpis.TotalEventos.Should().Be(0);
        reporte.Kpis.TotalVentas.Should().Be(0);
        reporte.Kpis.TotalAnulaciones.Should().Be(0);
        reporte.Kpis.ValorTotalVendido.Should().Be(0);
        reporte.Logs.Items.Should().BeEmpty();
        reporte.Logs.TotalCount.Should().Be(0);
    }

    [Fact]
    public async Task AuditoriaVentas_ConVenta_RegistraEventoCrearVenta()
    {
        // Arrange
        var adminClient = _factory.CreateAuthenticatedClient("admin@sincopos.com");
        var cod = $"AV-EV-{Guid.NewGuid():N}"[..14];
        var productoId = await CrearProducto(adminClient, cod, precioVenta: 3000m, precioCosto: 1200m);
        await RegistrarEntrada(adminClient, productoId, 10);
        var cajaId = await CrearAbrirCaja(adminClient, $"Caja-AV-{cod}");
        await CrearVenta(adminClient, cajaId, productoId, cantidad: 2);

        var supervisorClient = _factory.CreateAuthenticatedClient("supervisor@sincopos.com");
        var desde = DateTime.UtcNow.AddMinutes(-5).ToString("yyyy-MM-ddTHH:mm:ssZ");
        var hasta = DateTime.UtcNow.AddMinutes(5).ToString("yyyy-MM-ddTHH:mm:ssZ");

        // Act
        var r = await supervisorClient.GetAsync(
            $"/api/v1/Reportes/auditoria-ventas?fechaDesde={desde}&fechaHasta={hasta}&sucursalId={SucId}");

        // Assert
        r.StatusCode.Should().Be(HttpStatusCode.OK);
        var reporte = await r.Content.ReadFromJsonAsync<ReporteAuditoriaVentasDto>(_json);
        reporte!.Logs.Items.Should().Contain(log => log.Accion == "CrearVenta");
        reporte.Kpis.EventosPorAccion.Should().ContainKey("CrearVenta");
        reporte.Kpis.EventosPorAccion["CrearVenta"].Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task AuditoriaVentas_KpisReflejanVentasReales()
    {
        // Arrange: dos ventas en el mismo período
        var adminClient = _factory.CreateAuthenticatedClient("admin@sincopos.com");
        var cod = $"AV-KPI-{Guid.NewGuid():N}"[..13];
        var productoId = await CrearProducto(adminClient, cod, precioVenta: 4000m, precioCosto: 1500m);
        await RegistrarEntrada(adminClient, productoId, 20);
        var cajaId = await CrearAbrirCaja(adminClient, $"Caja-KPI-{cod}");
        await CrearVenta(adminClient, cajaId, productoId, cantidad: 3);
        await CrearVenta(adminClient, cajaId, productoId, cantidad: 2);

        var supervisorClient = _factory.CreateAuthenticatedClient("supervisor@sincopos.com");
        var desde = DateTime.UtcNow.AddMinutes(-5).ToString("yyyy-MM-ddTHH:mm:ssZ");
        var hasta = DateTime.UtcNow.AddMinutes(5).ToString("yyyy-MM-ddTHH:mm:ssZ");

        // Act
        var r = await supervisorClient.GetAsync(
            $"/api/v1/Reportes/auditoria-ventas?fechaDesde={desde}&fechaHasta={hasta}&sucursalId={SucId}");

        // Assert
        r.StatusCode.Should().Be(HttpStatusCode.OK);
        var reporte = await r.Content.ReadFromJsonAsync<ReporteAuditoriaVentasDto>(_json);
        reporte!.Kpis.TotalVentas.Should().BeGreaterThanOrEqualTo(2);
        reporte.Kpis.ValorTotalVendido.Should().BeGreaterThan(0);
        reporte.Kpis.TotalAnulaciones.Should().Be(0);
        reporte.Kpis.ValorTotalAnulado.Should().Be(0);
        reporte.Kpis.TotalEventos.Should().BeGreaterThanOrEqualTo(reporte.Kpis.TotalVentas);
    }

    [Fact]
    public async Task AuditoriaVentas_FiltroUsuario_SoloMuestraEventosDelUsuario()
    {
        // Arrange: crear una venta como admin
        var adminClient = _factory.CreateAuthenticatedClient("admin@sincopos.com");
        var cod = $"AV-USR-{Guid.NewGuid():N}"[..13];
        var productoId = await CrearProducto(adminClient, cod, precioVenta: 2500m, precioCosto: 1000m);
        await RegistrarEntrada(adminClient, productoId, 10);
        var cajaId = await CrearAbrirCaja(adminClient, $"Caja-USR-{cod}");
        await CrearVenta(adminClient, cajaId, productoId, cantidad: 1);

        var supervisorClient = _factory.CreateAuthenticatedClient("supervisor@sincopos.com");
        var desde = DateTime.UtcNow.AddMinutes(-5).ToString("yyyy-MM-ddTHH:mm:ssZ");
        var hasta = DateTime.UtcNow.AddMinutes(5).ToString("yyyy-MM-ddTHH:mm:ssZ");

        // Act — filtrar solo por admin@sincopos.com
        var r = await supervisorClient.GetAsync(
            $"/api/v1/Reportes/auditoria-ventas?fechaDesde={desde}&fechaHasta={hasta}" +
            "&usuarioEmail=admin%40sincopos.com");

        // Assert
        r.StatusCode.Should().Be(HttpStatusCode.OK);
        var reporte = await r.Content.ReadFromJsonAsync<ReporteAuditoriaVentasDto>(_json);
        reporte!.Logs.Items.Should().OnlyContain(log => log.UsuarioEmail == "admin@sincopos.com");
    }

    [Fact]
    public async Task AuditoriaVentas_Paginacion_RetornaTotalPagesConsistente()
    {
        var supervisorClient = _factory.CreateAuthenticatedClient("supervisor@sincopos.com");
        var desde = DateTime.UtcNow.AddYears(-1).ToString("yyyy-MM-ddTHH:mm:ssZ");
        var hasta = DateTime.UtcNow.AddMinutes(5).ToString("yyyy-MM-ddTHH:mm:ssZ");

        var r = await supervisorClient.GetAsync(
            $"/api/v1/Reportes/auditoria-ventas?fechaDesde={desde}&fechaHasta={hasta}&pageNumber=1&pageSize=5");

        r.StatusCode.Should().Be(HttpStatusCode.OK);
        var reporte = await r.Content.ReadFromJsonAsync<ReporteAuditoriaVentasDto>(_json);
        reporte.Should().NotBeNull();
        reporte!.Logs.PageNumber.Should().Be(1);
        reporte.Logs.PageSize.Should().Be(5);
        reporte.Logs.Items.Should().HaveCountLessThanOrEqualTo(5);
        if (reporte.Logs.TotalCount > 0)
            reporte.Logs.TotalPages.Should().Be((int)Math.Ceiling(reporte.Logs.TotalCount / 5.0));
    }

    // ─── Tests del historial por venta ────────────────────────────────────────

    [Fact]
    public async Task HistorialVenta_VentaExistente_RetornaTimeline()
    {
        // Arrange
        var adminClient = _factory.CreateAuthenticatedClient("admin@sincopos.com");
        var cod = $"AV-HIST-{Guid.NewGuid():N}"[..12];
        var productoId = await CrearProducto(adminClient, cod, precioVenta: 3500m, precioCosto: 1400m);
        await RegistrarEntrada(adminClient, productoId, 10);
        var cajaId = await CrearAbrirCaja(adminClient, $"Caja-H-{cod}");
        var ventaId = await CrearVenta(adminClient, cajaId, productoId, cantidad: 1);

        var supervisorClient = _factory.CreateAuthenticatedClient("supervisor@sincopos.com");

        // Act
        var r = await supervisorClient.GetAsync(
            $"/api/v1/Reportes/auditoria-ventas/venta/{ventaId}");

        // Assert
        r.StatusCode.Should().Be(HttpStatusCode.OK);
        var historial = await r.Content.ReadFromJsonAsync<HistorialEntidadDto>(_json);
        historial.Should().NotBeNull();
        historial!.EntidadId.Should().Be(ventaId.ToString());
        historial.TipoEntidad.Should().Be("Venta");
        historial.Cambios.Should().NotBeEmpty();
        historial.Cambios.Should().Contain(c => c.Accion == "CrearVenta");
        historial.TotalCambios.Should().Be(historial.Cambios.Count);
    }

    [Fact]
    public async Task HistorialVenta_SupervisorPuedeAcceder()
    {
        // Arrange
        var adminClient = _factory.CreateAuthenticatedClient("admin@sincopos.com");
        var cod = $"AV-SUP-{Guid.NewGuid():N}"[..13];
        var productoId = await CrearProducto(adminClient, cod, precioVenta: 2000m, precioCosto: 800m);
        await RegistrarEntrada(adminClient, productoId, 5);
        var cajaId = await CrearAbrirCaja(adminClient, $"Caja-SUP-{cod}");
        var ventaId = await CrearVenta(adminClient, cajaId, productoId, cantidad: 1);

        // Act — supervisor (no admin) también puede acceder
        var supervisorClient = _factory.CreateAuthenticatedClient("supervisor@sincopos.com");
        var r = await supervisorClient.GetAsync(
            $"/api/v1/Reportes/auditoria-ventas/venta/{ventaId}");

        r.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
