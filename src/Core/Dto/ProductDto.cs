namespace Core.Dto;

public record ProductDto(
    string Id,
    string Sku,
    string Name,
    string Unit,
    int Quantity,
    string? Note = null);
