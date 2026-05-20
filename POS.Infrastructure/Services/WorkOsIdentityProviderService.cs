using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using POS.Application.Services;
using POS.Infrastructure.Configuration;

namespace POS.Infrastructure.Services;

/// <summary>
/// Implementación de IIdentityProviderService usando WorkOS User Management API.
/// Docs: https://workos.com/docs/reference/user-management
/// </summary>
public sealed class WorkOsIdentityProviderService : IIdentityProviderService
{
    private readonly HttpClient _httpClient;
    private readonly WorkOsOptions _options;
    private readonly ILogger<WorkOsIdentityProviderService> _logger;

    private static readonly JsonSerializerOptions _json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    public WorkOsIdentityProviderService(
        HttpClient httpClient,
        IOptions<WorkOsOptions> options,
        ILogger<WorkOsIdentityProviderService> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;

        _httpClient.BaseAddress = new Uri("https://api.workos.com/");
        _httpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", _options.ApiKey);
    }

    public async Task<(string? ExternalId, string? Error)> CrearUsuarioAsync(
        string email, string displayName, string? tempPassword)
    {
        var nameParts = displayName.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        var firstName = nameParts.Length > 0 ? nameParts[0] : email.Split('@')[0];
        var lastName = nameParts.Length > 1 ? nameParts[1] : "";

        var body = new
        {
            email,
            first_name = firstName,
            last_name = lastName,
            email_verified = false,
            password = tempPassword,
        };

        var content = new StringContent(
            JsonSerializer.Serialize(body, _json),
            Encoding.UTF8,
            "application/json");

        try
        {
            var response = await _httpClient.PostAsync("user_management/users", content);
            var responseBody = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("[WorkOS] Error al crear usuario {Email}: {Status} {Body}",
                    email, response.StatusCode, responseBody);

                if (response.StatusCode == System.Net.HttpStatusCode.Conflict)
                    return (null, "El usuario ya existe en WorkOS");

                return (null, $"Error WorkOS {(int)response.StatusCode}");
            }

            var doc = JsonDocument.Parse(responseBody);
            var id = doc.RootElement.GetProperty("id").GetString();
            _logger.LogInformation("[WorkOS] Usuario creado: {Email} → {Id}", email, id);
            return (id, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[WorkOS] Excepción al crear usuario {Email}", email);
            return (null, ex.Message);
        }
    }

    public async Task<(bool Success, string? Error)> AsignarRolAsync(string externalId, string rol)
    {
        // WorkOS no tiene roles de usuario a nivel de plataforma sin Organizations.
        // Los roles se gestionan en la BD local (columna 'rol' en la tabla usuarios).
        // Este método es un no-op: el rol ya fue guardado por UsuarioService en la BD.
        _logger.LogDebug("[WorkOS] AsignarRol ignorado (roles manejados en BD): {Id} → {Rol}",
            externalId, rol);
        return await Task.FromResult((true, (string?)null));
    }

    public async Task<(bool Success, string? Error)> DesactivarUsuarioAsync(string externalId)
    {
        try
        {
            var response = await _httpClient.DeleteAsync($"user_management/users/{externalId}");
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync();
                _logger.LogError("[WorkOS] Error al desactivar usuario {Id}: {Body}", externalId, body);
                return (false, $"Error WorkOS {(int)response.StatusCode}");
            }
            _logger.LogInformation("[WorkOS] Usuario desactivado/eliminado: {Id}", externalId);
            return (true, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[WorkOS] Excepción al desactivar usuario {Id}", externalId);
            return (false, ex.Message);
        }
    }

    public async Task<(bool Success, string? Error)> ActivarUsuarioAsync(string externalId)
    {
        // WorkOS no tiene un endpoint directo de re-activación sin Organizations.
        // Si el usuario fue eliminado, debe ser recreado. Registramos el intento.
        _logger.LogWarning("[WorkOS] ActivarUsuario no está soportado directamente. " +
            "El usuario {Id} debe ser recreado si fue eliminado.", externalId);
        return await Task.FromResult((false, "Activación no soportada directamente en WorkOS. Crea un nuevo usuario."));
    }

    public async Task<(string? TempPassword, string? Error)> ResetPasswordAsync(string externalId)
    {
        // Flujo: GET user para email → PUT password nuevo → POST password_reset/send.
        // Settear un password fresco asegura que usuarios creados antes con password:null
        // tengan credenciales válidas aunque el email de reset no llegue.
        try
        {
            var userResponse = await _httpClient.GetAsync($"user_management/users/{externalId}");
            if (!userResponse.IsSuccessStatusCode)
            {
                var errBody = await userResponse.Content.ReadAsStringAsync();
                _logger.LogError(
                    "[WorkOS] No se pudo obtener usuario {Id}: {Status} {Body}",
                    externalId, userResponse.StatusCode, errBody);
                return (null, "Usuario no encontrado en WorkOS");
            }

            var userBody = await userResponse.Content.ReadAsStringAsync();
            var userDoc = JsonDocument.Parse(userBody);
            var email = userDoc.RootElement.GetProperty("email").GetString();

            if (string.IsNullOrEmpty(email))
                return (null, "No se pudo obtener el email del usuario");

            var nuevoPassword = PasswordGenerator.Generate();

            var putBody = JsonSerializer.Serialize(new { password = nuevoPassword }, _json);
            var putContent = new StringContent(putBody, Encoding.UTF8, "application/json");
            var putRequest = new HttpRequestMessage(HttpMethod.Put, $"user_management/users/{externalId}")
            {
                Content = putContent,
            };
            var putResponse = await _httpClient.SendAsync(putRequest);
            if (!putResponse.IsSuccessStatusCode)
            {
                var putRespBody = await putResponse.Content.ReadAsStringAsync();
                _logger.LogError(
                    "[WorkOS] Error al actualizar password de {Email} (Id={Id}): {Status} {Body}",
                    email, externalId, putResponse.StatusCode, putRespBody);
                return (null, $"Error WorkOS al actualizar password ({(int)putResponse.StatusCode})");
            }

            var sendBody = JsonSerializer.Serialize(new { email }, _json);
            var sendContent = new StringContent(sendBody, Encoding.UTF8, "application/json");
            var sendResponse = await _httpClient.PostAsync("user_management/password_reset/send", sendContent);
            var sendRespBody = await sendResponse.Content.ReadAsStringAsync();
            if (!sendResponse.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "[WorkOS] Password reseteado para {Email} pero fallo el envio de email: {Status} {Body}. " +
                    "El admin debera compartir el password manualmente.",
                    email, sendResponse.StatusCode, sendRespBody);
                return (nuevoPassword, null);
            }

            _logger.LogInformation(
                "[WorkOS] Password reseteado y email solicitado para {Email}. SendStatus={Status}",
                email, sendResponse.StatusCode);
            return (nuevoPassword, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[WorkOS] Excepción al resetear contraseña para {Id}", externalId);
            return (null, ex.Message);
        }
    }

    // ── Organizations ───────────────────────────────────────────────────────

    public async Task<(string? OrganizationId, string? Error)> CrearOrganizacionAsync(string nombre)
    {
        try
        {
            var body = JsonSerializer.Serialize(new { name = nombre }, _json);
            var content = new StringContent(body, Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync("organizations", content);
            var responseBody = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError(
                    "[WorkOS] Error al crear organization '{Nombre}': {Status} {Body}",
                    nombre, response.StatusCode, responseBody);
                return (null, $"Error WorkOS {(int)response.StatusCode}");
            }

            var doc = JsonDocument.Parse(responseBody);
            var id = doc.RootElement.GetProperty("id").GetString();
            _logger.LogInformation("[WorkOS] Organization creada: {Nombre} → {Id}", nombre, id);
            return (id, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[WorkOS] Excepción al crear organization '{Nombre}'", nombre);
            return (null, ex.Message);
        }
    }

    public async Task<(bool Success, string? Error)> ActualizarOrganizacionAsync(string organizationId, string nombre)
    {
        try
        {
            var body = JsonSerializer.Serialize(new { name = nombre }, _json);
            var content = new StringContent(body, Encoding.UTF8, "application/json");
            var request = new HttpRequestMessage(HttpMethod.Put, $"organizations/{organizationId}")
            {
                Content = content,
            };
            var response = await _httpClient.SendAsync(request);

            if (!response.IsSuccessStatusCode)
            {
                var respBody = await response.Content.ReadAsStringAsync();
                _logger.LogError(
                    "[WorkOS] Error al actualizar organization {Id}: {Status} {Body}",
                    organizationId, response.StatusCode, respBody);
                return (false, $"Error WorkOS {(int)response.StatusCode}");
            }
            return (true, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[WorkOS] Excepción al actualizar organization {Id}", organizationId);
            return (false, ex.Message);
        }
    }

    public async Task<(string? MembershipId, string? Error)> CrearMembresiaAsync(string externalUserId, string organizationId)
    {
        try
        {
            var body = JsonSerializer.Serialize(new
            {
                user_id = externalUserId,
                organization_id = organizationId,
            }, _json);
            var content = new StringContent(body, Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync("user_management/organization_memberships", content);
            var responseBody = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                var doc = JsonDocument.Parse(responseBody);
                var id = doc.RootElement.GetProperty("id").GetString();
                _logger.LogInformation(
                    "[WorkOS] Membership creada: user={UserId} org={OrgId} → {Id}",
                    externalUserId, organizationId, id);
                return (id, null);
            }

            // 422/409: WorkOS rechaza memberships duplicadas; lo tratamos como éxito idempotente
            if (response.StatusCode == System.Net.HttpStatusCode.UnprocessableEntity ||
                response.StatusCode == System.Net.HttpStatusCode.Conflict)
            {
                if (responseBody.Contains("already", StringComparison.OrdinalIgnoreCase) ||
                    responseBody.Contains("exists", StringComparison.OrdinalIgnoreCase) ||
                    responseBody.Contains("duplicate", StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogDebug(
                        "[WorkOS] Membership ya existe para user={UserId} org={OrgId}",
                        externalUserId, organizationId);
                    return (null, null);
                }
            }

            _logger.LogError(
                "[WorkOS] Error al crear membership user={UserId} org={OrgId}: {Status} {Body}",
                externalUserId, organizationId, response.StatusCode, responseBody);
            return (null, $"Error WorkOS {(int)response.StatusCode}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[WorkOS] Excepción al crear membership user={UserId} org={OrgId}",
                externalUserId, organizationId);
            return (null, ex.Message);
        }
    }

    public async Task<(bool Success, string? Error)> EliminarMembresiaAsync(string externalUserId, string organizationId)
    {
        try
        {
            // Hay que listar memberships para obtener el membership_id, no se puede borrar por user_id+org_id directo.
            var (ids, listError) = await ListarMembershipIdsAsync(externalUserId);
            if (listError != null)
                return (false, listError);

            foreach (var (membershipId, orgId) in ids)
            {
                if (orgId != organizationId) continue;
                var response = await _httpClient.DeleteAsync($"user_management/organization_memberships/{membershipId}");
                if (!response.IsSuccessStatusCode)
                {
                    var body = await response.Content.ReadAsStringAsync();
                    _logger.LogError(
                        "[WorkOS] Error al eliminar membership {Id}: {Status} {Body}",
                        membershipId, response.StatusCode, body);
                    return (false, $"Error WorkOS {(int)response.StatusCode}");
                }
                _logger.LogInformation(
                    "[WorkOS] Membership eliminada: user={UserId} org={OrgId}", externalUserId, organizationId);
                return (true, null);
            }
            // No existía: tratamos como éxito idempotente
            return (true, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[WorkOS] Excepción al eliminar membership user={UserId} org={OrgId}",
                externalUserId, organizationId);
            return (false, ex.Message);
        }
    }

    public async Task<(IReadOnlyList<string> OrganizationIds, string? Error)> ListarMembresiasAsync(string externalUserId)
    {
        var (ids, error) = await ListarMembershipIdsAsync(externalUserId);
        if (error != null)
            return (Array.Empty<string>(), error);
        return (ids.Select(p => p.OrgId).ToList(), null);
    }

    private async Task<(List<(string MembershipId, string OrgId)> Items, string? Error)> ListarMembershipIdsAsync(string externalUserId)
    {
        try
        {
            var url = $"user_management/organization_memberships?user_id={Uri.EscapeDataString(externalUserId)}&limit=100";
            var response = await _httpClient.GetAsync(url);
            var body = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError(
                    "[WorkOS] Error al listar memberships de {UserId}: {Status} {Body}",
                    externalUserId, response.StatusCode, body);
                return (new(), $"Error WorkOS {(int)response.StatusCode}");
            }

            var doc = JsonDocument.Parse(body);
            var items = new List<(string, string)>();
            if (doc.RootElement.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Array)
            {
                foreach (var m in data.EnumerateArray())
                {
                    var membershipId = m.GetProperty("id").GetString();
                    var orgId = m.GetProperty("organization_id").GetString();
                    if (membershipId != null && orgId != null)
                        items.Add((membershipId, orgId));
                }
            }
            return (items, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[WorkOS] Excepción al listar memberships de {UserId}", externalUserId);
            return (new(), ex.Message);
        }
    }
}
