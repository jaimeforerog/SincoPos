using POS.Application.DTOs;

namespace POS.Application.Services;

public interface ICompraService
{
    Task<(OrdenCompraDto? orden, string? error)> CrearOrdenAsync(CrearOrdenCompraDto dto);
    Task<(bool success, string? error)> AprobarOrdenAsync(int id, AprobarOrdenCompraDto? dto, string? emailUsuario);
    Task<(bool success, string? error)> RechazarOrdenAsync(int id, RechazarOrdenCompraDto dto);
    Task<(bool success, string? error)> RecibirOrdenAsync(int id, RecibirOrdenCompraDto dto, string? emailUsuario);
    Task<(bool success, string? error)> CancelarOrdenAsync(int id, CancelarOrdenCompraDto dto);
}
