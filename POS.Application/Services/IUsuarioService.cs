using POS.Application.DTOs;

namespace POS.Application.Services;

/// <summary>
/// Servicio para gestionar usuarios del sistema.
/// Metodos retornan DTOs para mantener la separacion Application/Infrastructure.
/// </summary>
public interface IUsuarioService
{
    // ── Metodos existentes (DTO-based) ────────────────────────────────────────

    /// <summary>
    /// Obtiene o crea un usuario basado en los claims del IdP.
    /// Retorna el UsuarioDto resultante.
    /// </summary>
    Task<UsuarioDto> ObtenerOCrearUsuarioAsync(string externalId, string email, string? nombreCompleto = null, string? rol = null);

    /// <summary>
    /// Obtiene un usuario por su ExternalId (WorkOS / Entra ID), incluyendo perfil completo.
    /// </summary>
    Task<PerfilUsuarioDto?> ObtenerPerfilPorExternalIdAsync(string externalId);

    /// <summary>
    /// Actualiza la sucursal por defecto de un usuario.
    /// </summary>
    Task<bool> ActualizarSucursalDefaultAsync(int usuarioId, int sucursalId);

    /// <summary>
    /// Asigna multiples sucursales a un usuario (reemplaza las existentes).
    /// </summary>
    Task AsignarSucursalesAsync(int usuarioId, List<int> sucursalIds);

    /// <summary>
    /// Obtiene todos los usuarios con filtros opcionales.
    /// </summary>
    Task<List<UsuarioDto>> ListarUsuariosAsync(string? busqueda = null, string? rol = null, bool? activo = null, int? sucursalId = null);

    /// <summary>
    /// Obtiene un usuario por su ID.
    /// </summary>
    Task<UsuarioDto?> ObtenerPorIdAsync(int id);

    /// <summary>
    /// Activa o desactiva un usuario.
    /// </summary>
    Task<bool> CambiarEstadoAsync(int usuarioId, bool activo);

    /// <summary>
    /// Obtiene todas las sucursales activas.
    /// </summary>
    Task<List<SucursalResumenDto>> ObtenerTodasSucursalesActivasAsync();

    /// <summary>
    /// Obtiene estadisticas de usuarios.
    /// </summary>
    Task<EstadisticasUsuariosDto> ObtenerEstadisticasAsync();

    // ── Metodos nuevos (User CRUD) ────────────────────────────────────────────

    Task<(CrearUsuarioResultDto? Result, string? Error)> CrearUsuarioAsync(CrearUsuarioDto dto, string creadorExternalId, string creadorRol);
    Task<(bool Success, string? Error)> ActualizarUsuarioAsync(int id, ActualizarUsuarioDto dto, string creadorRol);
    Task<(bool Success, string? Error)> CambiarRolAsync(int id, string nuevoRol, string creadorRol);
    Task<(string? TempPassword, string? Error)> ResetPasswordAsync(int id);
}
