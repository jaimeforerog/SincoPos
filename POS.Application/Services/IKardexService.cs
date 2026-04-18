using POS.Application.DTOs;

namespace POS.Application.Services;

public interface IKardexService
{
    Task<ReporteKardexDto> ObtenerKardexAsync(
        Guid productoId, int sucursalId, DateTime fechaDesde, DateTime fechaHasta);
}
