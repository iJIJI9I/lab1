namespace Core.Reports;

public sealed record DeferredExecutionSummary(int BeforeAddition, int AfterAddition, int SnapshotCount);
