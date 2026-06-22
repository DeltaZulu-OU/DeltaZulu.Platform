namespace DeltaZulu.Platform.Domain.Analytics.Nrt;

public sealed record NrtCompilationResult
{
    public bool Success { get; init; }
    public string? SelectSql { get; init; }
    public string? MaterializedViewDdl { get; init; }
    public IReadOnlyList<string> Errors { get; init; } = [];

    public static NrtCompilationResult Ok(string selectSql, string mvDdl) =>
        new() { Success = true, SelectSql = selectSql, MaterializedViewDdl = mvDdl };

    public static NrtCompilationResult Fail(IReadOnlyList<string> errors) =>
        new() { Success = false, Errors = errors };
}
