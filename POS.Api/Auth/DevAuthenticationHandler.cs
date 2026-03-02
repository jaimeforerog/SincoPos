using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace POS.Api.Auth;

/// <summary>
/// Handler de autenticación para desarrollo que siempre permite acceso
/// </summary>
public class DevAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public DevAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder)
    {
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.Name, "DevUser"),
            new Claim(ClaimTypes.Email, "dev@sincopos.com"),
            new Claim(ClaimTypes.Role, "admin"),
            new Claim(ClaimTypes.Role, "supervisor"),
            new Claim(ClaimTypes.Role, "cajero"),
            new Claim(ClaimTypes.Role, "vendedor")
        };

        var identity = new ClaimsIdentity(claims, "DevScheme");
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, "DevScheme");

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
