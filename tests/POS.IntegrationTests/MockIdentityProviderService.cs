using POS.Application.Services;

namespace POS.IntegrationTests;

public class MockIdentityProviderService : IIdentityProviderService
{
    private static int _userCounter = 1;
    private static int _orgCounter = 1;
    private static int _membershipCounter = 1;
    private readonly HashSet<string> _emails = new();
    private readonly Dictionary<string, HashSet<string>> _memberships = new();

    public Task<(string? ExternalId, string? Error)> CrearUsuarioAsync(string email, string displayName, string? tempPassword)
    {
        // Simular que WorkOS rechaza emails duplicados (Conflict) para satisfacer UserCrudTests
        if (!_emails.Add(email))
        {
            return Task.FromResult<(string?, string?)>((null, "El usuario ya existe en WorkOS"));
        }

        var newId = $"mock-external-id-{_userCounter++}";
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

    public Task<(string? OrganizationId, string? Error)> CrearOrganizacionAsync(string nombre)
    {
        var id = $"mock-org-{_orgCounter++}";
        return Task.FromResult<(string?, string?)>((id, null));
    }

    public Task<(bool Success, string? Error)> ActualizarOrganizacionAsync(string organizationId, string nombre)
    {
        return Task.FromResult((true, (string?)null));
    }

    public Task<(string? MembershipId, string? Error)> CrearMembresiaAsync(string externalUserId, string organizationId)
    {
        if (!_memberships.TryGetValue(externalUserId, out var orgs))
        {
            orgs = new HashSet<string>();
            _memberships[externalUserId] = orgs;
        }
        if (!orgs.Add(organizationId))
            return Task.FromResult<(string?, string?)>((null, null)); // ya existía
        var id = $"mock-membership-{_membershipCounter++}";
        return Task.FromResult<(string?, string?)>((id, null));
    }

    public Task<(bool Success, string? Error)> EliminarMembresiaAsync(string externalUserId, string organizationId)
    {
        if (_memberships.TryGetValue(externalUserId, out var orgs))
            orgs.Remove(organizationId);
        return Task.FromResult((true, (string?)null));
    }

    public Task<(IReadOnlyList<string> OrganizationIds, string? Error)> ListarMembresiasAsync(string externalUserId)
    {
        if (_memberships.TryGetValue(externalUserId, out var orgs))
            return Task.FromResult<(IReadOnlyList<string>, string?)>((orgs.ToList(), null));
        return Task.FromResult<(IReadOnlyList<string>, string?)>((Array.Empty<string>(), null));
    }
}
