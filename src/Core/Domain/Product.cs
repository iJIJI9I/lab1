using Core.Dto;

namespace Core.Domain;

/// <summary>
/// Доменна сутність «Товар на складі».
/// Інваріанти:
///   1. Id не може бути порожнім.
///   2. SKU не може бути порожнім.
///   3. Назва не може бути порожньою.
///   4. Початковий залишок не може бути від'ємним.
///   5. Кількість при приході має бути > 0.
///   6. Кількість при видачі має бути > 0.
///   7. Не можна видати більше товару, ніж є в залишку.
/// </summary>
public sealed class Product
{
    // Приватне поле залишку — змінюється лише через RegisterArrival / Issue.
    private int _quantity;

    // Автовластивості лише для читання: { get; } — присвоєння тільки в конструкторі.
    public string Id   { get; }
    public string Sku  { get; }
    public string Name { get; }
    public string Unit { get; }

    // Властивість-вираз: повертає приватне поле, зовнішній set неможливий.
    public int Quantity => _quantity;

    // Приватний конструктор — new Product(...) у зовнішньому коді не скомпілюється.
    private Product(string id, string sku, string name, string unit, int quantity)
    {
        Id        = id;
        Sku       = sku;
        Name      = name;
        Unit      = unit;
        _quantity = quantity;
    }

    // ── Фабричний метод — єдина відкрита точка створення ──────────────────────
    /// <summary>
    /// Створює новий товар після перевірки всіх інваріантів.
    /// </summary>
    /// <exception cref="ArgumentException">Якщо id, sku або name порожні.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Якщо quantity менше нуля.</exception>
    public static Product Create(string id, string sku, string name, string unit, int quantity)
    {
        // Інваріант 1: Id обов'язковий
        if (string.IsNullOrWhiteSpace(id))
            throw new ArgumentException("Ідентифікатор обов'язковий", nameof(id));

        // Інваріант 2: SKU обов'язковий
        if (string.IsNullOrWhiteSpace(sku))
            throw new ArgumentException("SKU не може бути порожнім", nameof(sku));

        // Інваріант 3: Назва обов'язкова
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Назва не може бути порожньою", nameof(name));

        // Інваріант 4: Початковий залишок не від'ємний
        if (quantity < 0)
            throw new ArgumentOutOfRangeException(nameof(quantity), quantity,
                "Початковий залишок не може бути від'ємним");

        // Нормалізація — після перевірок, перед збереженням
        return new Product(
            id.Trim(),
            sku.Trim().ToUpperInvariant(),
            name.Trim(),
            string.IsNullOrWhiteSpace(unit) ? "шт" : unit.Trim(),
            quantity);
    }

    // ── Методи зміни стану ────────────────────────────────────────────────────

    /// <summary>
    /// Реєструє прихід товару на склад.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Якщо amount ≤ 0.</exception>
    public void RegisterArrival(int amount)
    {
        // Інваріант 5: кількість приходу > 0
        if (amount <= 0)
            throw new ArgumentOutOfRangeException(nameof(amount), amount,
                "Кількість приходу має бути більшою за нуль");

        // Спочатку всі перевірки, потім зміна стану
        _quantity += amount;
    }

    /// <summary>
    /// Видає товар зі складу.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Якщо amount ≤ 0.</exception>
    /// <exception cref="InvalidOperationException">Якщо amount більше залишку.</exception>
    public void Issue(int amount)
    {
        // Інваріант 6: кількість видачі > 0
        if (amount <= 0)
            throw new ArgumentOutOfRangeException(nameof(amount), amount,
                "Кількість видачі має бути більшою за нуль");

        // Інваріант 7: не видавати більше, ніж є
        if (amount > _quantity)
            throw new InvalidOperationException(
                $"Не можна видати {amount}: залишок {Sku} = {_quantity}");

        // Перевірки пройдено — змінюємо стан
        _quantity -= amount;
    }

    // ── Маппінг DTO ↔ Сутність ────────────────────────────────────────────────

    /// <summary>
    /// Перетворює сутність у DTO для передачі / збереження (тиждень 5).
    /// </summary>
    public ProductDto ToDto() => new(Id, Sku, Name, Unit, Quantity);

    /// <summary>
    /// Відновлює сутність із DTO, проходячи ті самі інваріанти Create.
    /// </summary>
    public static Product FromDto(ProductDto dto) =>
        Create(dto.Id, dto.Sku, dto.Name, dto.Unit, dto.Quantity);

    public override string ToString() =>
        $"{Id} [{Sku}] {Name} — {Quantity} {Unit}";
}

