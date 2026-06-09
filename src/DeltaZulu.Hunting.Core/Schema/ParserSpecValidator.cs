namespace DeltaZulu.Hunting.Core.Schema;

/// <summary>
/// Validates parser specifications against target Golden contracts.
/// </summary>
public static class ParserSpecValidator
{
    public static IReadOnlyList<ParserSpecValidationIssue> ValidateAgainstTarget(
        ParserSpec spec,
        CanonicalViewDef target)
    {
        ArgumentNullException.ThrowIfNull(spec);
        ArgumentNullException.ThrowIfNull(target);

        var issues = new List<ParserSpecValidationIssue>();

        if (!spec.TargetContract.Equals(target.QualifiedName, StringComparison.OrdinalIgnoreCase) &&
            !spec.TargetContract.Equals(target.Name, StringComparison.OrdinalIgnoreCase))
        {
            issues.Add(new ParserSpecValidationIssue(
                ParserSpecValidationSeverity.Error,
                spec.Name,
                null,
                $"Parser target {spec.TargetContract} does not match canonical view {target.QualifiedName}."));
        }

        var suppliedColumns = spec.Projections
            .Select(static projection => projection.TargetColumn)
            .Concat(spec.IntentionalNulls.Select(static intentionalNull => intentionalNull.TargetColumn))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var column in target.Columns)
        {
            if (!suppliedColumns.Contains(column.Name))
            {
                issues.Add(new ParserSpecValidationIssue(
                    ParserSpecValidationSeverity.Error,
                    spec.Name,
                    column.Name,
                    $"Parser specification does not supply target column {column.Name}."));
            }
        }

        var targetColumns = target.Columns
            .Select(static column => column.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var projection in spec.Projections)
        {
            if (!targetColumns.Contains(projection.TargetColumn))
            {
                issues.Add(new ParserSpecValidationIssue(
                    ParserSpecValidationSeverity.Error,
                    spec.Name,
                    projection.TargetColumn,
                    $"Projection targets unknown column {projection.TargetColumn}."));
            }
        }

        foreach (var intentionalNull in spec.IntentionalNulls)
        {
            if (!targetColumns.Contains(intentionalNull.TargetColumn))
            {
                issues.Add(new ParserSpecValidationIssue(
                    ParserSpecValidationSeverity.Error,
                    spec.Name,
                    intentionalNull.TargetColumn,
                    $"Intentional null targets unknown column {intentionalNull.TargetColumn}."));
            }
        }

        if (spec.AdditionalFieldsPolicy == ParserAdditionalFieldsPolicy.PreserveRawLog &&
            !targetColumns.Contains("AdditionalFields"))
        {
            issues.Add(new ParserSpecValidationIssue(
                ParserSpecValidationSeverity.Warning,
                spec.Name,
                "AdditionalFields",
                "Parser preserves raw log fields, but target contract does not define AdditionalFields."));
        }

        return issues;
    }
}

public sealed record ParserSpecValidationIssue(
    ParserSpecValidationSeverity Severity,
    string ParserName,
    string? ColumnName,
    string Message);

public enum ParserSpecValidationSeverity
{
    Warning,
    Error
}
