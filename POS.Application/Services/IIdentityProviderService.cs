namespace POS.Application.Services;

/// <summary>
/// Abstraccion del proveedor de identidad externo (WorkOS / Entra ID).
/// Permite crear/gestionar usuarios en el IdP externo.
/// </summary>
public interface IIdentityProviderService
{
    /// <summary>
    /// Crea un usuario en el proveedor de identidad externo.
    /// Retorna el ExternalId asignado o un error.
    /// </summary>
    Task<(string? ExternalId, string? Error)> CrearUsuarioAsync(string email, string displayName, string? tempPassword);

    /// <summary>
    /// Asigna un rol al usuario en el proveedor de identidad.
    /// </summary>
    Task<(bool Success, string? Error)> AsignarRolAsync(string externalId, string rol);

    /// <summary>
    /// Desactiva un usuario en el proveedor de identidad.
    /// </summary>
    Task<(bool Success, string? Error)> DesactivarUsuarioAsync(string externalId);

    /// <summary>
    /// Activa un usuario previamente desactivado en el proveedor de identidad.
    /// </summary>
    Task<(bool Success, string? Error)> ActivarUsuarioAsync(string externalId);

    /// <summary>
    /// Resetea la contrasena del usuario y retorna la nueva contrasena temporal.
    /// </summary>
    Task<(string? TempPassword, string? Error)> ResetPasswordAsync(string externalId);
}
