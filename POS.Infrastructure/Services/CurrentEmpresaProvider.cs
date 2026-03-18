using POS.Application.Services;

namespace POS.Infrastructure.Services;

/// <summary>
/// Implementación scoped — un valor por request HTTP.
/// Lo llena EmpresaMiddleware al inicio del pipeline.
/// </summary>
public class CurrentEmpresaProvider : ICurrentEmpresaProvider
{
    public int? EmpresaId { get; set; }
}
