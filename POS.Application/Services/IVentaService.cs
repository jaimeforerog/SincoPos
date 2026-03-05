using POS.Application.DTOs;

namespace POS.Application.Services;

public interface IVentaService
{
    Task<(VentaDto? venta, string? error)> CrearVentaAsync(CrearVentaDto dto);
    Task<(bool success, string? error)> AnularVentaAsync(int id, string? motivo);
    Task<(DevolucionVentaDto? devolucion, string? error)> CrearDevolucionParcialAsync(
        int ventaId, CrearDevolucionParcialDto dto, string? emailUsuario);
}
