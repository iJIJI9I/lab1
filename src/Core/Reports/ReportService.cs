using System.Diagnostics;
using Core.Domain;

namespace Core.Reports;

public static class ReportService
{
    public static IReadOnlyList<StockLine> LowStock(IEnumerable<Product> source, int threshold)
    {
        ArgumentNullException.ThrowIfNull(source);

        return source
            .Where(product => product.Quantity < threshold)
            .OrderBy(product => product.Quantity)
            .ThenBy(product => product.Sku, StringComparer.Ordinal)
            .ThenBy(product => product.Id, StringComparer.Ordinal)
            .Select(ToStockLine)
            .ToList();
    }

    public static IReadOnlyList<StockLine> TopByQuantity(IEnumerable<Product> source, int count)
    {
        ArgumentNullException.ThrowIfNull(source);

        return source
            .TopBy(product => product.Quantity, count, product => product.Sku, product => product.Id)
            .Select(ToStockLine)
            .ToList();
    }

    public static IReadOnlyList<StockLine> TopByUnit(
        IEnumerable<Product> source,
        string unit,
        int count)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentException.ThrowIfNullOrWhiteSpace(unit);

        return source
            .Where(product => string.Equals(product.Unit, unit, StringComparison.OrdinalIgnoreCase))
            .TopBy(product => product.Quantity, count, product => product.Sku, product => product.Id)
            .Select(ToStockLine)
            .ToList();
    }

    public static IReadOnlyList<GroupSummary> ByUnit(IEnumerable<Product> source)
    {
        ArgumentNullException.ThrowIfNull(source);

        return source
            .GroupBy(product => product.Unit, StringComparer.OrdinalIgnoreCase)
            .Select(group => new GroupSummary(
                group.Key,
                group.Count(),
                group.Sum(product => product.Quantity)))
            .OrderByDescending(summary => summary.TotalQuantity)
            .ThenBy(summary => summary.Key, StringComparer.OrdinalIgnoreCase)
            .ThenBy(summary => summary.Key, StringComparer.Ordinal)
            .ToList();
    }

    public static IReadOnlyList<GroupSummary> ByUnitUsingQuerySyntax(IEnumerable<Product> source)
    {
        ArgumentNullException.ThrowIfNull(source);

        var query =
            from product in source
            group product by product.Unit.ToUpperInvariant() into unitGroup
            let totalQuantity = unitGroup.Sum(product => product.Quantity)
            orderby totalQuantity descending, unitGroup.Key
            select new GroupSummary(
                unitGroup.First().Unit,
                unitGroup.Count(),
                totalQuantity);

        return query.ToList();
    }

    public static (int Items, int Total, double Average, int Max) Totals(IEnumerable<Product> source)
    {
        ArgumentNullException.ThrowIfNull(source);
        var products = source.ToList();

        return products.Count == 0
            ? (0, 0, 0d, 0)
            : (
                products.Count,
                products.Sum(product => product.Quantity),
                products.Average(product => product.Quantity),
                products.Max(product => product.Quantity));
    }

    public static IReadOnlyList<MovementLine> JoinMovements(
        IEnumerable<Product> products,
        IEnumerable<StockMovement> movements)
    {
        ArgumentNullException.ThrowIfNull(products);
        ArgumentNullException.ThrowIfNull(movements);

        return products
            .Join(
                movements,
                product => product.Id,
                movement => movement.ProductId,
                (product, movement) =>
                    new MovementLine(product.Sku, product.Name, movement.Delta, product.Unit))
            .OrderBy(movement => movement.Sku, StringComparer.Ordinal)
            .ThenBy(movement => movement.Name, StringComparer.Ordinal)
            .ToList();
    }

    public static DeferredExecutionSummary DemonstrateDeferredExecution(
        IEnumerable<Product> source,
        Product addedProduct,
        int threshold)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(addedProduct);

        var products = source.ToList();
        var query = products.Where(product => product.Quantity < threshold);
        int beforeAddition = query.Count();

        products.Add(addedProduct);
        int afterAddition = query.Count();

        var snapshot = query.ToList();
        products.Clear();

        return new DeferredExecutionSummary(beforeAddition, afterAddition, snapshot.Count);
    }

    public static ReportBenchmark CompareMaterialization(
        IEnumerable<Product> source,
        int threshold)
    {
        ArgumentNullException.ThrowIfNull(source);
        var products = source.ToList();

        _ = MeasureWithoutMaterialization(products, threshold);
        _ = MeasureWithMaterialization(products, threshold);

        var stopwatch = Stopwatch.StartNew();
        var directResult = MeasureWithoutMaterialization(products, threshold);
        stopwatch.Stop();
        double withoutMaterializationMilliseconds = stopwatch.Elapsed.TotalMilliseconds;

        stopwatch.Restart();
        var materializedResult = MeasureWithMaterialization(products, threshold);
        stopwatch.Stop();

        if (directResult != materializedResult)
            throw new InvalidOperationException("Варіанти звіту повернули різні результати.");

        return new ReportBenchmark(
            directResult.Count,
            directResult.Total,
            withoutMaterializationMilliseconds,
            stopwatch.Elapsed.TotalMilliseconds);
    }

    private static (int Count, int Total) MeasureWithoutMaterialization(
        IEnumerable<Product> source,
        int threshold)
    {
        var query = source
            .Where(product => product.Quantity < threshold)
            .Select(product => product.Quantity);

        return (query.Count(), query.Sum());
    }

    private static (int Count, int Total) MeasureWithMaterialization(
        IEnumerable<Product> source,
        int threshold)
    {
        var filtered = source
            .Where(product => product.Quantity < threshold)
            .ToList();

        return (filtered.Count, filtered.Sum(product => product.Quantity));
    }

    private static StockLine ToStockLine(Product product) =>
        new(product.Sku, product.Name, product.Quantity, product.Unit);
}
