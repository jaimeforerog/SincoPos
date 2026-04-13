using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;

namespace POS.IntegrationTests;

/// <summary>
/// Pruebas de integración para el CRUD de usuarios.
/// Verifica: creación, actualización, cambio de rol, reset de contraseña,
/// jerarquía de roles y control de acceso por política.
/// </summary>
[Collection("POS-Auth")]
public class UserCrudTests
{
    private readonly AuthenticatedWebApplicationFactory _factory;
    private readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };

    private const string AdminEmail = "admin@sincopos.com";
    private const string SupervisorEmail = "supervisor@sincopos.com";
    private const string CajeroEmail = "cajero@sincopos.com";

    public UserCrudTests(AuthenticatedWebApplicationFactory factory)
    {
        _factory = factory;
    }

    // ─── Helpers ──────────────────────────────────────────────────────────────

    private async Task<(HttpResponseMessage Response, JsonElement? Body)> CrearUsuarioAsync(
        HttpClient client, string email, string nombre, string rol, int? sucursalDefaultId = null)
    {
        var dto = new
        {
            email,
            nombreCompleto = nombre,
            rol,
            sucursalDefaultId
        };

        var response = await client.PostAsJsonAsync("/api/v1/Usuarios", dto);
        JsonElement? body = null;
        if (response.IsSuccessStatusCode)
        {
            body = await response.Content.ReadFromJsonAsync<JsonElement>(_jsonOptions);
        }
        return (response, body);
    }

    // ─── 1. Admin puede crear un usuario ─────────────────────────────────────

    [Fact]
    public async Task Admin_CrearUsuario_Devuelve201()
    {
        var client = _factory.CreateAuthenticatedClient(AdminEmail);
        var uniqueEmail = $"nuevo-{Guid.NewGuid():N}@test.com";

        var (response, body) = await CrearUsuarioAsync(
            client, uniqueEmail, "Nuevo Usuario Test", "cajero", _factory.SucursalPPId);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        body.Should().NotBeNull();
        body!.Value.GetProperty("id").GetInt32().Should().BeGreaterThan(0);
        body!.Value.GetProperty("email").GetString().Should().Be(uniqueEmail);
        body!.Value.GetProperty("rol").GetString().Should().Be("cajero");
        body!.Value.GetProperty("nombreCompleto").GetString().Should().Be("Nuevo Usuario Test");
    }

    // ─── 2. Email duplicado devuelve 400 ─────────────────────────────────────

    [Fact]
    public async Task Admin_CrearUsuario_EmailDuplicado_Devuelve400()
    {
        var client = _factory.CreateAuthenticatedClient(AdminEmail);
        var uniqueEmail = $"dup-{Guid.NewGuid():N}@test.com";

        // Crear el primer usuario
        var (first, _) = await CrearUsuarioAsync(client, uniqueEmail, "Primero", "vendedor");
        first.StatusCode.Should().Be(HttpStatusCode.Created);

        // Intentar crear con el mismo email
        var (second, _) = await CrearUsuarioAsync(client, uniqueEmail, "Segundo", "vendedor");
        second.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ─── 3. Rol inválido devuelve 400 ────────────────────────────────────────

    [Fact]
    public async Task Admin_CrearUsuario_RolInvalido_Devuelve400()
    {
        var client = _factory.CreateAuthenticatedClient(AdminEmail);
        var uniqueEmail = $"invalid-rol-{Guid.NewGuid():N}@test.com";

        var dto = new
        {
            email = uniqueEmail,
            nombreCompleto = "Rol Invalido",
            rol = "superadmin" // rol que no existe
        };

        var response = await client.PostAsJsonAsync("/api/v1/Usuarios", dto);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ─── 4. Admin puede actualizar un usuario ────────────────────────────────

    [Fact]
    public async Task Admin_ActualizarUsuario_Devuelve204()
    {
        var client = _factory.CreateAuthenticatedClient(AdminEmail);
        var uniqueEmail = $"upd-{Guid.NewGuid():N}@test.com";

        // Crear usuario
        var (createResp, createBody) = await CrearUsuarioAsync(client, uniqueEmail, "Original", "vendedor");
        createResp.StatusCode.Should().Be(HttpStatusCode.Created);
        var userId = createBody!.Value.GetProperty("id").GetInt32();

        // Actualizar
        var updateDto = new
        {
            nombreCompleto = "Actualizado",
            telefono = "3001234567"
        };

        var response = await client.PutAsJsonAsync($"/api/v1/Usuarios/{userId}", updateDto);
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Verificar
        var getResp = await client.GetAsync($"/api/v1/Usuarios/{userId}");
        getResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var usuario = await getResp.Content.ReadFromJsonAsync<JsonElement>(_jsonOptions);
        usuario.GetProperty("nombreCompleto").GetString().Should().Be("Actualizado");
        usuario.GetProperty("telefono").GetString().Should().Be("3001234567");
    }

    // ─── 5. Admin puede cambiar rol ──────────────────────────────────────────

    [Fact]
    public async Task Admin_CambiarRol_Devuelve204()
    {
        var client = _factory.CreateAuthenticatedClient(AdminEmail);
        var uniqueEmail = $"rol-{Guid.NewGuid():N}@test.com";

        // Crear usuario como vendedor
        var (createResp, createBody) = await CrearUsuarioAsync(client, uniqueEmail, "Cambio Rol", "vendedor");
        createResp.StatusCode.Should().Be(HttpStatusCode.Created);
        var userId = createBody!.Value.GetProperty("id").GetInt32();

        // Cambiar a supervisor
        var rolDto = new { rol = "supervisor" };
        var response = await client.PutAsJsonAsync($"/api/v1/Usuarios/{userId}/rol", rolDto);
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Verificar
        var getResp = await client.GetAsync($"/api/v1/Usuarios/{userId}");
        var usuario = await getResp.Content.ReadFromJsonAsync<JsonElement>(_jsonOptions);
        usuario.GetProperty("rol").GetString().Should().Be("supervisor");
    }

    // ─── 6. Admin no puede cambiar su propio rol ─────────────────────────────

    [Fact]
    public async Task Admin_CambiarRolPropio_Devuelve400()
    {
        var client = _factory.CreateAuthenticatedClient(AdminEmail);

        // El admin seeded tiene UsuarioTestId y ExternalId = "00000000-0000-0000-0000-000000000001"
        var userId = _factory.UsuarioTestId;
        var rolDto = new { rol = "supervisor" };

        var response = await client.PutAsJsonAsync($"/api/v1/Usuarios/{userId}/rol", rolDto);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ─── 7. Admin puede resetear contraseña ──────────────────────────────────

    [Fact]
    public async Task Admin_ResetPassword_Devuelve200()
    {
        var client = _factory.CreateAuthenticatedClient(AdminEmail);
        var uniqueEmail = $"reset-{Guid.NewGuid():N}@test.com";

        // Crear usuario
        var (createResp, createBody) = await CrearUsuarioAsync(client, uniqueEmail, "Reset Test", "vendedor");
        createResp.StatusCode.Should().Be(HttpStatusCode.Created);
        var userId = createBody!.Value.GetProperty("id").GetInt32();

        // Reset password
        var response = await client.PostAsync($"/api/v1/Usuarios/{userId}/reset-password", null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(_jsonOptions);
        body.GetProperty("passwordTemporal").GetString().Should().NotBeNullOrEmpty();
    }

    // ─── 8. Supervisor puede crear un cajero ─────────────────────────────────

    [Fact]
    public async Task Supervisor_CrearCajero_Devuelve201()
    {
        var client = _factory.CreateAuthenticatedClient(SupervisorEmail);
        var uniqueEmail = $"sup-cajero-{Guid.NewGuid():N}@test.com";

        var (response, body) = await CrearUsuarioAsync(client, uniqueEmail, "Cajero de Supervisor", "cajero");

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        body.Should().NotBeNull();
        body!.Value.GetProperty("rol").GetString().Should().Be("cajero");
    }

    // ─── 9. Supervisor NO puede crear un admin ───────────────────────────────

    [Fact]
    public async Task Supervisor_CrearAdmin_Devuelve400()
    {
        var client = _factory.CreateAuthenticatedClient(SupervisorEmail);
        var uniqueEmail = $"sup-admin-{Guid.NewGuid():N}@test.com";

        var (response, _) = await CrearUsuarioAsync(client, uniqueEmail, "Admin Fallido", "admin");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ─── 10. Cajero NO puede crear usuarios ──────────────────────────────────

    [Fact]
    public async Task Cajero_CrearUsuario_Devuelve403()
    {
        var client = _factory.CreateAuthenticatedClient(CajeroEmail);
        var uniqueEmail = $"cajero-intento-{Guid.NewGuid():N}@test.com";

        var dto = new
        {
            email = uniqueEmail,
            nombreCompleto = "No Debería Crearse",
            rol = "vendedor"
        };

        var response = await client.PostAsJsonAsync("/api/v1/Usuarios", dto);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
