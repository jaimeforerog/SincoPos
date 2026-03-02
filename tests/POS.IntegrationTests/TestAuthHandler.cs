using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace POS.IntegrationTests;

/// <summary>
/// Handler de autenticación para tests que permite simular diferentes usuarios autenticados.
/// </summary>
public class TestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string SchemeName = "TestAuth";

    public TestAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder)
    {
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        // Obtener email del usuario desde el header de prueba
        var email = Context.Request.Headers["X-Test-User"].FirstOrDefault();

        if (string.IsNullOrEmpty(email))
        {
            // Sin usuario - autenticación fallida
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        // Determinar rol basado en el email
        var role = email.ToLower() switch
        {
            var e when e.Contains("admin") => "admin",
            var e when e.Contains("supervisor") => "supervisor",
            var e when e.Contains("cajero") => "cajero",
            var e when e.Contains("vendedor") => "vendedor",
            _ => "vendedor" // Default role
        };

        var claims = new[]
        {
            new Claim(ClaimTypes.Email, email),
            new Claim("email", email),
            new Claim(ClaimTypes.Name, email),
            new Claim("preferred_username", email),
            new Claim(ClaimTypes.Role, role) // Agregar rol para políticas
        };

        var identity = new ClaimsIdentity(claims, SchemeName);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, SchemeName);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
