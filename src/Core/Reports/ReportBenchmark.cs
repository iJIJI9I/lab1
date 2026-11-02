namespace Core.Reports;

public sealed record ReportBenchmark(
    int ResultCount,
    int TotalQuantity,
    double WithoutMaterializationMilliseconds,
    double WithMaterializationMilliseconds);
