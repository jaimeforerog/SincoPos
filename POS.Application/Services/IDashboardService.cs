using POS.Application.DTOs;

namespace POS.Application.Services;

public interface IDashboardService
{
    Task<DashboardDto> ObtenerDashboardAsync(int? sucursalId = null);

    Task<List<TopProductoDto>> ObtenerTopProductosAsync(
        DateTime fechaDesde, DateTime fechaHasta, int? sucursalId = null, int limite = 10);
}
