using System.Net;
using System.Net.Http.Json;
using FluentAssertions;

namespace POS.IntegrationTests;

/// <summary>
/// Pruebas de seguridad y control de acceso por rol.
/// Verifica: 401 sin autenticar, 403 por rol insuficiente, 2xx con rol correcto.
/// Usa AuthenticatedWebApplicationFactory (modo estricto: sin X-Test-User → 401).
/// </summary>
[Collection("POS-Auth")]
public class SeguridadRolesTests
{
    private readonly AuthenticatedWebApplicationFactory _factory;

    private const string AdminEmail = "admin@sincopos.com";
    private const string SupervisorEmail = "supervisor@sincopos.com";
    private const string CajeroEmail = "cajero@sincopos.com";
    private const string VendedorEmail = "vendedor@sincopos.com";

    public SeguridadRolesTests(AuthenticatedWebApplicationFactory factory)
    {
        _factory = factory;
    }

    // ─── Sin autenticar (401) ────────────────────────────────────────────────

    [Fact]
    public async Task SinAutenticar_EndpointProtegido_Devuelve401()
    {
        var client = _factory.CreateClient(); // sin X-Test-User header

        var response = await client.GetAsync("/api/v1/Terceros");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task SinAutenticar_EndpointPublico_Devuelve200()
    {
        var client = _factory.CreateClient(); // sin X-Test-User header

        // calcular-dv es [AllowAnonymous]
        var response = await client.GetAsync("/api/v1/Terceros/calcular-dv?nit=900455955");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task SinAutenticar_EndpointImpuestos_Devuelve401()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/v1/Impuestos");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ─── Cajero — acceso denegado en endpoints superiores ───────────────────

    [Fact]
    public async Task Cajero_ListarUsuarios_RequiereSupervisor_Devuelve403()
    {
        var client = _factory.CreateAuthenticatedClient(CajeroEmail);

        var response = await client.GetAsync("/api/v1/Usuarios");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Cajero_EstadisticasUsuarios_RequiereAdmin_Devuelve403()
    {
        var client = _factory.CreateAuthenticatedClient(CajeroEmail);

        var response = await client.GetAsync("/api/v1/Usuarios/estadisticas");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Cajero_CrearImpuesto_RequiereAdmin_Devuelve403()
    {
        var client = _factory.CreateAuthenticatedClient(CajeroEmail);
        var dto = new { nombre = "IVA Test", porcentaje = 0.19m, tipo = "IVA" };

        var response = await client.PostAsJsonAsync("/api/v1/Impuestos", dto);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Cajero_DesactivarImpuesto_RequiereAdmin_Devuelve403()
    {
        var client = _factory.CreateAuthenticatedClient(CajeroEmail);

        var response = await client.DeleteAsync("/api/v1/Impuestos/1");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ─── Supervisor — acceso denegado en endpoints Admin ────────────────────

    [Fact]
    public async Task Supervisor_EstadisticasUsuarios_RequiereAdmin_Devuelve403()
    {
        var client = _factory.CreateAuthenticatedClient(SupervisorEmail);

        var response = await client.GetAsync("/api/v1/Usuarios/estadisticas");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Supervisor_CrearImpuesto_RequiereAdmin_Devuelve403()
    {
        var client = _factory.CreateAuthenticatedClient(SupervisorEmail);
        var dto = new { nombre = "IVA Test", porcentaje = 0.19m, tipo = "IVA" };

        var response = await client.PostAsJsonAsync("/api/v1/Impuestos", dto);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Supervisor_AsignarSucursalesUsuario_RequiereAdmin_Devuelve403()
    {
        var client = _factory.CreateAuthenticatedClient(SupervisorEmail);
        var dto = new { sucursalIds = new[] { _factory.SucursalPPId } };

        var response = await client.PutAsJsonAsync($"/api/v1/Usuarios/{_factory.UsuarioTestId}/sucursales", dto);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ─── Acceso permitido por rol ────────────────────────────────────────────

    [Fact]
    public async Task Cajero_ConsultarImpuestos_Devuelve200()
    {
        var client = _factory.CreateAuthenticatedClient(CajeroEmail);

        var response = await client.GetAsync("/api/v1/Impuestos");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Cajero_ConsultarTerceros_Devuelve200()
    {
        var client = _factory.CreateAuthenticatedClient(CajeroEmail);

        var response = await client.GetAsync("/api/v1/Terceros");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Supervisor_ListarUsuarios_Devuelve200()
    {
        var client = _factory.CreateAuthenticatedClient(SupervisorEmail);

        var response = await client.GetAsync("/api/v1/Usuarios");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Admin_EstadisticasUsuarios_Devuelve200()
    {
        var client = _factory.CreateAuthenticatedClient(AdminEmail);

        var response = await client.GetAsync("/api/v1/Usuarios/estadisticas");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Admin_ListarUsuarios_Devuelve200()
    {
        var client = _factory.CreateAuthenticatedClient(AdminEmail);

        var response = await client.GetAsync("/api/v1/Usuarios");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Vendedor_ConsultarProductos_Devuelve200()
    {
        var client = _factory.CreateAuthenticatedClient(VendedorEmail);

        var response = await client.GetAsync("/api/v1/Productos");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Vendedor_CrearProducto_RequiereSupervisor_Devuelve403()
    {
        var client = _factory.CreateAuthenticatedClient(VendedorEmail);
        var dto = new
        {
            codigoBarras = "VEN-TEST-001",
            nombre = "Producto Vendedor",
            categoriaId = _factory.CategoriaTestId,
            precioVenta = 100m,
            precioCosto = 60m,
        };

        var response = await client.PostAsJsonAsync("/api/v1/Productos", dto);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
