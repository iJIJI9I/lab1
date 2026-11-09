using Core.Reports;
using Xunit;

namespace Core.Tests;

public sealed class ReportServiceTests
{
    [Theory] // 3) звіт на кількох наборах вхідних даних
    [InlineData(10, 2)]
    [InlineData(1, 0)]
    public void LowStock_CountsOnlyBelowThreshold(int threshold, int expected)
    {
        var result = ReportService.LowStock(TestData.Items(), threshold);
        Assert.Equal(expected, result.Count);
    }

    [Fact] // 4) детермінований топ-N з тижня 7
    public void TopByQuantity_IsDeterministic()
    {
        var result = ReportService.TopByQuantity(TestData.Items(), 2);
        Assert.Equal(new[] { "SKU-BIG", "SKU-MID" }, result.Select(l => l.Sku));
    }

    [Fact]
    public void TopByQuantity_DeterministicOrderWhenQuantitiesAreEqual()
    {
        var tiedItems = new[]
        {
            Domain.Product.Create("ID-2", "SKU-B", "Товар B", "шт", 50),
            Domain.Product.Create("ID-1", "SKU-A", "Товар A", "шт", 50)
        };

        var result = ReportService.TopByQuantity(tiedItems, 2);

        // Має бути детерміновано впорядковано за SKU ("SKU-A", потім "SKU-B")
        Assert.Equal(new[] { "SKU-A", "SKU-B" }, result.Select(l => l.Sku));
    }
}
