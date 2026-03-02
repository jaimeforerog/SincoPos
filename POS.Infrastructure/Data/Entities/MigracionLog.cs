namespace POS.Infrastructure.Data.Entities;

/// <summary>
/// Log de migraciones de base de datos aplicadas.
/// Complementa __ef_migrations_history con información adicional de auditoría.
/// </summary>
public class MigracionLog
{
    public int Id { get; set; }

    /// <summary>
    /// Nombre de la migración (mismo que en __ef_migrations_history)
    /// Ejemplo: "20260302210242_AgregarOrigenDatoAPrecioSucursal"
    /// </summary>
    public string MigracionId { get; set; } = string.Empty;

    /// <summary>
    /// Descripción legible de los cambios realizados
    /// Ejemplo: "Agregar campo OrigenDato a tabla precios_sucursal"
    /// </summary>
    public string Descripcion { get; set; } = string.Empty;

    /// <summary>
    /// Versión de Entity Framework Core utilizada
    /// </summary>
    public string ProductVersion { get; set; } = string.Empty;

    /// <summary>
    /// Fecha y hora de aplicación de la migración (UTC)
    /// </summary>
    public DateTime FechaAplicacion { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Usuario o sistema que aplicó la migración
    /// </summary>
    public string AplicadoPor { get; set; } = "sistema";

    /// <summary>
    /// Estado de la migración: Success, Failed, Reverted
    /// </summary>
    public string Estado { get; set; } = "Success";

    /// <summary>
    /// Tiempo de ejecución en milisegundos
    /// </summary>
    public long DuracionMs { get; set; }

    /// <summary>
    /// Información adicional o errores
    /// </summary>
    public string? Notas { get; set; }

    /// <summary>
    /// Comando SQL ejecutado (opcional, puede ser largo)
    /// </summary>
    public string? SqlEjecutado { get; set; }
}
