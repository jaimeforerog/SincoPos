using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using POS.Application.DTOs;
using POS.Application.Services;
using POS.Infrastructure.Data;
using POS.Infrastructure.Data.Entities;

namespace POS.Infrastructure.Services;

/// <summary>
/// Servicio para gestionar usuarios sincronizados con el proveedor de identidad.
/// Implementa IUsuarioService (DTO-based) y expone metodos adicionales con entidades
/// para uso interno (CajasController).
/// </summary>
public sealed partial class UsuarioService : IUsuarioService
{
    private readonly AppDbContext _context;
    private readonly ILogger<UsuarioService> _logger;
    private readonly IIdentityProviderService _identityProvider;

    public UsuarioService(
        AppDbContext context,
        ILogger<UsuarioService> logger,
        IIdentityProviderService identityProvider)
    {
        _context = context;
        _logger = logger;
        _identityProvider = identityProvider;
    }

    // ── Mapping helpers ──────────────────────────────────────────────────────

    private static UsuarioDto ToDto(Usuario u) => new(
        u.Id,
        u.ExternalId,
        u.Email,
        u.NombreCompleto,
        u.Telefono,
        u.Rol,
        u.SucursalDefaultId,
        u.SucursalDefault?.Nombre,
        u.Activo,
        u.FechaCreacion,
        u.UltimoAcceso,
        u.CreadoPor,
        u.FechaModificacion,
        u.ModificadoPor,
        u.Sucursales.Select(us => new SucursalResumenDto(us.Sucursal.Id, us.Sucursal.Nombre)).ToList()
    );

    // ── IUsuarioService implementation ───────────────────────────────────────

    /// <inheritdoc />
    async Task<UsuarioDto> IUsuarioService.ObtenerOCrearUsuarioAsync(
        string externalId, string email, string? nombreCompleto, string? rol)
    {
        var usuario = await ObtenerOCrearUsuarioEntityAsync(externalId, email, nombreCompleto, rol);
        // Reload with includes for DTO mapping
        var loaded = await _context.Usuarios
            .Include(u => u.SucursalDefault)
            .Include(u => u.Sucursales).ThenInclude(us => us.Sucursal)
            .FirstAsync(u => u.Id == usuario.Id);
        return ToDto(loaded);
    }

    /// <inheritdoc />
    async Task<PerfilUsuarioDto?> IUsuarioService.ObtenerPerfilPorExternalIdAsync(string externalId)
    {
        var usuario = await _context.Usuarios
            .Include(u => u.SucursalDefault)
            .FirstOrDefaultAsync(u => u.ExternalId == externalId);

        if (usuario == null)
            return null;

        // Cargar sucursales ignorando el filtro global de EmpresaId para obtener TODAS
        // las sucursales asignadas al usuario (multi-empresa).
        // Sin IgnoreQueryFilters(), EF filtra las sucursales de otras empresas y el
        // usuario solo vería las sucursales de la empresa activa en ese momento.
        var sucursalIds = await _context.UsuarioSucursales
            .Where(us => us.UsuarioId == usuario.Id)
            .Select(us => us.SucursalId)
            .ToListAsync();

        var sucursalesAsignadas = await _context.Sucursales
            .IgnoreQueryFilters()
            .Include(s => s.Empresa)
            .Where(s => sucursalIds.Contains(s.Id) && s.Activo)
            .Select(s => new SucursalResumenDto(s.Id, s.Nombre, s.EmpresaId, s.Empresa != null ? s.Empresa.Nombre : null))
            .ToListAsync();

        return new PerfilUsuarioDto(
            usuario.Id,
            usuario.Email,
            usuario.NombreCompleto,
            usuario.Telefono,
            usuario.Rol,
            usuario.SucursalDefaultId,
            usuario.SucursalDefault?.Nombre,
            usuario.UltimoAcceso,
            Enumerable.Empty<string>(), // Permisos se asignan en el controller
            sucursalesAsignadas
        );
    }

    /// <inheritdoc />
    public async Task<bool> ActualizarSucursalDefaultAsync(int usuarioId, int sucursalId)
    {
        var usuario = await _context.Usuarios
            .Include(u => u.Sucursales)
            .FirstOrDefaultAsync(u => u.Id == usuarioId);
        if (usuario == null)
            return false;

        var sucursalExiste = await _context.Sucursales.AnyAsync(s => s.Id == sucursalId && s.Activo);
        if (!sucursalExiste)
            return false;

        usuario.SucursalDefaultId = sucursalId;

        // Asegurar que la sucursal default esté en la tabla pivot
        if (!usuario.Sucursales.Any(us => us.SucursalId == sucursalId))
        {
            _context.UsuarioSucursales.Add(new UsuarioSucursal
            {
                UsuarioId = usuarioId,
                SucursalId = sucursalId
            });
        }

        await _context.SaveChangesAsync();
        return true;
    }

    /// <inheritdoc />
    public async Task AsignarSucursalesAsync(int usuarioId, List<int> sucursalIds)
    {
        var usuario = await _context.Usuarios
            .Include(u => u.Sucursales)
            .FirstOrDefaultAsync(u => u.Id == usuarioId);

        if (usuario == null)
            throw new KeyNotFoundException($"Usuario {usuarioId} no encontrado");

        // Eliminar asignaciones existentes
        _context.UsuarioSucursales.RemoveRange(usuario.Sucursales);

        // Insertar nuevas (solo sucursales activas)
        var sucursalesValidas = await _context.Sucursales
            .Where(s => sucursalIds.Contains(s.Id) && s.Activo)
            .Select(s => s.Id)
            .ToListAsync();

        foreach (var sid in sucursalesValidas)
        {
            _context.UsuarioSucursales.Add(new UsuarioSucursal
            {
                UsuarioId = usuarioId,
                SucursalId = sid
            });
        }

        await _context.SaveChangesAsync();

        _logger.LogInformation(
            "Sucursales asignadas a usuario {UsuarioId}: [{SucursalIds}]",
            usuarioId, string.Join(",", sucursalesValidas));
    }

    /// <inheritdoc />
    async Task<PaginatedResult<UsuarioDto>> IUsuarioService.ListarUsuariosAsync(
        string? busqueda, string? rol, bool? activo, int? sucursalId, int page, int pageSize)
    {
        var query = _context.Usuarios
            .Include(u => u.SucursalDefault)
            .Include(u => u.Sucursales).ThenInclude(us => us.Sucursal)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(busqueda))
            query = query.Where(u => u.NombreCompleto.Contains(busqueda) || u.Email.Contains(busqueda));

        if (!string.IsNullOrWhiteSpace(rol))
            query = query.Where(u => u.Rol == rol);

        if (activo.HasValue)
            query = query.Where(u => u.Activo == activo.Value);

        if (sucursalId.HasValue)
            query = query.Where(u =>
                u.SucursalDefaultId == sucursalId.Value ||
                u.Sucursales.Any(us => us.SucursalId == sucursalId.Value));

        var totalCount = await query.CountAsync();
        var items = await query
            .OrderByDescending(u => u.FechaCreacion)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);
        return new PaginatedResult<UsuarioDto>(items.Select(ToDto).ToList(), totalCount, page, pageSize, totalPages);
    }

    /// <inheritdoc />
    async Task<UsuarioDto?> IUsuarioService.ObtenerPorIdAsync(int id)
    {
        var usuario = await ObtenerPorIdEntityAsync(id);
        return usuario == null ? null : ToDto(usuario);
    }

    /// <inheritdoc />
    public async Task<bool> CambiarEstadoAsync(int usuarioId, bool activo)
    {
        var usuario = await _context.Usuarios.FindAsync(usuarioId);
        if (usuario == null)
            return false;

        usuario.Activo = activo;
        await _context.SaveChangesAsync();

        _logger.LogInformation(
            "Estado de usuario {Email} cambiado a {Estado}",
            usuario.Email, activo ? "Activo" : "Inactivo");

        return true;
    }

    /// <inheritdoc />
    async Task<List<SucursalResumenDto>> IUsuarioService.ObtenerTodasSucursalesActivasAsync()
    {
        return await _context.Sucursales
            .IgnoreQueryFilters()
            .Include(s => s.Empresa)
            .Where(s => s.Activo)
            .OrderBy(s => s.Nombre)
            .Select(s => new SucursalResumenDto(s.Id, s.Nombre, s.EmpresaId, s.Empresa != null ? s.Empresa.Nombre : null))
            .ToListAsync();
    }

    /// <inheritdoc />
    async Task<EstadisticasUsuariosDto> IUsuarioService.ObtenerEstadisticasAsync()
    {
        var ahora = DateTime.UtcNow;
        var hace24Horas = ahora.AddDays(-1);
        var hace7Dias = ahora.AddDays(-7);

        var totalUsuarios = await _context.Usuarios.CountAsync();
        var usuariosActivos = await _context.Usuarios.CountAsync(u => u.Activo);
        var usuariosInactivos = totalUsuarios - usuariosActivos;

        var usuariosPorRol = await _context.Usuarios
            .GroupBy(u => u.Rol)
            .Select(g => new { Rol = g.Key, Cantidad = g.Count() })
            .ToDictionaryAsync(x => x.Rol, x => x.Cantidad);

        var conectadosHoy = await _context.Usuarios
            .CountAsync(u => u.UltimoAcceso.HasValue && u.UltimoAcceso.Value >= hace24Horas);

        var conectadosSemana = await _context.Usuarios
            .CountAsync(u => u.UltimoAcceso.HasValue && u.UltimoAcceso.Value >= hace7Dias);

        var usuariosRecientes = await _context.Usuarios
            .OrderByDescending(u => u.FechaCreacion)
            .Take(5)
            .ToListAsync();

        return new EstadisticasUsuariosDto(
            totalUsuarios,
            usuariosActivos,
            usuariosInactivos,
            usuariosPorRol,
            conectadosHoy,
            conectadosSemana,
            usuariosRecientes.Select(u => new UsuarioRecienteDto(
                u.Id,
                u.Email,
                u.NombreCompleto,
                u.Rol,
                u.FechaCreacion,
                u.UltimoAcceso
            )).ToList()
        );
    }

}
