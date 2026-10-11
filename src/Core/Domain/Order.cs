using Core.Dto;

namespace Core.Domain;

/// <summary>
/// Агрегат «Замовлення» з захищеною колекцією рядків.
/// Інваріанти:
///   1. Id замовлення не може бути порожнім.
///   2. CustomerId не може бути порожнім.
///   3. Кількість товару в рядку має бути > 0.
///   4. Ціна товару в рядку має бути ≥ 0.
///   5. Не можна додавати рядки до підтвердженого або скасованого замовлення.
///   6. Не можна підтверджувати порожнє замовлення.
///   7. Допустимі переходи станів: Draft → Confirmed, Draft → Cancelled.
/// </summary>
public sealed class Order
{
    // Захищена колекція: зовні List<T> не доступний, лише IReadOnlyList<T>.
    private readonly List<OrderLine> _lines = [];

    public string      Id         { get; }
    public string      CustomerId { get; }

    // Стан зберігається з private set — читається звідусіль, пишеться лише класом.
    public OrderStatus Status     { get; private set; }

    /// <summary>Рядки замовлення — лише для читання, AsReadOnly() унеможливлює Cast назад до List.</summary>
    public IReadOnlyList<OrderLine> Lines => _lines.AsReadOnly();

    /// <summary>Загальна сума замовлення.</summary>
    public decimal Total => _lines.Sum(l => l.LineTotal);

    // Приватний конструктор — доступ лише через Create.
    private Order(string id, string customerId)
    {
        Id         = id;
        CustomerId = customerId;
        Status     = OrderStatus.Draft;
    }

    // ── Фабричний метод ───────────────────────────────────────────────────────

    /// <summary>
    /// Відкриває нове замовлення у стані Draft.
    /// </summary>
    /// <exception cref="ArgumentException">Якщо id або customerId порожні.</exception>
    public static Order Create(string id, string customerId)
    {
        // Інваріант 1
        if (string.IsNullOrWhiteSpace(id))
            throw new ArgumentException("Ідентифікатор замовлення обов'язковий", nameof(id));

        // Інваріант 2
        if (string.IsNullOrWhiteSpace(customerId))
            throw new ArgumentException("Ідентифікатор клієнта обов'язковий", nameof(customerId));

        return new Order(id.Trim(), customerId.Trim());
    }

    // ── Методи зміни стану ────────────────────────────────────────────────────

    /// <summary>
    /// Додає рядок до замовлення.
    /// </summary>
    /// <exception cref="InvalidOperationException">Якщо замовлення вже не Draft.</exception>
    /// <exception cref="ArgumentException">Якщо productId або name порожні.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Якщо quantity ≤ 0 або price &lt; 0.</exception>
    public void AddLine(string productId, string name, decimal price, int quantity)
    {
        // Інваріант 5: переходи станів через switch expression
        EnsureInState(OrderStatus.Draft, nameof(AddLine));

        if (string.IsNullOrWhiteSpace(productId))
            throw new ArgumentException("Ідентифікатор товару обов'язковий", nameof(productId));

        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Назва товару обов'язкова", nameof(name));

        // Інваріант 3
        if (quantity <= 0)
            throw new ArgumentOutOfRangeException(nameof(quantity), quantity,
                "Кількість у рядку замовлення має бути більшою за нуль");

        // Інваріант 4
        if (price < 0)
            throw new ArgumentOutOfRangeException(nameof(price), price,
                "Ціна товару не може бути від'ємною");

        // Усі перевірки пройдено — додаємо рядок
        _lines.Add(new OrderLine(productId.Trim(), name.Trim(), price, quantity));
    }

    /// <summary>
    /// Підтверджує замовлення (Draft → Confirmed).
    /// </summary>
    /// <exception cref="InvalidOperationException">Якщо замовлення не Draft або порожнє.</exception>
    public void Confirm()
    {
        // Інваріант 5
        EnsureInState(OrderStatus.Draft, nameof(Confirm));

        // Інваріант 6
        if (_lines.Count == 0)
            throw new InvalidOperationException(
                $"Замовлення {Id} порожнє — підтвердження неможливе");

        Status = OrderStatus.Confirmed;
    }

    /// <summary>
    /// Скасовує замовлення (Draft → Cancelled).
    /// </summary>
    /// <exception cref="InvalidOperationException">Якщо замовлення не Draft.</exception>
    public void Cancel()
    {
        EnsureInState(OrderStatus.Draft, nameof(Cancel));
        Status = OrderStatus.Cancelled;
    }

    // ── Маппінг DTO ↔ Сутність ────────────────────────────────────────────────

    /// <summary>Серіалізує замовлення у DTO для збереження (тиждень 5).</summary>
    public OrderDto ToDto() =>
        new(Id,
            CustomerId,
            Status.ToString(),
            _lines.Select(l => new OrderLineDto(l.ProductId, l.Name, l.Price, l.Quantity))
                  .ToList()
                  .AsReadOnly());

    /// <summary>Відновлює замовлення з DTO, проходячи перевірки Create та AddLine.</summary>
    public static Order FromDto(OrderDto dto)
    {
        Order order = Create(dto.Id, dto.CustomerId);

        foreach (OrderLineDto line in dto.Lines)
            order.AddLine(line.ProductId, line.Name, line.Price, line.Quantity);

        // Відновлення стану через явний перехід
        if (Enum.TryParse<OrderStatus>(dto.Status, out OrderStatus status))
        {
            if (status == OrderStatus.Confirmed) order.Confirm();
            else if (status == OrderStatus.Cancelled) order.Cancel();
        }

        return order;
    }

    public override string ToString() =>
        $"Order {Id} [{Status}] Customer={CustomerId} Lines={_lines.Count} Total={Total:C2}";

    // ── Приватні допоміжні методи ─────────────────────────────────────────────

    /// <summary>
    /// Перевіряє, що поточний стан відповідає очікуваному.
    /// Використовує switch expression для перевірки допустимих переходів (інваріант 7).
    /// </summary>
    private void EnsureInState(OrderStatus expected, string operation)
    {
        string reason = Status switch
        {
            OrderStatus.Confirmed => $"вже підтверджене",
            OrderStatus.Cancelled => $"скасоване",
            _                     => null!
        };

        if (Status != expected)
            throw new InvalidOperationException(
                $"Замовлення {Id} {reason} — операція '{operation}' неможлива");
    }
}

