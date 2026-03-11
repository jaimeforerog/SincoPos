namespace POS.Application.DTOs;

/// <summary>
/// DTO genérico para resultados paginados.
/// </summary>
/// <typeparam name="T">Tipo de los elementos en la lista.</typeparam>
public record PaginatedResult<T>(
    List<T> Items,
    int TotalCount,
    int PageNumber,
    int PageSize,
    int TotalPages
);
