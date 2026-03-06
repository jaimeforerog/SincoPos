using POS.Application.DTOs;

namespace POS.Application.Services;

public interface IFacturacionService
{
    Task<(DocumentoElectronicoDto? doc, string? error)> EmitirFacturaVentaAsync(int ventaId);
    Task<(DocumentoElectronicoDto? doc, string? error)> EmitirNotaCreditoAsync(int devolucionId);
    Task<PaginatedResult<DocumentoElectronicoDto>> ListarAsync(FiltroDocumentosElectronicosDto filtro);
    Task<DocumentoElectronicoDto?> ObtenerAsync(int id);
    Task<string?> ObtenerXmlAsync(int id);
    Task<(DocumentoElectronicoDto? doc, string? error)> ReintentarAsync(int documentoId);
    Task<DianRespuesta?> ConsultarEstadoDianAsync(int documentoId);
    Task<ConfiguracionEmisorDto?> ObtenerConfiguracionAsync(int sucursalId);
    Task ActualizarConfiguracionAsync(int sucursalId, ActualizarConfiguracionEmisorDto dto);
}
