using POS.Application.DTOs;

namespace POS.Application.Services;

public interface IProductoAnticipacionService
{
    /// <summary>
    /// Retorna los productos más frecuentes del cajero (externalUserId)
    /// ordenados por cantidad vendida descendente.
    /// Fallback: retorna [] si el usuario no tiene historial.
    /// </summary>
    Task<IReadOnlyList<ProductoDto>> ObtenerProductosAnticipados(
        string externalUserId,
        int    limite = 20);
}
