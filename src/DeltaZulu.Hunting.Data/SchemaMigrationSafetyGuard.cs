namespace DeltaZulu.Hunting.Data;

/// <summary>
/// Enforces conservative schema migration safety based on provenance drift assessments.
/// This component does not apply schema by itself; it validates whether apply may proceed.
/// </summary>
public sealed class SchemaMigrationSafetyGuard
{
    public SchemaMigrationSafetyReport Evaluate(
        IEnumerable<SchemaMigrationSafetyAssessment> assessments,
        SchemaMigrationSafetyPolicy policy = SchemaMigrationSafetyPolicy.BlockUnsafe)
    {
        ArgumentNullException.ThrowIfNull(assessments);

        var assessmentList = assessments
            .OrderBy(static assessment => assessment.ObjectName, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var unsafeAssessments = assessmentList
            .Where(static assessment => assessment.Safety == SchemaMigrationSafety.Unsafe)
            .ToArray();

        var shouldBlock = policy == SchemaMigrationSafetyPolicy.BlockUnsafe && unsafeAssessments.Length > 0;

        return new SchemaMigrationSafetyReport(
            ShouldBlock: shouldBlock,
            Policy: policy,
            Assessments: assessmentList,
            UnsafeAssessments: unsafeAssessments,
            Message: BuildMessage(policy, shouldBlock, unsafeAssessments));
    }

    public void ThrowIfBlocked(
        IEnumerable<SchemaMigrationSafetyAssessment> assessments,
        SchemaMigrationSafetyPolicy policy = SchemaMigrationSafetyPolicy.BlockUnsafe)
    {
        var report = Evaluate(assessments, policy);
        if (report.ShouldBlock)
        {
            throw new SchemaMigrationSafetyException(report);
        }
    }

    private static string BuildMessage(
        SchemaMigrationSafetyPolicy policy,
        bool shouldBlock,
        IReadOnlyList<SchemaMigrationSafetyAssessment> unsafeAssessments)
    {
        if (unsafeAssessments.Count == 0)
        {
            return "Schema migration safety check passed.";
        }

        if (!shouldBlock)
        {
            return $"Schema migration safety check found {unsafeAssessments.Count} unsafe change(s), but policy {policy} allows continuation.";
        }

        var names = string.Join(", ", unsafeAssessments.Select(static assessment => assessment.ObjectName));
        return $"Schema migration safety check blocked {unsafeAssessments.Count} unsafe change(s): {names}.";
    }
}

/// <summary>
/// Policy for applying schema after unsafe provenance drift is found.
/// </summary>
public enum SchemaMigrationSafetyPolicy
{
    /// <summary>
    /// Block unsafe drift. This is the default Phase 1B behavior.
    /// </summary>
    BlockUnsafe,

    /// <summary>
    /// Report unsafe drift but allow continuation. Intended for explicit reset/dev workflows only.
    /// </summary>
    AllowUnsafe
}

public sealed record SchemaMigrationSafetyReport(
    bool ShouldBlock,
    SchemaMigrationSafetyPolicy Policy,
    IReadOnlyList<SchemaMigrationSafetyAssessment> Assessments,
    IReadOnlyList<SchemaMigrationSafetyAssessment> UnsafeAssessments,
    string Message);

public sealed class SchemaMigrationSafetyException : InvalidOperationException
{
    public SchemaMigrationSafetyException(SchemaMigrationSafetyReport report)
        : base(report.Message)
    {
        Report = report;
    }

    public SchemaMigrationSafetyReport Report { get; }
}
