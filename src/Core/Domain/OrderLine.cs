namespace Core.Domain;

/// <summary>
/// Рядок замовлення — незмінний об'єкт-значення (value object).
/// Не є агрегатом, тому конструктор пакетний (internal).
/// </summary>
public sealed class OrderLine
{
    public string  ProductId { get; }
    public string  Name      { get; }
    public decimal Price     { get; }
    public int     Quantity  { get; }

    /// <summary>Загальна вартість рядка.</summary>
    public decimal LineTotal => Price * Quantity;

    // Internal: лише Order.AddLine може створити рядок.
    internal OrderLine(string productId, string name, decimal price, int quantity)
    {
        ProductId = productId;
        Name      = name;
        Price     = price;
        Quantity  = quantity;
    }

    public override string ToString() =>
        $"{ProductId} «{Name}» × {Quantity} @ {Price:C2} = {LineTotal:C2}";
}

