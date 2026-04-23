using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using POS.Infrastructure.Configuration;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace POS.Api.Controllers;

/// <summary>
/// Endpoints de autenticación WorkOS (intercambio de código PKCE).
/// No requieren JWT — son parte del flujo de login.
/// </summary>
[AllowAnonymous]
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/auth")]
public sealed class AuthController : ControllerBase
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly WorkOsOptions _workos;
    private readonly ILogger<AuthController> _logger;
    private readonly POS.Application.Services.IUsuarioService _usuarioService;

    public AuthController(
        IHttpClientFactory httpClientFactory,
        IOptions<WorkOsOptions> workosOptions,
        ILogger<AuthController> logger,
        POS.Application.Services.IUsuarioService usuarioService)
    {
        _httpClientFactory = httpClientFactory;
        _workos = workosOptions.Value;
        _logger = logger;
        _usuarioService = usuarioService;
    }

    /// <summary>
    /// Intercambia el authorization_code de WorkOS por un access_token.
    /// El intercambio se hace servidor-a-servidor (sin restricciones CORS).
    /// </summary>
    [HttpPost("callback")]
    public async Task<IActionResult> Callback([FromBody] CallbackRequest request)
    {
        if (string.IsNullOrEmpty(request.Code))
            return BadRequest(new { message = "code es requerido" });

        if (string.IsNullOrEmpty(request.CodeVerifier))
            return BadRequest(new { message = "code_verifier es requerido" });

        var client = _httpClientFactory.CreateClient("workos");

        // WorkOS authenticate endpoint expects JSON (not form-encoded).
        // PKCE flow: send code_verifier instead of client_secret.
        var body = new
        {
            client_id    = _workos.ClientId,
            client_secret = _workos.ApiKey,
            code         = request.Code,
            code_verifier = request.CodeVerifier,
            grant_type   = "authorization_code",
        };

        var response = await client.PostAsJsonAsync(
            "https://api.workos.com/user_management/authenticate",
            body);

        var responseBody = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("WorkOS authenticate failed {Status}: {Body}", response.StatusCode, responseBody);
            return StatusCode((int)response.StatusCode, JsonSerializer.Deserialize<object>(responseBody));
        }

        using var document = JsonDocument.Parse(responseBody);
        var root = document.RootElement;

        // The token might be in "access_token" or nested in "session.token" format depending on WorkOS API version
        string accessToken = string.Empty;
        string? refreshToken = null;
        if (root.TryGetProperty("access_token", out var atk))
            accessToken = atk.GetString() ?? "";
        else if (root.TryGetProperty("session", out var session) && session.TryGetProperty("token", out var stk))
            accessToken = stk.GetString() ?? "";

        if (root.TryGetProperty("refresh_token", out var rtk))
            refreshToken = rtk.GetString();

        if (string.IsNullOrEmpty(accessToken))
        {
            _logger.LogError("WorkOS authentication response did not contain an access token. Body: {Body}", responseBody);
            return StatusCode(500, new { message = "Invalid auth response from identity provider." });
        }

        // Recuperar información de User desde payload de WorkOS para bindear el id con la bd antes del /me
        if (root.TryGetProperty("user", out var userEl))
        {
            var externalId = userEl.TryGetProperty("id", out var idEl) ? idEl.GetString() : null;
            var email = userEl.TryGetProperty("email", out var eEl) ? eEl.GetString() : null;
            var firstName = userEl.TryGetProperty("first_name", out var fEl) ? fEl.GetString() : "";
            var lastName = userEl.TryGetProperty("last_name", out var lEl) ? lEl.GetString() : "";
            var nombreCompleto = $"{firstName} {lastName}".Trim();
            
            // WorkOS a veces no manda rol explicito aca pero sabemos el email y el id, así que podemos atarlo
            if (!string.IsNullOrEmpty(externalId) && !string.IsNullOrEmpty(email))
            {
                try
                {
                    await _usuarioService.ObtenerOCrearUsuarioAsync(externalId, email, string.IsNullOrEmpty(nombreCompleto) ? null : nombreCompleto, null);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error sincronizando usuario WorkOS: {Email}", email);
                }
            }
        }

        return Ok(new { accessToken, refreshToken });
    }

    /// <summary>
    /// Intercambia un refresh_token de WorkOS por un nuevo access_token.
    /// </summary>
    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh([FromBody] RefreshRequest request)
    {
        if (string.IsNullOrEmpty(request.RefreshToken))
            return BadRequest(new { message = "refresh_token es requerido" });

        var client = _httpClientFactory.CreateClient("workos");

        var body = new
        {
            client_id     = _workos.ClientId,
            client_secret = _workos.ApiKey,
            refresh_token = request.RefreshToken,
            grant_type    = "refresh_token",
        };

        var response = await client.PostAsJsonAsync(
            "https://api.workos.com/user_management/authenticate",
            body);

        var responseBody = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("WorkOS refresh failed {Status}: {Body}", response.StatusCode, responseBody);
            return StatusCode((int)response.StatusCode, JsonSerializer.Deserialize<object>(responseBody));
        }

        using var document = JsonDocument.Parse(responseBody);
        var root = document.RootElement;

        string accessToken = string.Empty;
        string? refreshToken = null;
        if (root.TryGetProperty("access_token", out var atk))
            accessToken = atk.GetString() ?? "";
        if (root.TryGetProperty("refresh_token", out var rtk))
            refreshToken = rtk.GetString();

        if (string.IsNullOrEmpty(accessToken))
            return StatusCode(500, new { message = "No access_token in refresh response" });

        return Ok(new { accessToken, refreshToken });
    }

    public record CallbackRequest(
        [property: JsonPropertyName("code")] string Code,
        [property: JsonPropertyName("codeVerifier")] string CodeVerifier
    );

    public record RefreshRequest(
        [property: JsonPropertyName("refreshToken")] string RefreshToken
    );
}
