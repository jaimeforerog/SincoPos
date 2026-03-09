using System.Security.Cryptography;
using Microsoft.Extensions.Logging;
using POS.Application.Services;

namespace POS.Infrastructure.Services;

/// <summary>
/// Implementacion local del proveedor de identidad para desarrollo.
/// Genera GUIDs como ExternalId y contrasenas aleatorias.
/// </summary>
public class LocalIdentityProviderService : IIdentityProviderService
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
