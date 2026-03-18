using POS.Application.DTOs;
using POS.Domain.Aggregates;

namespace POS.Application.Services;

public interface IRadarNegocioService
{
    /// <summary>
    /// Retorna el radar de negocio para una sucursal: métricas del día + ventas por hora
    /// (desde EF Core) y riesgos de ruptura de stock.
    /// </summary>
    Task<RadarNegocioDto?> ObtenerRadarAsync(int sucursalId);

    /// <summary>
    /// Retorna el documento BusinessRadar acumulado en Marten (velocidad histórica
    /// de productos e ingresos por fecha/hora). null si la sucursal nunca tuvo ventas.
    /// </summary>
    Task<BusinessRadar?> ObtenerPatronAsync(int sucursalId);
}
