using POS.Application.DTOs;

namespace POS.Application.Services;

/// <summary>
/// Interfaz para el servicio de productos.
/// Permite implementar diferentes origenes de datos (Local, ERP).
/// </summary>
public interface IProductoService
{
    Task<ProductoDto?> ObtenerPorIdAsync(Guid id);
    Task<ProductoDto?> ObtenerPorCodigoBarrasAsync(string codigoBarras);
    Task<PaginatedResult<ProductoDto>> BuscarAsync(string? query, int? categoriaId, bool incluirInactivos, int page = 1, int pageSize = 50);
    Task<(ProductoDto? Result, string? Error)> CrearAsync(CrearProductoDto dto);
    Task<(bool Success, string? Error)> ActualizarAsync(Guid id, ActualizarProductoDto dto);
    Task<(bool Success, string? Error)> DesactivarAsync(Guid id, string? motivo);
    Task<(bool Success, string? Error)> ActivarAsync(Guid id);
}
