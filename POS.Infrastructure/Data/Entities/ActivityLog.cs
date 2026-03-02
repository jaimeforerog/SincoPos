using POS.Application.DTOs;

namespace POS.Infrastructure.Data.Entities;

/// <summary>
/// Registra las actividades y acciones de negocio realizadas en el sistema.
/// Complementa la auditoría automática de EntidadAuditable para acciones específicas
/// como apertura/cierre de cajas, anulación de ventas, cambios de estado, etc.
/// </summary>
public class ActivityLog
{
    /// <summary>
    /// Identificador único del log (BIGINT para escalabilidad)
    /// </summary>
    public long Id { get; set; }

    // ========== WHO (Quién) ==========

    /// <summary>
    /// Email del usuario que ejecutó la acción
    /// </summary>
    public string UsuarioEmail { get; set; } = string.Empty;

    /// <summary>
    /// ID del usuario (FK nullable - puede ser null si el usuario fue eliminado)
    /// </summary>
    public int? UsuarioId { get; set; }

    // ========== WHEN (Cuándo) ==========

    /// <summary>
    /// Fecha y hora de la acción (UTC)
    /// </summary>
    public DateTime FechaHora { get; set; } = DateTime.UtcNow;

    // ========== WHAT (Qué) ==========

    /// <summary>
    /// Nombre de la acción ejecutada (ej: "AperturaCaja", "AnulacionVenta")
    /// </summary>
    public string Accion { get; set; } = string.Empty;

    /// <summary>
    /// Categoría de la actividad (Caja, Venta, Inventario, etc.)
    /// </summary>
    public TipoActividad Tipo { get; set; }

    // ========== WHERE (Dónde) ==========

    /// <summary>
    /// ID de la sucursal donde se ejecutó la acción (FK nullable)
    /// </summary>
    public int? SucursalId { get; set; }

    /// <summary>
    /// Dirección IP del cliente
    /// </summary>
    public string? IpAddress { get; set; }

    /// <summary>
    /// User-Agent del navegador/cliente
    /// </summary>
    public string? UserAgent { get; set; }

    // ========== ENTITY CONTEXT (Sobre qué entidad) ==========

    /// <summary>
    /// Tipo de entidad afectada (ej: "Venta", "Caja", "Usuario")
    /// </summary>
    public string? TipoEntidad { get; set; }

    /// <summary>
    /// ID de la entidad afectada (string para soportar diferentes tipos de IDs)
    /// </summary>
    public string? EntidadId { get; set; }

    /// <summary>
    /// Nombre o descripción de la entidad para facilitar búsquedas
    /// </summary>
    public string? EntidadNombre { get; set; }

    // ========== DETAILS (Detalles) ==========

    /// <summary>
    /// Descripción legible de la acción
    /// </summary>
    public string? Descripcion { get; set; }

    /// <summary>
    /// Datos anteriores en formato JSON (para cambios)
    /// </summary>
    public string? DatosAnteriores { get; set; }

    /// <summary>
    /// Datos nuevos en formato JSON
    /// </summary>
    public string? DatosNuevos { get; set; }

    /// <summary>
    /// Metadatos adicionales en formato JSON
    /// </summary>
    public string? Metadatos { get; set; }

    // ========== RESULT (Resultado) ==========

    /// <summary>
    /// Indica si la acción fue exitosa
    /// </summary>
    public bool Exitosa { get; set; } = true;

    /// <summary>
    /// Mensaje de error si la acción falló
    /// </summary>
    public string? MensajeError { get; set; }

    // ========== NAVIGATION PROPERTIES ==========

    public Usuario? Usuario { get; set; }
    public Sucursal? Sucursal { get; set; }
}
