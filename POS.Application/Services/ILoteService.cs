using POS.Application.DTOs;

namespace POS.Application.Services;

public interface ILoteService
{
    Task<List<LoteDto>> ObtenerLotesAsync(Guid productoId, int sucursalId, bool soloVigentes = true);
    Task<List<AlertaLoteDto>> ObtenerProximosAVencerAsync(int sucursalId, int diasAnticipacion);
    Task<(LoteDto? result, string? error)> ActualizarLoteAsync(int id, ActualizarLoteDto dto);
    Task<List<AlertaLoteDto>> ObtenerTodasLasAlertasAsync();
}
