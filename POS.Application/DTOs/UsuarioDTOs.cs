namespace POS.Application.DTOs;

/// <summary>
/// Resumen de sucursal para listas de asignación
/// </summary>
public record SucursalResumenDto(int Id, string Nombre);

/// <summary>
/// DTO para información completa del usuario
/// </summary>
public record UsuarioDto(
    int Id,
    string KeycloakId,
    string Email,
    string NombreCompleto,
    string? Telefono,
    string Rol,
    int? SucursalDefaultId,
    string? SucursalDefaultNombre,
    bool Activo,
    DateTime FechaCreacion,
    DateTime? UltimoAcceso,
    string CreadoPor,
    DateTime? FechaModificacion,
    string? ModificadoPor,
    List<SucursalResumenDto> SucursalesAsignadas
);

/// <summary>
/// DTO para perfil del usuario actual (información limitada)
/// </summary>
public record PerfilUsuarioDto(
    int Id,
    string Email,
    string NombreCompleto,
    string? Telefono,
    string Rol,
    int? SucursalDefaultId,
    string? SucursalDefaultNombre,
    DateTime? UltimoAcceso,
    IEnumerable<string> Permisos,
    List<SucursalResumenDto> SucursalesAsignadas
);

/// <summary>
/// DTO para asignar múltiples sucursales a un usuario
/// </summary>
public record AsignarSucursalesDto(List<int> SucursalIds);

/// <summary>
/// DTO para actualizar sucursal default del usuario
/// </summary>
public record ActualizarSucursalDefaultDto(
    int SucursalId
);

/// <summary>
/// DTO para activar/desactivar usuario
/// </summary>
public record CambiarEstadoUsuarioDto(
    bool Activo,
    string? Motivo
);

/// <summary>
/// DTO para estadísticas de usuarios
/// </summary>
public record EstadisticasUsuariosDto(
    int TotalUsuarios,
    int UsuariosActivos,
    int UsuariosInactivos,
    Dictionary<string, int> UsuariosPorRol,
    int UsuariosConectadosHoy,
    int UsuariosConectadosUltimaSemana,
    List<UsuarioRecienteDto> UsuariosRecientes
);

/// <summary>
/// DTO para usuarios recientes
/// </summary>
public record UsuarioRecienteDto(
    int Id,
    string Email,
    string NombreCompleto,
    string Rol,
    DateTime FechaCreacion,
    DateTime? UltimoAcceso
);

/// <summary>
/// DTO para filtros de búsqueda de usuarios
/// </summary>
public record FiltrosUsuarioDto(
    string? Busqueda,
    string? Rol,
    bool? Activo,
    int? SucursalId
);
