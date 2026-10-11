using Core.Dto;
using Core.Domain;

namespace Core.Import;

/// <summary>
/// Результат перетворення імпортованих DTO на доменні сутності.
/// Успішні сутності + рядки, що не пройшли інваріанти.
/// </summary>
public sealed record DomainImportResult(
    IReadOnlyList<Product> Products,
    IReadOnlyList<string>  RejectedByDomain);

/// <summary>
/// Зв'язує тижень 3 (ImportResult) з тижнем 4 (Domain):
/// приймає ImportResult і намагається створити Product для кожного DTO.
/// Записи, що порушують доменні інваріанти, потрапляють у RejectedByDomain.
/// </summary>
public static class ProductDomainMapper
{
    /// <summary>
    /// Перетворює ImportResult&lt;ProductDto&gt; на DomainImportResult.
    /// DTO, що не пройшли інваріанти Create, фіксуються у RejectedByDomain.
    /// </summary>
    public static DomainImportResult ToDomain(ImportResult<ProductDto> importResult)
    {
        var products = new List<Product>();
        var rejected = new List<string>();

        // Спочатку ті, що вже були відкинуті імпортером (неправильний формат CSV)
        foreach (string csvError in importResult.Errors)
            rejected.Add($"[CSV] {csvError}");

        // Намагаємося перетворити кожен DTO на доменну сутність
        foreach (ProductDto dto in importResult.Items)
        {
            try
            {
                products.Add(Product.FromDto(dto));
            }
            catch (Exception ex)
            {
                // Порушення доменного інваріанту: логуємо і пропускаємо
                rejected.Add($"[Domain] {dto.Id}/{dto.Sku}: {ex.GetType().Name} — {ex.Message}");
            }
        }

        return new DomainImportResult(products, rejected);
    }
}

