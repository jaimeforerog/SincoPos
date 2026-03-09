using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;

namespace POS.IntegrationTests;

/// <summary>
/// Pruebas de integración para el módulo de Multi-sucursal por usuario.
/// Verifica: asignación de sucursales, listado con sucursales, cambio de estado y control de acceso.
/// </summary>
[Collection("POS-Auth")]
public class MultiSucursalTests
{
    private readonly AuthenticatedWebApplicationFactory _factory;
    private readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };

    private const string AdminEmail = "admin@sincopos.com";
    private const string SupervisorEmail = "supervisor@sincopos.com";
    private const string CajeroEmail = "cajero@sincopos.com";

    private int UsuarioId => _factory.UsuarioTestId;
    private int SucPP => _factory.SucursalPPId;
    private int SucFIFO => _factory.SucursalFIFOId;
    private int SucLIFO => _factory.SucursalLIFOId;

    public MultiSucursalTests(AuthenticatedWebApplicationFactory factory)
    {
        _factory = factory;
    }

    // ─── Listar usuarios ─────────────────────────────────────────────────────

    [Fact]
    public async Task ListarUsuarios_ComoAdmin_Devuelve200ConLista()
    {
        var client = _factory.CreateAuthenticatedClient(AdminEmail);

        var response = await client.GetAsync("/api/v1/Usuarios");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var lista = await response.Content.ReadFromJsonAsync<List<JsonElement>>(_jsonOptions);
        lista.Should().NotBeNull();
        // El usuario seeded en el factory debe estar en la lista
        lista!.Should().Contain(u => u.GetProperty("id").GetInt32() == UsuarioId);
    }

    [Fact]
    public async Task ListarUsuarios_ComoSupervisor_Devuelve200()
    {
        var client = _factory.CreateAuthenticatedClient(SupervisorEmail);

        var response = await client.GetAsync("/api/v1/Usuarios");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ListarUsuarios_ComoCajero_Devuelve403()
    {
        var client = _factory.CreateAuthenticatedClient(CajeroEmail);

        var response = await client.GetAsync("/api/v1/Usuarios");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task ListarUsuarios_FiltrandoPorRol_DevuelveSubconjunto()
    {
        var client = _factory.CreateAuthenticatedClient(AdminEmail);

        var response = await client.GetAsync("/api/v1/Usuarios?rol=admin");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var lista = await response.Content.ReadFromJsonAsync<List<JsonElement>>(_jsonOptions);
        // Todos los resultados deben ser admins
        lista!.Should().AllSatisfy(u =>
            u.GetProperty("rol").GetString().Should().Be("admin"));
    }

    // ─── Obtener usuario por ID ──────────────────────────────────────────────

    [Fact]
    public async Task ObtenerUsuario_ComoAdmin_Devuelve200()
    {
        var client = _factory.CreateAuthenticatedClient(AdminEmail);

        var response = await client.GetAsync($"/api/v1/Usuarios/{UsuarioId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var usuario = await response.Content.ReadFromJsonAsync<JsonElement>(_jsonOptions);
        usuario.GetProperty("id").GetInt32().Should().Be(UsuarioId);
        usuario.GetProperty("email").GetString().Should().Be("admin@sincopos.com");
    }

    [Fact]
    public async Task ObtenerUsuario_ComoSupervisor_Devuelve200()
    {
        var client = _factory.CreateAuthenticatedClient(SupervisorEmail);

        var response = await client.GetAsync($"/api/v1/Usuarios/{UsuarioId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ObtenerUsuario_Inexistente_Devuelve404()
    {
        var client = _factory.CreateAuthenticatedClient(AdminEmail);

        var response = await client.GetAsync("/api/v1/Usuarios/999999");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ─── Asignar sucursales ──────────────────────────────────────────────────

    [Fact]
    public async Task AsignarSucursales_ComoAdmin_Devuelve204()
    {
        var client = _factory.CreateAuthenticatedClient(AdminEmail);
        var dto = new { sucursalIds = new[] { SucPP, SucFIFO } };

        var response = await client.PutAsJsonAsync($"/api/v1/Usuarios/{UsuarioId}/sucursales", dto);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task AsignarSucursales_ComoSupervisor_Devuelve403()
    {
        var client = _factory.CreateAuthenticatedClient(SupervisorEmail);
        var dto = new { sucursalIds = new[] { SucPP } };

        var response = await client.PutAsJsonAsync($"/api/v1/Usuarios/{UsuarioId}/sucursales", dto);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task AsignarSucursales_SucursalInexistente_SilenciosamenteFiltrada()
    {
        var client = _factory.CreateAuthenticatedClient(AdminEmail);
        // El servicio filtra sucursales inactivas/inexistentes — devuelve 204 sin error
        var dto = new { sucursalIds = new[] { 999999 } };

        var response = await client.PutAsJsonAsync($"/api/v1/Usuarios/{UsuarioId}/sucursales", dto);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Verificar que no se asignó ninguna sucursal inválida
        var getResp = await client.GetAsync($"/api/v1/Usuarios/{UsuarioId}");
        var usuario = await getResp.Content.ReadFromJsonAsync<JsonElement>(_jsonOptions);
        var sucursales = usuario.GetProperty("sucursalesAsignadas").EnumerateArray().ToList();
        sucursales.Should().NotContain(s => s.GetProperty("id").GetInt32() == 999999);
    }

    [Fact]
    public async Task AsignarSucursales_SeReflejanEnListado()
    {
        var client = _factory.CreateAuthenticatedClient(AdminEmail);

        // Asignar las tres sucursales
        var asignarDto = new { sucursalIds = new[] { SucPP, SucFIFO, SucLIFO } };
        var asignarResp = await client.PutAsJsonAsync($"/api/v1/Usuarios/{UsuarioId}/sucursales", asignarDto);
        asignarResp.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Verificar que aparecen en el detalle del usuario
        var getResp = await client.GetAsync($"/api/v1/Usuarios/{UsuarioId}");
        getResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var usuario = await getResp.Content.ReadFromJsonAsync<JsonElement>(_jsonOptions);
        var sucursales = usuario.GetProperty("sucursalesAsignadas").EnumerateArray().ToList();
        sucursales.Should().HaveCount(3);
        sucursales.Select(s => s.GetProperty("id").GetInt32())
            .Should().Contain(new[] { SucPP, SucFIFO, SucLIFO });
    }

    [Fact]
    public async Task AsignarSucursales_VacioLimpiaTodas()
    {
        var client = _factory.CreateAuthenticatedClient(AdminEmail);

        // Primero asignar algunas
        await client.PutAsJsonAsync(
            $"/api/v1/Usuarios/{UsuarioId}/sucursales",
            new { sucursalIds = new[] { SucPP } });

        // Luego limpiar con lista vacía
        var response = await client.PutAsJsonAsync(
            $"/api/v1/Usuarios/{UsuarioId}/sucursales",
            new { sucursalIds = Array.Empty<int>() });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var getResp = await client.GetAsync($"/api/v1/Usuarios/{UsuarioId}");
        var usuario = await getResp.Content.ReadFromJsonAsync<JsonElement>(_jsonOptions);
        var sucursales = usuario.GetProperty("sucursalesAsignadas").EnumerateArray().ToList();
        sucursales.Should().BeEmpty();
    }

    // ─── Estadísticas ────────────────────────────────────────────────────────

    [Fact]
    public async Task ObtenerEstadisticas_ComoAdmin_Devuelve200()
    {
        var client = _factory.CreateAuthenticatedClient(AdminEmail);

        var response = await client.GetAsync("/api/v1/Usuarios/estadisticas");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var stats = await response.Content.ReadFromJsonAsync<JsonElement>(_jsonOptions);
        stats.GetProperty("totalUsuarios").GetInt32().Should().BeGreaterThanOrEqualTo(1);
        stats.GetProperty("usuariosActivos").GetInt32().Should().BeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public async Task ObtenerEstadisticas_ComoSupervisor_Devuelve403()
    {
        var client = _factory.CreateAuthenticatedClient(SupervisorEmail);

        var response = await client.GetAsync("/api/v1/Usuarios/estadisticas");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ─── Cambiar estado ──────────────────────────────────────────────────────

    [Fact]
    public async Task CambiarEstado_ComoSupervisor_Devuelve403()
    {
        var client = _factory.CreateAuthenticatedClient(SupervisorEmail);
        var dto = new { activo = false, motivo = "Test" };

        var response = await client.PutAsJsonAsync($"/api/v1/Usuarios/{UsuarioId}/estado", dto);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task CambiarEstado_UsuarioInexistente_Devuelve404()
    {
        var client = _factory.CreateAuthenticatedClient(AdminEmail);
        var dto = new { activo = false, motivo = "Test" };

        var response = await client.PutAsJsonAsync("/api/v1/Usuarios/999999/estado", dto);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
