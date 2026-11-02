namespace Core.Reports;

public static class EnumerableReportExtensions
{
    public static IEnumerable<T> TopBy<T>(
        this IEnumerable<T> source,
        Func<T, int> keySelector,
        int count,
        Func<T, string> tieBreaker,
        Func<T, string> uniqueKeySelector)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(keySelector);
        ArgumentNullException.ThrowIfNull(tieBreaker);
        ArgumentNullException.ThrowIfNull(uniqueKeySelector);

        return source
            .OrderByDescending(keySelector)
            .ThenBy(tieBreaker, StringComparer.Ordinal)
            .ThenBy(uniqueKeySelector, StringComparer.Ordinal)
            .Take(Math.Max(count, 0));
    }
}
