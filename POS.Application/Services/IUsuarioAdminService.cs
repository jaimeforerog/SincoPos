using POS.Application.DTOs;

namespace POS.Application.Services;

public interface IUsuarioAdminService
{
    Task<(CrearUsuarioResultDto? Result, string? Error)> CrearUsuarioAsync(CrearUsuarioDto dto, string creadorExternalId, string creadorRol);
    Task<(bool Success, string? Error)> ActualizarUsuarioAsync(int id, ActualizarUsuarioDto dto, string creadorRol);
    Task<(bool Success, string? Error)> CambiarRolAsync(int id, string nuevoRol, string creadorRol);
    Task<(string? TempPassword, string? Error)> ResetPasswordAsync(int id);
}
