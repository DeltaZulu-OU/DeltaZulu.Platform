namespace DeltaZulu.Platform.Domain.Analytics.Scheduled;

public sealed record ScheduledCompilationResult
{
    public bool Success { get; init; }
    public string? SelectSql { get; init; }
    public string? ScheduledTaskDdl { get; init; }
    public IReadOnlyList<string> Errors { get; init; } = [];

    public static ScheduledCompilationResult Ok(string selectSql, string taskDdl) =>
        new() { Success = true, SelectSql = selectSql, ScheduledTaskDdl = taskDdl };

    public static ScheduledCompilationResult Fail(IReadOnlyList<string> errors) =>
        new() { Success = false, Errors = errors };
}
