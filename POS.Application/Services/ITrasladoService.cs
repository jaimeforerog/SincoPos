using POS.Application.DTOs;

namespace POS.Application.Services;

public interface ITrasladoService
{
    Task<(object? resultado, string? error)> CrearTrasladoAsync(CrearTrasladoDto dto);
    Task<(bool success, string? error)> EnviarTrasladoAsync(int id);
    Task<(bool success, string? error)> RecibirTrasladoAsync(int id, RecibirTrasladoDto dto, string? emailUsuario);
    Task<(bool success, string? error)> RechazarTrasladoAsync(int id, RechazarTrasladoDto dto);
    Task<(bool success, string? error)> CancelarTrasladoAsync(int id, CancelarTrasladoDto dto);
}
