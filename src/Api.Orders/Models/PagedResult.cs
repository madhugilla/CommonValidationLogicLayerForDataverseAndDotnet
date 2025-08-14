namespace Api.Orders.Models;

public sealed record PagedResult<T>(
    int PageNumber,
    int PageSize,
    int TotalCount,
    IReadOnlyList<T> Items
)
{
    public static PagedResult<T> Empty(int pageNumber = 1, int pageSize = 0) => new(pageNumber, pageSize, 0, Array.Empty<T>());
}
