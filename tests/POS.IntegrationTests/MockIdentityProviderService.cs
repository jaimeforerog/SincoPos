using POS.Application.Services;

namespace POS.IntegrationTests;

public class MockIdentityProviderService : IIdentityProviderService
{
    private static int _counter = 1;
    private readonly HashSet<string> _emails = new();

    public Task<(string? ExternalId, string? Error)> CrearUsuarioAsync(string email, string displayName, string? tempPassword)
    {
        // Simular que WorkOS rechaza emails duplicados (Conflict) para satisfacer UserCrudTests
        if (!_emails.Add(email))
        {
            return Task.FromResult<(string?, string?)>((null, "El usuario ya existe en WorkOS"));
        }

        var newId = $"mock-external-id-{_counter++}";
        return Task.FromResult<(string?, string?)>((newId, null));
    }

    public Task<(bool Success, string? Error)> AsignarRolAsync(string externalId, string rol)
    {
        return Task.FromResult((true, (string?)null));
    }

    public Task<(bool Success, string? Error)> DesactivarUsuarioAsync(string externalId)
    {
        return Task.FromResult((true, (string?)null));
    }

    public Task<(bool Success, string? Error)> ActivarUsuarioAsync(string externalId)
    {
        return Task.FromResult((true, (string?)null));
    }

    public Task<(string? TempPassword, string? Error)> ResetPasswordAsync(string externalId)
    {
        return Task.FromResult<(string?, string?)>(("temp-pwd-123", null));
    }
}
