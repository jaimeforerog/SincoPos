namespace POS.Application.DTOs;

// ─── Activity Log Enums ─────────────────────────────────────────

/// <summary>
/// Categorías de actividades para facilitar filtrado y análisis
/// </summary>
public enum TipoActividad
{
    /// <summary>
    /// Operaciones de caja (apertura, cierre, movimientos) - CRÍTICA
    /// </summary>
    Caja = 1,

    /// <summary>
    /// Operaciones de ventas (creación, anulación)
    /// </summary>
    Venta = 2,

    /// <summary>
    /// Movimientos de inventario (entrada, salida, ajuste)
    /// </summary>
    Inventario = 3,

    /// <summary>
    /// Gestión de usuarios (creación, cambio estado, permisos)
    /// </summary>
    Usuario = 4,

    /// <summary>
    /// Cambios de precios - ALTA
    /// </summary>
    Precio = 10,

    /// <summary>
    /// Gestión de productos (CRUD)
    /// </summary>
    Producto = 11,

    /// <summary>
    /// Cambios en configuración de costeo
    /// </summary>
    Costeo = 12,

    /// <summary>
    /// Cambios en configuración del sistema - MEDIA
    /// </summary>
    Configuracion = 20,

    /// <summary>
    /// Eventos del sistema
    /// </summary>
    Sistema = 99
}

// ─── Activity Log DTOs ─────────────────────────────────────────

/// <summary>
/// DTO para registrar una nueva actividad en el log
/// </summary>
public record ActivityLogDto(
    string Accion,
    TipoActividad Tipo,
    string? Descripcion = null,
    int? SucursalId = null,
    string? IpAddress = null,
    string? UserAgent = null,
    string? TipoEntidad = null,
    string? EntidadId = null,
    string? EntidadNombre = null,
    object? DatosAnteriores = null,
    object? DatosNuevos = null,
    object? Metadatos = null,
    bool Exitosa = true,
    string? MensajeError = null
);

/// <summary>
/// DTO con toda la información de un log registrado (para consultas)
/// </summary>
public record ActivityLogFullDto(
    long Id,
    string UsuarioEmail,
    int? UsuarioId,
    DateTime FechaHora,
    string Accion,
    TipoActividad Tipo,
    string TipoNombre,
    int? SucursalId,
    string? NombreSucursal,
    string? IpAddress,
    string? UserAgent,
    string? TipoEntidad,
    string? EntidadId,
    string? EntidadNombre,
    string? Descripcion,
    string? DatosAnteriores,
    string? DatosNuevos,
    string? Metadatos,
    bool Exitosa,
    string? MensajeError
);

/// <summary>
/// DTO para filtrar logs en consultas
/// </summary>
public record ActivityLogFilterDto(
    DateTime? FechaDesde = null,
    DateTime? FechaHasta = null,
    string? UsuarioEmail = null,
    TipoActividad? Tipo = null,
    string? Accion = null,
    int? SucursalId = null,
    string? TipoEntidad = null,
    string? EntidadId = null,
    bool? Exitosa = null,
    int PageNumber = 1,
    int PageSize = 50
);



/// <summary>
/// DTO para métricas del dashboard
/// </summary>
public record DashboardActivityDto(
    DateTime Fecha,
    int TotalAcciones,
    int AccionesExitosas,
    int AccionesFallidas,
    Dictionary<string, int> AccionesPorTipo,
    List<ActividadRecienteDto> ActividadesRecientes
);

/// <summary>
/// DTO para actividades recientes en el dashboard
/// </summary>
public record ActividadRecienteDto(
    long Id,
    string UsuarioEmail,
    DateTime FechaHora,
    string Accion,
    string TipoNombre,
    string? Descripcion,
    bool Exitosa
);

/// <summary>
/// DTO para estadísticas por usuario
/// </summary>
public record EstadisticasUsuarioDto(
    string UsuarioEmail,
    int TotalAcciones,
    int AccionesExitosas,
    int AccionesFallidas,
    Dictionary<string, int> AccionesPorTipo,
    DateTime? UltimaActividad
);

/// <summary>
/// DTO para historial de una entidad específica
/// </summary>
public record HistorialEntidadDto(
    string TipoEntidad,
    string EntidadId,
    string? EntidadNombre,
    int TotalCambios,
    List<CambioEntidadDto> Cambios
);

/// <summary>
/// DTO para un cambio específico en una entidad
/// </summary>
public record CambioEntidadDto(
    long Id,
    DateTime FechaHora,
    string UsuarioEmail,
    string Accion,
    string? Descripcion,
    string? DatosAnteriores,
    string? DatosNuevos,
    bool Exitosa
);
