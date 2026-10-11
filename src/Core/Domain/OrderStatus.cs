namespace Core.Domain;

/// <summary>
/// Стани замовлення. Перевірка допустимих переходів через switch expression.
/// </summary>
public enum OrderStatus
{
    /// <summary>Чернетка — рядки можна додавати, замовлення ще не підтверджено.</summary>
    Draft,

    /// <summary>Підтверджено — змін не приймає, готово до виконання.</summary>
    Confirmed,

    /// <summary>Скасовано — кінцевий стан, переходи неможливі.</summary>
    Cancelled
}

