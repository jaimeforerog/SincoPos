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
    Task<PaginatedResult<TerceroDto>> BuscarAsync(string? query, string? tipoTercero, bool incluirInactivos, int page = 1, int pageSize = 50);
    Task<(TerceroDto? Result, string? Error)> CrearAsync(CrearTerceroDto dto);
    Task<(bool Success, string? Error)> ActualizarAsync(int id, ActualizarTerceroDto dto);
    Task<(bool Success, string? Error)> DesactivarAsync(int id);

    // Actividades CIIU
    Task<(TerceroActividadDto? Result, string? Error)> AgregarActividadAsync(int terceroId, AgregarActividadDto dto);
    Task<(bool Success, string? Error)> EliminarActividadAsync(int terceroId, int actividadId);
    Task<(bool Success, string? Error)> EstablecerPrincipalAsync(int terceroId, int actividadId);

    // Importación Excel
    Task<ResultadoImportacionTercerosDto> ImportarDesdeExcelAsync(Stream stream);
    byte[] GenerarPlantillaExcel();
}
