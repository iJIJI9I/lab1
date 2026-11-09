using Core.Domain;

namespace Core.Tests;

public static class TestData
{
    public static IReadOnlyList<Product> Items() =>
    [
        Product.Create("T-01", "SKU-BIG", "Цемент М400", "шт", 100),
        Product.Create("T-02", "SKU-MID", "Пісок річковий", "т", 50),
        Product.Create("T-03", "SKU-LOW1", "Фарба біла", "л", 5),
        Product.Create("T-04", "SKU-LOW2", "Скоби металеві", "шт", 2)
    ];
}
