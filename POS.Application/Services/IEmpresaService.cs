using POS.Application.DTOs;

namespace POS.Application.Services;

public interface IEmpresaService
{
    Task<List<EmpresaDto>> ObtenerTodasAsync();
    Task<EmpresaDto?> ObtenerPorIdAsync(int id);
    Task<(EmpresaDto? result, string? error)> CrearAsync(CrearEmpresaDto dto);
    Task<(EmpresaDto? result, string? error)> ActualizarAsync(int id, ActualizarEmpresaDto dto);
}
