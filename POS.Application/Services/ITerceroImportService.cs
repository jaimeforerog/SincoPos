using POS.Application.DTOs;

namespace POS.Application.Services;

public interface ITerceroImportService
{
    Task<ResultadoImportacionTercerosDto> ImportarDesdeExcelAsync(Stream stream);
    byte[] GenerarPlantillaExcel();
}
