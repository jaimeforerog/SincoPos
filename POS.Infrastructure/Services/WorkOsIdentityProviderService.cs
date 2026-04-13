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
public class WorkOsIdentityProviderService : IIdentityProviderService
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
        // WorkOS envía un email de restablecimiento de contraseña al usuario
        // Primero necesitamos el email del usuario
        try
        {
            var userResponse = await _httpClient.GetAsync($"user_management/users/{externalId}");
            if (!userResponse.IsSuccessStatusCode)
                return (null, "Usuario no encontrado en WorkOS");

            var userBody = await userResponse.Content.ReadAsStringAsync();
            var userDoc = JsonDocument.Parse(userBody);
            var email = userDoc.RootElement.GetProperty("email").GetString();

            if (string.IsNullOrEmpty(email))
                return (null, "No se pudo obtener el email del usuario");

            var body = JsonSerializer.Serialize(new { email }, _json);
            var content = new StringContent(body, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync("user_management/password_reset/send", content);
            if (!response.IsSuccessStatusCode)
            {
                var responseBody = await response.Content.ReadAsStringAsync();
                _logger.LogError("[WorkOS] Error al enviar reset de contraseña: {Body}", responseBody);
                return (null, $"Error WorkOS {(int)response.StatusCode}");
            }

            _logger.LogInformation("[WorkOS] Email de reset de contraseña enviado a {Email}", email);
            return ("EMAIL_ENVIADO", null); // WorkOS envía email, no retorna contraseña
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[WorkOS] Excepción al resetear contraseña para {Id}", externalId);
            return (null, ex.Message);
        }
    }
}
