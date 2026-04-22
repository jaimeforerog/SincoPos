using POS.Application.DTOs;

namespace POS.Infrastructure.Data.Entities;

/// <summary>
/// Almacén histórico de logs de actividad. Misma estructura que ActivityLog pero sin
/// foreign keys — las entidades referenciadas pueden haber sido eliminadas.
/// Los registros llegan aquí desde activity_logs vía la función de limpieza semanal.
/// </summary>
public class ActivityLogArchivo
{
    public long Id { get; set; }

    // WHO
    public string UsuarioEmail { get; set; } = string.Empty;
    public int? UsuarioId { get; set; }

    // WHEN
    public DateTime FechaHora { get; set; }

    // WHAT
    public string Accion { get; set; } = string.Empty;
    public TipoActividad Tipo { get; set; }

    // WHERE
    public int? SucursalId { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }

    // ENTITY CONTEXT
    public string? TipoEntidad { get; set; }
    public string? EntidadId { get; set; }
    public string? EntidadNombre { get; set; }

    // DETAILS
    public string? Descripcion { get; set; }
    public string? DatosAnteriores { get; set; }
    public string? DatosNuevos { get; set; }
    public string? Metadatos { get; set; }

    // RESULT
    public bool Exitosa { get; set; } = true;
    public string? MensajeError { get; set; }

    // ARCHIVO
    /// <summary>Fecha en que fue movido desde activity_logs a este archivo.</summary>
    public DateTime FechaArchivado { get; set; }
}
