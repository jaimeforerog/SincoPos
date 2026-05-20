using System.Security.Cryptography;
using Microsoft.Extensions.Logging;
using POS.Application.Services;

namespace POS.Infrastructure.Services;

/// <summary>
/// Implementacion local del proveedor de identidad para desarrollo.
/// Genera GUIDs como ExternalId y contrasenas aleatorias.
/// </summary>
public sealed class LocalIdentityProviderService : IIdentityProviderService
{
    private readonly ILogger<LocalIdentityProviderService> _logger;

    public LocalIdentityProviderService(ILogger<LocalIdentityProviderService> logger)
    {
        _logger = logger;
    }

    public Task<(string? ExternalId, string? Error)> CrearUsuarioAsync(string email, string displayName, string? tempPassword)
    {
        var externalId = Guid.NewGuid().ToString();

        _logger.LogInformation(
            "[LocalIdP] Usuario creado: Email={Email}, DisplayName={DisplayName}, ExternalId={ExternalId}",
            email, displayName, externalId);

        return Task.FromResult<(string? ExternalId, string? Error)>((externalId, null));
    }

    public Task<(bool Success, string? Error)> AsignarRolAsync(string externalId, string rol)
    {
        _logger.LogInformation(
            "[LocalIdP] Rol asignado: ExternalId={ExternalId}, Rol={Rol}",
            externalId, rol);

        return Task.FromResult<(bool Success, string? Error)>((true, null));
    }

    public Task<(bool Success, string? Error)> DesactivarUsuarioAsync(string externalId)
    {
        _logger.LogInformation(
            "[LocalIdP] Usuario desactivado: ExternalId={ExternalId}",
            externalId);

        return Task.FromResult<(bool Success, string? Error)>((true, null));
    }

    public Task<(bool Success, string? Error)> ActivarUsuarioAsync(string externalId)
    {
        _logger.LogInformation(
            "[LocalIdP] Usuario activado: ExternalId={ExternalId}",
            externalId);

        return Task.FromResult<(bool Success, string? Error)>((true, null));
    }

    public Task<(string? TempPassword, string? Error)> ResetPasswordAsync(string externalId)
    {
        var password = GenerateRandomPassword(12);

        _logger.LogInformation(
            "[LocalIdP] Password reseteado: ExternalId={ExternalId}",
            externalId);

        return Task.FromResult<(string? TempPassword, string? Error)>((password, null));
    }

    public Task<(string? OrganizationId, string? Error)> CrearOrganizacionAsync(string nombre)
    {
        var orgId = $"local-org-{Guid.NewGuid()}";
        _logger.LogInformation("[LocalIdP] Organization creada: {Nombre} → {Id}", nombre, orgId);
        return Task.FromResult<(string?, string?)>((orgId, null));
    }

    public Task<(bool Success, string? Error)> ActualizarOrganizacionAsync(string organizationId, string nombre)
    {
        _logger.LogInformation("[LocalIdP] Organization actualizada: {Id} → {Nombre}", organizationId, nombre);
        return Task.FromResult<(bool, string?)>((true, null));
    }

    public Task<(string? MembershipId, string? Error)> CrearMembresiaAsync(string externalUserId, string organizationId)
    {
        var id = $"local-membership-{Guid.NewGuid()}";
        _logger.LogInformation(
            "[LocalIdP] Membership creada: user={UserId} org={OrgId} → {Id}",
            externalUserId, organizationId, id);
        return Task.FromResult<(string?, string?)>((id, null));
    }

    public Task<(bool Success, string? Error)> EliminarMembresiaAsync(string externalUserId, string organizationId)
    {
        _logger.LogInformation(
            "[LocalIdP] Membership eliminada: user={UserId} org={OrgId}", externalUserId, organizationId);
        return Task.FromResult<(bool, string?)>((true, null));
    }

    public Task<(IReadOnlyList<string> OrganizationIds, string? Error)> ListarMembresiasAsync(string externalUserId)
    {
        return Task.FromResult<(IReadOnlyList<string>, string?)>((Array.Empty<string>(), null));
    }

    private static string GenerateRandomPassword(int length)
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789!@#$%&*";
        var bytes = RandomNumberGenerator.GetBytes(length);
        var result = new char[length];
        for (var i = 0; i < length; i++)
        {
            result[i] = chars[bytes[i] % chars.Length];
        }
        return new string(result);
    }
}
