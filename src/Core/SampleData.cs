using Core.Domain;

namespace Core;

public static class SampleData
{
    public static IReadOnlyList<Product> Products() =>
    [
        Product.Create("P-001", "SKU-001", "Цемент М400 25кг", "шт", 120),
        Product.Create("P-002", "SKU-002", "Пісок будівельний", "т", 8),
        Product.Create("P-003", "SKU-003", "Цегла червона", "шт", 4200),
        Product.Create("P-004", "SKU-004", "Щебінь фракція 20-40", "т", 5),
        Product.Create("P-005", "SKU-005", "Гіпсокартон 12.5мм", "шт", 85),
        Product.Create("P-006", "SKU-006", "Профіль CD-60 3м", "шт", 150),
        Product.Create("P-007", "SKU-007", "Шпаклівка фінішна 20кг", "шт", 40),
        Product.Create("P-008", "SKU-008", "Ґрунтовка глибока 10л", "шт", 30),
        Product.Create("P-009", "SKU-009", "Фарба акрилова 14кг", "шт", 9),
        Product.Create("P-010", "SKU-010", "Клей для плитки 25кг", "шт", 65),
        Product.Create("P-011", "SKU-011", "Плитка керамічна 30x30", "м2", 110),
        Product.Create("P-012", "SKU-012", "Утеплювач мінвата 50мм", "шт", 45),
        Product.Create("P-013", "SKU-013", "Піна монтажна 750мл", "шт", 90),
        Product.Create("P-014", "SKU-014", "Герметик силіконовий", "шт", 70),
        Product.Create("P-015", "SKU-015", "Саморізи по металу", "шт", 200),
        Product.Create("P-016", "SKU-016", "Кабель ВВГнг 3x2.5", "м", 500),
        Product.Create("P-017", "SKU-017", "Кабель UTP cat6", "м", 70)
    ];
}
