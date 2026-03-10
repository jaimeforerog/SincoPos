using System;
using System.Collections.Generic;

namespace POS.Infrastructure.Data.Entities;

public class DocumentoContable : EntidadAuditable
{
    // Identificadores Auxiliares
    public string TipoDocumento { get; set; } = string.Empty; // Ej: Recepción Compra, Factura de Venta
    public string NumeroSoporte { get; set; } = string.Empty; // Ej: Número de OC o Factura
    
    // Relaciones
    public int? TerceroId { get; set; }
    public Tercero? Tercero { get; set; }
    public int? SucursalId { get; set; }
    public Sucursal? Sucursal { get; set; }
    
    // Datos Financieros (Tesorería)
    public DateTime FechaCausacion { get; set; }
    public string FormaPago { get; set; } = "Contado";
    public DateTime? FechaVencimiento { get; set; }
    
    // Sumas
    public decimal TotalDebito { get; set; }
    public decimal TotalCredito { get; set; }
    
    // Trazabilidad ERP Específica del Documento
    public bool SincronizadoErp { get; set; } = false;
    public string? ErpReferencia { get; set; }
    public DateTime? FechaSincronizacionErp { get; set; }
    
    public List<DetalleDocumentoContable> Detalles { get; set; } = new();
}
