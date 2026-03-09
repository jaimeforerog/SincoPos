using System.Security.Cryptography;
using Azure.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Graph;
using Microsoft.Graph.Models;
using Microsoft.Graph.ServicePrincipals.Item.AppRoleAssignedTo;
using POS.Application.Services;
using POS.Infrastructure.Configuration;

namespace POS.Infrastructure.Services;

/// <summary>
/// Implementacion de IIdentityProviderService usando Microsoft Graph API (Entra ID).
/// Requiere un tenant organizacional con App Registration y permisos:
/// User.ReadWrite.All, Directory.ReadWrite.All, AppRoleAssignment.ReadWrite.All (Application).
/// </summary>
public class EntraIdService : IIdentityProviderService
{
    private readonly GraphServiceClient _graphClient;
    private readonly MicrosoftGraphOptions _options;
    private readonly ILogger<EntraIdService> _logger;

    public EntraIdService(IOptions<MicrosoftGraphOptions> options, ILogger<EntraIdService> logger)
    {
        _options = options.Value;
        _logger = logger;

        var credential = new ClientSecretCredential(
            _options.TenantId,
            _options.ClientId,
            _options.ClientSecret);

        _graphClient = new GraphServiceClient(credential, new[] { "https://graph.microsoft.com/.default" });
    }

    public async Task<(string? ExternalId, string? Error)> CrearUsuarioAsync(
        string email, string displayName, string? tempPassword)
    {
        try
        {
            var password = tempPassword ?? GenerateRandomPassword(12);
            var mailNickname = email.Split('@')[0].Replace(".", "").Replace("+", "");

            // Generar UPN en el dominio del tenant
            var upn = string.IsNullOrEmpty(_options.TenantDomain)
                ? email
                : $"{mailNickname}@{_options.TenantDomain}";

            var user = new User
            {
                AccountEnabled = true,
                DisplayName = displayName,
                MailNickname = mailNickname,
                UserPrincipalName = upn,
                Mail = email, // Email real del usuario
                PasswordProfile = new PasswordProfile
                {
                    Password = password,
                    ForceChangePasswordNextSignIn = true
                }
            };

            var createdUser = await _graphClient.Users.PostAsync(user);

            if (createdUser?.Id == null)
                return (null, "Graph API no retorno ID para el usuario creado");

            _logger.LogInformation(
                "[EntraId] Usuario creado: Email={Email}, UPN={Upn}, ObjectId={ObjectId}",
                email, upn, createdUser.Id);

            return (createdUser.Id, null);
        }
        catch (ServiceException ex) when (ex.ResponseStatusCode == 409)
        {
            _logger.LogWarning("[EntraId] Usuario ya existe en Entra ID: {Email}", email);
            return (null, "El usuario ya existe en el directorio de Entra ID");
        }
        catch (ServiceException ex)
        {
            _logger.LogError(ex, "[EntraId] Error al crear usuario {Email}: {Code} {Message}",
                email, ex.ResponseStatusCode, ex.Message);
            return (null, $"Error de Entra ID ({ex.ResponseStatusCode}): {ex.Message}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[EntraId] Error inesperado al crear usuario {Email}", email);
            return (null, $"Error inesperado: {ex.Message}");
        }
    }

    public async Task<(bool Success, string? Error)> AsignarRolAsync(string externalId, string rol)
    {
        if (string.IsNullOrEmpty(_options.ServicePrincipalId))
        {
            _logger.LogWarning("[EntraId] ServicePrincipalId no configurado, omitiendo asignacion de rol");
            return (true, null);
        }

        if (!_options.AppRoleIds.TryGetValue(rol.ToLower(), out var appRoleId))
        {
            _logger.LogWarning("[EntraId] Rol '{Rol}' no tiene AppRoleId configurado, omitiendo", rol);
            return (true, null);
        }

        try
        {
            // Primero remover roles existentes del usuario en esta app
            await RemoverRolesExistentesAsync(externalId);

            // Asignar el nuevo rol
            var assignment = new Microsoft.Graph.Models.AppRoleAssignment
            {
                PrincipalId = Guid.Parse(externalId),
                ResourceId = Guid.Parse(_options.ServicePrincipalId),
                AppRoleId = Guid.Parse(appRoleId)
            };

            await _graphClient.ServicePrincipals[_options.ServicePrincipalId]
                .AppRoleAssignedTo
                .PostAsync(assignment);

            _logger.LogInformation(
                "[EntraId] Rol '{Rol}' asignado a usuario {ExternalId}", rol, externalId);

            return (true, null);
        }
        catch (ServiceException ex)
        {
            _logger.LogError(ex, "[EntraId] Error al asignar rol '{Rol}' a {ExternalId}: {Message}",
                rol, externalId, ex.Message);
            return (false, $"Error al asignar rol: {ex.Message}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[EntraId] Error inesperado al asignar rol");
            return (false, $"Error inesperado: {ex.Message}");
        }
    }

    public async Task<(bool Success, string? Error)> DesactivarUsuarioAsync(string externalId)
    {
        try
        {
            await _graphClient.Users[externalId].PatchAsync(new User
            {
                AccountEnabled = false
            });

            _logger.LogInformation("[EntraId] Usuario desactivado: {ExternalId}", externalId);
            return (true, null);
        }
        catch (ServiceException ex) when (ex.ResponseStatusCode == 404)
        {
            _logger.LogWarning("[EntraId] Usuario no encontrado en Entra ID: {ExternalId}", externalId);
            return (true, null); // Si no existe en Entra ID, considerarlo exitoso
        }
        catch (ServiceException ex)
        {
            _logger.LogError(ex, "[EntraId] Error al desactivar usuario {ExternalId}", externalId);
            return (false, $"Error de Entra ID: {ex.Message}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[EntraId] Error inesperado al desactivar usuario");
            return (false, $"Error inesperado: {ex.Message}");
        }
    }

    public async Task<(bool Success, string? Error)> ActivarUsuarioAsync(string externalId)
    {
        try
        {
            await _graphClient.Users[externalId].PatchAsync(new User
            {
                AccountEnabled = true
            });

            _logger.LogInformation("[EntraId] Usuario activado: {ExternalId}", externalId);
            return (true, null);
        }
        catch (ServiceException ex) when (ex.ResponseStatusCode == 404)
        {
            _logger.LogWarning("[EntraId] Usuario no encontrado en Entra ID: {ExternalId}", externalId);
            return (false, "NOT_FOUND");
        }
        catch (ServiceException ex)
        {
            _logger.LogError(ex, "[EntraId] Error al activar usuario {ExternalId}", externalId);
            return (false, $"Error de Entra ID: {ex.Message}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[EntraId] Error inesperado al activar usuario");
            return (false, $"Error inesperado: {ex.Message}");
        }
    }

    public async Task<(string? TempPassword, string? Error)> ResetPasswordAsync(string externalId)
    {
        try
        {
            var newPassword = GenerateRandomPassword(12);

            await _graphClient.Users[externalId].PatchAsync(new User
            {
                PasswordProfile = new PasswordProfile
                {
                    Password = newPassword,
                    ForceChangePasswordNextSignIn = true
                }
            });

            _logger.LogInformation("[EntraId] Password reseteado para usuario {ExternalId}", externalId);
            return (newPassword, null);
        }
        catch (ServiceException ex) when (ex.ResponseStatusCode == 404)
        {
            _logger.LogWarning("[EntraId] Usuario no encontrado en Entra ID: {ExternalId}", externalId);
            return (null, "NOT_FOUND");
        }
        catch (ServiceException ex)
        {
            _logger.LogError(ex, "[EntraId] Error al resetear password de {ExternalId}", externalId);
            return (null, $"Error de Entra ID: {ex.Message}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[EntraId] Error inesperado al resetear password");
            return (null, $"Error inesperado: {ex.Message}");
        }
    }

    /// <summary>
    /// Remueve todos los app role assignments existentes del usuario en esta app.
    /// </summary>
    private async Task RemoverRolesExistentesAsync(string externalId)
    {
        try
        {
            var assignments = await _graphClient.ServicePrincipals[_options.ServicePrincipalId]
                .AppRoleAssignedTo
                .GetAsync(r =>
                {
                    r.QueryParameters.Filter = $"principalId eq {externalId}";
                });

            if (assignments?.Value == null) return;

            foreach (var assignment in assignments.Value)
            {
                if (assignment.Id != null)
                {
                    await _graphClient.ServicePrincipals[_options.ServicePrincipalId]
                        .AppRoleAssignedTo[assignment.Id]
                        .DeleteAsync();
                }
            }
        }
        catch (ServiceException ex)
        {
            // No fallar si no se pueden remover roles anteriores
            _logger.LogWarning(ex, "[EntraId] Error al remover roles existentes de {ExternalId}", externalId);
        }
    }

    private static string GenerateRandomPassword(int length)
    {
        // Entra ID requiere: mayuscula, minuscula, numero, caracter especial
        const string upper = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
        const string lower = "abcdefghijklmnopqrstuvwxyz";
        const string digits = "0123456789";
        const string special = "!@#$%&*";
        const string all = upper + lower + digits + special;

        var bytes = RandomNumberGenerator.GetBytes(length);
        var result = new char[length];

        // Garantizar al menos uno de cada tipo
        result[0] = upper[bytes[0] % upper.Length];
        result[1] = lower[bytes[1] % lower.Length];
        result[2] = digits[bytes[2] % digits.Length];
        result[3] = special[bytes[3] % special.Length];

        for (var i = 4; i < length; i++)
            result[i] = all[bytes[i] % all.Length];

        // Mezclar para no tener patron predecible
        var rng = RandomNumberGenerator.GetBytes(length);
        for (var i = length - 1; i > 0; i--)
        {
            var j = rng[i] % (i + 1);
            (result[i], result[j]) = (result[j], result[i]);
        }

        return new string(result);
    }
}
