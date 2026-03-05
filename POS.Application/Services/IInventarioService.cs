using POS.Application.DTOs;

namespace POS.Application.Services;

public interface IInventarioService
{
    Task<(object? resultado, string? error)> RegistrarEntradaAsync(EntradaInventarioDto dto, string? emailUsuario);
    Task<(object? resultado, string? error)> DevolucionProveedorAsync(DevolucionProveedorDto dto, string? emailUsuario);
    Task<(object? resultado, string? error)> AjustarInventarioAsync(AjusteInventarioDto dto, string? emailUsuario);
    Task<(bool success, string? error)> ActualizarStockMinimoAsync(Guid productoId, int sucursalId, decimal stockMinimo, string? emailUsuario);
}
