namespace Hunting.Data;

/// <summary>
/// Classifies provenance drift into conservative migration-safety outcomes.
/// This classifier does not block schema application; enforcement belongs to a later commit.
/// </summary>
public sealed class SchemaMigrationSafetyClassifier
{
    public IReadOnlyList<SchemaMigrationSafetyAssessment> Classify(
        IEnumerable<SchemaProvenanceDrift> drift)
    {
        ArgumentNullException.ThrowIfNull(drift);

        return drift
            .Select(ClassifyOne)
            .OrderBy(static assessment => assessment.ObjectName, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static SchemaMigrationSafetyAssessment ClassifyOne(SchemaProvenanceDrift drift)
    {
        return drift.Status switch
        {
            SchemaProvenanceDriftStatus.Unchanged => new(
                drift.ObjectName,
                drift.ObjectKind,
                drift.Status,
                SchemaMigrationSafety.Safe,
                RequiresExplicitApproval: false,
                "Schema object is unchanged."),

            SchemaProvenanceDriftStatus.NewObject => new(
                drift.ObjectName,
                drift.ObjectKind,
                drift.Status,
                SchemaMigrationSafety.Safe,
                RequiresExplicitApproval: false,
                "Schema object is new. Creating a new schema object is safe in Phase 1B."),

            SchemaProvenanceDriftStatus.ChangedObject => new(
                drift.ObjectName,
                drift.ObjectKind,
                drift.Status,
                SchemaMigrationSafety.Unsafe,
                RequiresExplicitApproval: true,
                "Schema object hash changed. Phase 1B cannot yet prove whether this is additive, so it is unsafe by default."),

            SchemaProvenanceDriftStatus.MissingObject => new(
                drift.ObjectName,
                drift.ObjectKind,
                drift.Status,
                SchemaMigrationSafety.Unsafe,
                RequiresExplicitApproval: true,
                "Schema object is recorded but no longer expected. Removal is unsafe by default."),

            _ => throw new ArgumentOutOfRangeException(nameof(drift), drift.Status, "Unknown schema drift status")
        };
    }
}

public sealed record SchemaMigrationSafetyAssessment(
    string ObjectName,
    string ObjectKind,
    SchemaProvenanceDriftStatus DriftStatus,
    SchemaMigrationSafety Safety,
    bool RequiresExplicitApproval,
    string Message);

public enum SchemaMigrationSafety
{
    Safe,
    Unsafe
}