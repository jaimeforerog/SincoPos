namespace POS.Application.DTOs;

/// <summary>
/// Representa la carga útil (Payload) contable de una recepción de compra en SincoPos 
/// formateada de manera neutra para que los adaptadores de ERP (como SincoErpClient)
/// los procesen.
/// </summary>
public record CompraErpPayload(
    string NumeroOrden,
    string NitProveedor,
    string FormaPago,
    DateTime FechaVencimientoErp,
    DateTime FechaRecepcion,
    int SucursalId,
    List<AsientoContableErp> Asientos,
    decimal TotalOriginalDocumento
);

/// <summary>
/// Representa una entrada de un comprobante de diario (Asiento Contable) para el ERP.
/// </summary>
public record AsientoContableErp(
    string Cuenta,
    string CentroCosto,
    string Naturaleza, // "Debito" o "Credito"
    decimal Valor,
    string Nota
);

/// <summary>
/// Respuesta general de un cliente ERP (Interface adapter) cuando contesta.
/// </summary>
public record ErpResponse(
    bool Exitoso,
    string? ErpReferencia,
    string? MensajeError
);
