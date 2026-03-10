namespace POS.Infrastructure.Data.Entities;

public enum EstadoOutbox
{
    Pendiente = 0,
    Procesado = 1,
    Error = 2,
    Descartado = 3
}

/// <summary>
/// Mensaje saliente almacenado de forma atómica bajo el patrón Outbox.
/// Acumula las sincronizaciones pendientes hacia el ERP (como las recepciones de Compras)
/// para evitar latencias altas o pérdida de datos por caídas de red al integrarse.
/// </summary>
public class ErpOutboxMessage
{
    public long Id { get; set; }
    
    /// <summary>
    /// Clasificador del tipo de documento o payload a enviar (ej. "CompraRecibida")
    /// </summary>
    public string TipoDocumento { get; set; } = string.Empty;
    
    /// <summary>
    /// ID de la entidad a la cual está ligada este mensaje (ej: OrdenCompraId)
    /// </summary>
    public int EntidadId { get; set; }
    
    /// <summary>
    /// Payload serializado en formato JSON que representa el contrato esperado por IErpClient
    /// </summary>
    public string Payload { get; set; } = string.Empty;
    
    public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;
    
    public DateTime? FechaProcesamiento { get; set; }
    
    public int Intentos { get; set; } = 0;
    
    public string? UltimoError { get; set; }
    
    public EstadoOutbox Estado { get; set; } = EstadoOutbox.Pendiente;
}
