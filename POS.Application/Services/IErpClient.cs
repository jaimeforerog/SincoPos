using POS.Application.DTOs;

namespace POS.Application.Services;

/// <summary>
/// Contrato del cliente de integración para contabilizar operaciones
/// en un ERP (External Resource Planning) como Sinco.
/// Está aislado de la Infraestructura para permitir Inversión de Dependencias.
/// </summary>
public interface IErpClient
{
    /// <summary>
    /// Envía el payload de una recepción de compra (ya sea total o parcial)
    /// para que el ERP contabilice el ingreso al inventario y liquide facturas.
    /// </summary>
    Task<ErpResponse> ContabilizarCompraAsync(CompraErpPayload payload);

    /// <summary>
    /// Consulta el estado actual de un documento de integración en el ERP
    /// utilizando su identificador único o referencia cruzada.
    /// </summary>
    Task<ErpResponse> ConsultarEstadoDocumentoAsync(string erpReferencia);
}
