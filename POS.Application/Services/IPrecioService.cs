using POS.Application.DTOs;

namespace POS.Application.Services;

public interface IPrecioService
{
    Task<PrecioResueltoDto> ResolverPrecio(Guid productoId, int sucursalId);
    Task<List<PrecioResueltoLoteItemDto>> ResolverPrecioLote(int sucursalId);
    Task<(bool valido, string? error)> ValidarPrecio(Guid productoId, int sucursalId, decimal precioSolicitado, string? nombreProducto = null);
}
