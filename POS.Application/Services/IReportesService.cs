using POS.Application.DTOs;

namespace POS.Application.Services;

public interface IReportesService
{
    Task<ReporteVentasDto> ObtenerReporteVentasAsync(
        DateTime fechaDesde, DateTime fechaHasta, int? sucursalId = null, int? metodoPago = null);

    Task<ReporteInventarioValorizadoDto> ObtenerInventarioValorizadoAsync(
        int? sucursalId = null, int? categoriaId = null, bool soloConStock = false);

    Task<(ReporteCajaDto? reporte, string? error)> ObtenerReporteCajaAsync(
        int cajaId, DateTime? fechaDesde = null, DateTime? fechaHasta = null);

    Task<ReporteAuditoriaComprasDto> ObtenerAuditoriaComprasAsync(
        ReporteAuditoriaComprasQueryDto query);
}
