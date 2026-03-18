namespace POS.Application.Services;

/// <summary>
/// Provee el EmpresaId del usuario autenticado en el request actual.
/// Null = sin contexto de empresa (background services, tests, seed).
/// En ese caso los filtros globales se omiten para no romper compatibilidad.
/// </summary>
public interface ICurrentEmpresaProvider
{
    int? EmpresaId { get; set; }
}
