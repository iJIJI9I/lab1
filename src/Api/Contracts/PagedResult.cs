namespace Api.Contracts;

/// <summary>
/// Результат пагінації списку для додаткового завдання 1.
/// GET /api/products?page=1&pageSize=20&search=...
/// </summary>
public sealed record PagedResult<T>(
    IReadOnlyList<T> Items,
    int Total,
    int Page,
    int PageSize,
    int TotalPages);
