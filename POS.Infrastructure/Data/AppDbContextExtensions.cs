using Microsoft.EntityFrameworkCore;

namespace POS.Infrastructure.Data;

public static class AppDbContextExtensions
{
    /// <summary>
    /// Resuelve el ID de un usuario a partir de su email (insensible a mayúsculas).
    /// Si el email no produce resultado, intenta por <paramref name="externalId"/> (WorkOS sub / Keycloak ID).
    /// Retorna null si ninguno produce resultado.
    /// </summary>
    public static async Task<int?> ResolverUsuarioIdAsync(
        this AppDbContext context, string? email, string? externalId = null)
    {
        if (!string.IsNullOrEmpty(email))
        {
            var emailLower = email.ToLowerInvariant();
            var porEmail = await context.Usuarios
                .Where(u => u.Email.ToLower() == emailLower)
                .Select(u => (int?)u.Id)
                .FirstOrDefaultAsync();
            if (porEmail.HasValue) return porEmail;
        }

        if (!string.IsNullOrEmpty(externalId))
        {
            return await context.Usuarios
                .Where(u => u.ExternalId == externalId)
                .Select(u => (int?)u.Id)
                .FirstOrDefaultAsync();
        }

        return null;
    }
}
