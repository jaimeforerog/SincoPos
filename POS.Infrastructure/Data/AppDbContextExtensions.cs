using Microsoft.EntityFrameworkCore;

namespace POS.Infrastructure.Data;

public static class AppDbContextExtensions
{
    /// <summary>
    /// Resuelve el ID de un usuario a partir de su email.
    /// Retorna null si el email es nulo/vacío o no existe en la base de datos.
    /// </summary>
    public static async Task<int?> ResolverUsuarioIdAsync(this AppDbContext context, string? email)
    {
        if (string.IsNullOrEmpty(email)) return null;
        return await context.Usuarios
            .Where(u => u.Email == email)
            .Select(u => (int?)u.Id)
            .FirstOrDefaultAsync();
    }
}
