using FluentValidation;

namespace Api.Contracts;

/// <summary>
/// Вхідний DTO запиту створення або оновлення товару.
/// Доменна сутність захищена від прямого зв'язування з JSON-тілом запиту.
/// </summary>
public sealed record SaveProductRequest(string Sku, string Name, string Unit, int Quantity)
{
    /// <summary>
    /// Вбудована валідація згідно з вимогами завдання (сторінка 3).
    /// Повертає словник помилок у форматі ValidationProblemDetails (RFC 9457).
    /// </summary>
    public bool TryValidate(out Dictionary<string, string[]> errors)
    {
        var found = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(Sku))
            found[nameof(Sku)] = ["Артикул обов'язковий."];

        if (string.IsNullOrWhiteSpace(Name) || Name.Length > 200)
            found[nameof(Name)] = ["Назва обов'язкова, до 200 символів."];

        if (string.IsNullOrWhiteSpace(Unit) || Unit.Length > 20)
            found[nameof(Unit)] = ["Одиниця виміру обов'язкова, до 20 символів."];

        if (Quantity < 0)
            found[nameof(Quantity)] = ["Кількість не може бути від'ємною."];

        errors = found;
        return found.Count == 0;
    }
}

/// <summary>
/// Валідатор FluentValidation для додаткового завдання 2.
/// Викликається в конвеєрі Minimal API через EndpointFilter.
/// </summary>
public sealed class SaveProductRequestValidator : AbstractValidator<SaveProductRequest>
{
    public SaveProductRequestValidator()
    {
        RuleFor(x => x.Sku)
            .NotEmpty().WithMessage("Артикул обов'язковий.");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Назва обов'язкова, до 200 символів.")
            .MaximumLength(200).WithMessage("Назва обов'язкова, до 200 символів.");

        RuleFor(x => x.Unit)
            .NotEmpty().WithMessage("Одиниця виміру обов'язкова, до 20 символів.")
            .MaximumLength(20).WithMessage("Одиниця виміру обов'язкова, до 20 символів.");

        RuleFor(x => x.Quantity)
            .GreaterThanOrEqualTo(0).WithMessage("Кількість не може бути від'ємною.");
    }
}
