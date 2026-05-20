using POS.Application.DTOs;

namespace POS.Application.Services;

public interface IEmpresaService
{
    Task<List<EmpresaDto>> ObtenerTodasAsync();
    Task<EmpresaDto?> ObtenerPorIdAsync(int id);
    Task<(EmpresaDto? result, string? error)> CrearAsync(CrearEmpresaDto dto);
    Task<(EmpresaDto? result, string? error)> ActualizarAsync(int id, ActualizarEmpresaDto dto);

    /// <summary>
    /// Recorre todas las empresas sin WorkOsOrganizationId y las crea en WorkOS.
    /// Retorna cuántas fueron sincronizadas y cuántas fallaron.
    /// </summary>
    Task<(int Sincronizadas, int Fallidas)> SincronizarOrganizacionesAsync();
}
