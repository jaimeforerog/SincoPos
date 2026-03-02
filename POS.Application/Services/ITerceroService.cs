using POS.Application.DTOs;

namespace POS.Application.Services;

/// <summary>
/// Interfaz para el servicio de terceros.
/// Permite implementar diferentes origenes de datos (Local, ERP).
/// </summary>
public interface ITerceroService
{
    Task<TerceroDto?> ObtenerPorIdAsync(int id);
    Task<TerceroDto?> ObtenerPorIdentificacionAsync(string identificacion);
    Task<List<TerceroDto>> BuscarAsync(string? query, string? tipoTercero, bool incluirInactivos);
    Task<(TerceroDto? Result, string? Error)> CrearAsync(CrearTerceroDto dto);
    Task<(bool Success, string? Error)> ActualizarAsync(int id, ActualizarTerceroDto dto);
    Task<(bool Success, string? Error)> DesactivarAsync(int id);
}
