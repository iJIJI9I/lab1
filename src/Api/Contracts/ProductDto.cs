namespace Api.Contracts;

/// <summary>
/// Вихідний DTO товару для HTTP API.
/// Відокремлює публічний контракт API від внутрішньої доменної сутності Product.
/// </summary>
public sealed record ProductDto(string Id, string Sku, string Name, string Unit, int Quantity);
