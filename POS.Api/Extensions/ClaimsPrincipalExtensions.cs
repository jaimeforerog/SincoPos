using System.Security.Claims;

namespace POS.Api.Extensions;

/// <summary>
/// Extensiones para obtener información del usuario autenticado
/// </summary>
public static class ClaimsPrincipalExtensions
{
    /// <summary>
    /// Obtiene el Keycloak Subject ID (UUID del usuario)
    /// </summary>
    public static string? GetKeycloakId(this ClaimsPrincipal principal)
    {
        return principal.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? principal.FindFirst("sub")?.Value;
    }

    /// <summary>
    /// Obtiene el email del usuario
    /// </summary>
    public static string? GetEmail(this ClaimsPrincipal principal)
    {
        return principal.FindFirst(ClaimTypes.Email)?.Value
            ?? principal.FindFirst("email")?.Value;
    }

    /// <summary>
    /// Obtiene el nombre completo del usuario
    /// </summary>
    public static string? GetNombreCompleto(this ClaimsPrincipal principal)
    {
        return principal.FindFirst(ClaimTypes.Name)?.Value
            ?? principal.FindFirst("name")?.Value
            ?? principal.FindFirst("preferred_username")?.Value;
    }

    /// <summary>
    /// Obtiene el username preferido
    /// </summary>
    public static string? GetUsername(this ClaimsPrincipal principal)
    {
        return principal.FindFirst("preferred_username")?.Value
            ?? principal.FindFirst(ClaimTypes.Name)?.Value;
    }

    /// <summary>
    /// Obtiene los roles del usuario desde Keycloak
    /// Los roles pueden estar en diferentes claims dependiendo de la configuración
    /// </summary>
    public static IEnumerable<string> GetRoles(this ClaimsPrincipal principal)
    {
        // Keycloak puede enviar roles en diferentes claims
        var roleClaims = principal.FindAll(ClaimTypes.Role)
            .Concat(principal.FindAll("realm_access.roles"))
            .Concat(principal.FindAll("role"))
            .Select(c => c.Value)
            .Distinct();

        return roleClaims;
    }

    /// <summary>
    /// Verifica si el usuario tiene un rol específico
    /// </summary>
    public static bool TieneRol(this ClaimsPrincipal principal, string rol)
    {
        return principal.GetRoles().Any(r => r.Equals(rol, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Verifica si el usuario tiene al menos uno de los roles especificados
    /// </summary>
    public static bool TieneAlgunoDeEstosRoles(this ClaimsPrincipal principal, params string[] roles)
    {
        var userRoles = principal.GetRoles().Select(r => r.ToLower());
        return roles.Any(r => userRoles.Contains(r.ToLower()));
    }
}
