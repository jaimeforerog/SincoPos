using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using POS.Application.Services;
using POS.Infrastructure.Configuration;

namespace POS.Infrastructure.Services;

/// <summary>
/// Implementacion de IIdentityProviderService usando el Admin REST API de Keycloak.
/// Crea, activa/desactiva y resetea contraseñas de usuarios directamente en Keycloak.
/// </summary>
public class KeycloakIdentityProviderService : IIdentityProviderService
{
    private readonly HttpClient _httpClient;
    private readonly KeycloakAdminOptions _options;
    private readonly ILogger<KeycloakIdentityProviderService> _logger;

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public KeycloakIdentityProviderService(
        HttpClient httpClient,
        IOptions<KeycloakAdminOptions> options,
        ILogger<KeycloakIdentityProviderService> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private async Task<string?> GetAdminTokenAsync()
    {
        using var tokenClient = new HttpClient();
        var tokenUrl = $"{_options.BaseUrl}/realms/master/protocol/openid-connect/token";

        var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "password",
            ["client_id"] = "admin-cli",
            ["username"] = _options.AdminUsername,
            ["password"] = _options.AdminPassword,
        });

        var response = await tokenClient.PostAsync(tokenUrl, form);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync();
            _logger.LogError("[Keycloak] No se pudo obtener token admin: {Status} {Body}",
                response.StatusCode, body);
            return null;
        }

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return doc.RootElement.GetProperty("access_token").GetString();
    }

    private HttpRequestMessage BuildRequest(HttpMethod method, string url, string token, string? jsonBody = null)
    {
        var request = new HttpRequestMessage(method, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (jsonBody != null)
            request.Content = new StringContent(jsonBody, Encoding.UTF8, "application/json");
        return request;
    }

    private async Task<string?> GetUserIdByEmailAsync(string token, string email)
    {
        var url = $"{_options.BaseUrl}/admin/realms/{_options.Realm}/users" +
                  $"?email={Uri.EscapeDataString(email)}&exact=true";

        var request = BuildRequest(HttpMethod.Get, url, token);
        var response = await _httpClient.SendAsync(request);
        if (!response.IsSuccessStatusCode) return null;

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        if (doc.RootElement.GetArrayLength() == 0) return null;

        return doc.RootElement[0].GetProperty("id").GetString();
    }

    private static string GenerateRandomPassword(int length)
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789!@#$%&*";
        var bytes = RandomNumberGenerator.GetBytes(length);
        var result = new char[length];
        for (var i = 0; i < length; i++)
            result[i] = chars[bytes[i] % chars.Length];
        return new string(result);
    }

    // ── IIdentityProviderService ──────────────────────────────────────────────

    public async Task<(string? ExternalId, string? Error)> CrearUsuarioAsync(
        string email, string displayName, string? tempPassword)
    {
        var token = await GetAdminTokenAsync();
        if (token == null)
            return (null, "No se pudo obtener token de administración de Keycloak");

        var parts = displayName.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        var firstName = parts.Length > 0 ? parts[0] : displayName;
        var lastName = parts.Length > 1 ? parts[1] : string.Empty;

        var payload = JsonSerializer.Serialize(new
        {
            email,
            username = email,
            firstName,
            lastName,
            enabled = true,
            emailVerified = true,
        }, _jsonOptions);

        var usersUrl = $"{_options.BaseUrl}/admin/realms/{_options.Realm}/users";
        var request = BuildRequest(HttpMethod.Post, usersUrl, token, payload);
        var response = await _httpClient.SendAsync(request);

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync();
            _logger.LogError("[Keycloak] Error creando usuario {Email}: {Status} {Body}",
                email, response.StatusCode, errorBody);
            return (null, $"Error Keycloak ({(int)response.StatusCode}): {errorBody}");
        }

        // Keycloak responde 201 con Location: .../users/{id}
        var location = response.Headers.Location?.ToString();
        string? externalId = location?.Split('/').Last();

        // Fallback: buscar por email si no hay Location header
        if (string.IsNullOrEmpty(externalId))
        {
            externalId = await GetUserIdByEmailAsync(token, email);
            if (externalId == null)
                return (null, "No se pudo obtener el ID del usuario creado en Keycloak");
        }

        _logger.LogInformation("[Keycloak] Usuario creado: Email={Email}, ExternalId={ExternalId}",
            email, externalId);

        return (externalId, null);
    }

    public async Task<(bool Success, string? Error)> AsignarRolAsync(string externalId, string rol)
    {
        var token = await GetAdminTokenAsync();
        if (token == null)
            return (false, "No se pudo obtener token de administración de Keycloak");

        // Obtener el rol del realm por nombre
        var roleUrl = $"{_options.BaseUrl}/admin/realms/{_options.Realm}/roles/{Uri.EscapeDataString(rol)}";
        var roleRequest = BuildRequest(HttpMethod.Get, roleUrl, token);
        var roleResponse = await _httpClient.SendAsync(roleRequest);

        if (!roleResponse.IsSuccessStatusCode)
        {
            _logger.LogWarning("[Keycloak] Rol '{Rol}' no encontrado en el realm: {Status}", rol, roleResponse.StatusCode);
            return (false, $"Rol '{rol}' no encontrado en Keycloak");
        }

        using var roleDoc = JsonDocument.Parse(await roleResponse.Content.ReadAsStringAsync());
        var roleId = roleDoc.RootElement.GetProperty("id").GetString();
        var roleName = roleDoc.RootElement.GetProperty("name").GetString();

        // Asignar rol de realm al usuario
        var assignUrl = $"{_options.BaseUrl}/admin/realms/{_options.Realm}/users/{externalId}/role-mappings/realm";
        var rolesPayload = JsonSerializer.Serialize(new[] { new { id = roleId, name = roleName } }, _jsonOptions);
        var assignRequest = BuildRequest(HttpMethod.Post, assignUrl, token, rolesPayload);
        var assignResponse = await _httpClient.SendAsync(assignRequest);

        if (!assignResponse.IsSuccessStatusCode)
        {
            var body = await assignResponse.Content.ReadAsStringAsync();
            _logger.LogWarning("[Keycloak] Error asignando rol {Rol} a {ExternalId}: {Body}", rol, externalId, body);
            return (false, $"Error asignando rol: {body}");
        }

        _logger.LogInformation("[Keycloak] Rol '{Rol}' asignado a {ExternalId}", rol, externalId);
        return (true, null);
    }

    public Task<(bool Success, string? Error)> DesactivarUsuarioAsync(string externalId)
        => SetUserEnabledAsync(externalId, false);

    public Task<(bool Success, string? Error)> ActivarUsuarioAsync(string externalId)
        => SetUserEnabledAsync(externalId, true);

    public async Task<(string? TempPassword, string? Error)> ResetPasswordAsync(string externalId)
    {
        var token = await GetAdminTokenAsync();
        if (token == null)
            return (null, "No se pudo obtener token de administración de Keycloak");

        var password = GenerateRandomPassword(12);
        var resetUrl = $"{_options.BaseUrl}/admin/realms/{_options.Realm}/users/{externalId}/reset-password";

        var payload = JsonSerializer.Serialize(new
        {
            type = "password",
            value = password,
            temporary = false,
        }, _jsonOptions);

        var request = BuildRequest(HttpMethod.Put, resetUrl, token, payload);
        var response = await _httpClient.SendAsync(request);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync();
            _logger.LogError("[Keycloak] Error reseteando password para {ExternalId}: {Body}", externalId, body);
            return (null, $"Error reseteando contraseña: {body}");
        }

        _logger.LogInformation("[Keycloak] Password reseteado para {ExternalId}", externalId);
        return (password, null);
    }

    private async Task<(bool Success, string? Error)> SetUserEnabledAsync(string externalId, bool enabled)
    {
        var token = await GetAdminTokenAsync();
        if (token == null)
            return (false, "No se pudo obtener token de administración de Keycloak");

        var url = $"{_options.BaseUrl}/admin/realms/{_options.Realm}/users/{externalId}";
        var payload = JsonSerializer.Serialize(new { enabled }, _jsonOptions);
        var request = BuildRequest(HttpMethod.Put, url, token, payload);
        var response = await _httpClient.SendAsync(request);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync();
            return (false, body);
        }

        _logger.LogInformation("[Keycloak] Usuario {ExternalId} {Estado}",
            externalId, enabled ? "activado" : "desactivado");
        return (true, null);
    }
}
