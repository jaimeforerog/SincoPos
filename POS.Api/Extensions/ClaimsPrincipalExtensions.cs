using System.Security.Claims;

namespace POS.Api.Extensions;

/// <summary>
/// Extensiones para obtener información del usuario autenticado.
/// Compatible con Entra ID (Azure AD) y Keycloak.
/// </summary>
public static class ClaimsPrincipalExtensions
{
    /// <summary>
    /// Obtiene el ID externo del proveedor de identidad.
    /// Entra ID usa "oid", Keycloak usa "sub" / NameIdentifier.
    /// </summary>
    public static string? GetExternalId(this ClaimsPrincipal principal)
    {
        // Entra ID: "oid" (Object ID) — estable entre tokens
        return principal.FindFirst("oid")?.Value
            ?? principal.FindFirst("http://schemas.microsoft.com/identity/claims/objectidentifier")?.Value
            ?? principal.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? principal.FindFirst("sub")?.Value;
    }

    /// <summary>
    /// Alias para compatibilidad con código existente.
    /// </summary>
    [Obsolete("Usar GetExternalId() en su lugar")]
    public static string? GetKeycloakId(this ClaimsPrincipal principal)
    {
        return principal.GetExternalId();
    }

    /// <summary>
    /// Obtiene el email del usuario
    /// </summary>
    public static string? GetEmail(this ClaimsPrincipal principal)
    {
        return principal.FindFirst(ClaimTypes.Email)?.Value
            ?? principal.FindFirst("email")?.Value
            ?? principal.FindFirst("preferred_username")?.Value;
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
    /// Obtiene los roles del usuario desde Entra ID o Keycloak.
    /// Entra ID envía roles en claim "roles", Keycloak en "realm_access.roles".
    /// </summary>
    public static IEnumerable<string> GetRoles(this ClaimsPrincipal principal)
    {
        var roleClaims = principal.FindAll(ClaimTypes.Role)
            .Concat(principal.FindAll("roles"))
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
