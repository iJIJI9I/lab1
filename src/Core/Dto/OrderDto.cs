namespace Core.Dto;

/// <summary>
/// DTO рядка замовлення — лише дані, без бізнес-логіки.
/// </summary>
public record OrderLineDto(
    string  ProductId,
    string  Name,
    decimal Price,
    int     Quantity);

/// <summary>
/// DTO замовлення — лише дані, без бізнес-логіки.
/// </summary>
public record OrderDto(
    string                    Id,
    string                    CustomerId,
    string                    Status,
    IReadOnlyList<OrderLineDto> Lines);

