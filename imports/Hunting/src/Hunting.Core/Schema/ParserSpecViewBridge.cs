namespace Hunting.Core.Schema;

/// <summary>
/// Bridge between Phase 1D parser specifications and the existing ParserViewDef model.
/// This bridge validates that a ParserSpec describes an existing parser view.
/// It deliberately does not regenerate mapping expressions yet.
/// </summary>
public static class ParserSpecViewBridge
{
    /// <summary>
    /// Returns the existing ParserViewDef after validating that the spec describes it.
    /// This is a compatibility bridge, not a spec-to-SQL generator.
    /// </summary>
    public static ParserViewDef ToExistingParserView(
        ParserSpec spec,
        ParserViewDef parserView,
        CanonicalViewDef target)
    {
        var issues = Validate(spec, parserView, target);
        var errors = issues
            .Where(static issue => issue.Severity == ParserSpecValidationSeverity.Error)
            .ToArray();

        if (errors.Length > 0)
        {
            throw new ParserSpecViewBridgeException(spec.QualifiedName, errors);
        }

        return parserView;
    }

    public static IReadOnlyList<ParserSpecViewBridgeIssue> Validate(
        ParserSpec spec,
        ParserViewDef parserView,
        CanonicalViewDef target)
    {
        ArgumentNullException.ThrowIfNull(spec);
        ArgumentNullException.ThrowIfNull(parserView);
        ArgumentNullException.ThrowIfNull(target);

        var issues = new List<ParserSpecViewBridgeIssue>();

        if (!spec.QualifiedName.Equals(parserView.QualifiedName, StringComparison.OrdinalIgnoreCase))
        {
            issues.Add(Error(
                spec,
                null,
                $"Parser spec {spec.QualifiedName} does not describe parser view {parserView.QualifiedName}."));
        }

        if (!spec.SourceObject.Equals(parserView.Mapping.SourceObject, StringComparison.OrdinalIgnoreCase))
        {
            issues.Add(Error(
                spec,
                null,
                $"Parser spec source object {spec.SourceObject} does not match parser view source object {parserView.Mapping.SourceObject}."));
        }

        if (!spec.SourceName.Equals(parserView.SourceName, StringComparison.Ordinal))
        {
            issues.Add(Error(
                spec,
                null,
                $"Parser spec source name {spec.SourceName} does not match parser view source name {parserView.SourceName}."));
        }

        if (!TargetMatches(spec.TargetContract, parserView.CanonicalTarget, target))
        {
            issues.Add(Error(
                spec,
                null,
                $"Parser spec target {spec.TargetContract} does not match parser view target {parserView.CanonicalTarget} / canonical view {target.QualifiedName}."));
        }

        ValidateColumns(spec, parserView, target, issues);
        ValidateProjectionMapping(spec, parserView, issues);
        ValidateAdditionalFieldsPolicy(spec, target, issues);

        return issues
            .OrderBy(static issue => issue.ParserName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(static issue => issue.ColumnName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(static issue => issue.Message, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static ParserSpecViewBridgeIssue Error(ParserSpec spec, string? columnName, string message) =>
        new(ParserSpecValidationSeverity.Error, spec.QualifiedName, columnName, message);

    private static bool TargetMatches(
        string specTarget,
        string parserTarget,
        CanonicalViewDef canonicalView) =>
        specTarget.Equals(parserTarget, StringComparison.OrdinalIgnoreCase) ||
        specTarget.Equals(canonicalView.Name, StringComparison.OrdinalIgnoreCase) ||
        specTarget.Equals(canonicalView.QualifiedName, StringComparison.OrdinalIgnoreCase);

    private static void ValidateAdditionalFieldsPolicy(
        ParserSpec spec,
        CanonicalViewDef target,
        List<ParserSpecViewBridgeIssue> issues)
    {
        var targetHasAdditionalFields = target.Columns.Any(static column =>
            column.Name.Equals("AdditionalFields", StringComparison.OrdinalIgnoreCase));

        var specSuppliesAdditionalFields =
            spec.Projections.Any(static projection => projection.TargetColumn.Equals("AdditionalFields", StringComparison.OrdinalIgnoreCase)) ||
            spec.IntentionalNulls.Any(static intentionalNull => intentionalNull.TargetColumn.Equals("AdditionalFields", StringComparison.OrdinalIgnoreCase));

        if (targetHasAdditionalFields && !specSuppliesAdditionalFields)
        {
            issues.Add(Error(
                spec,
                "AdditionalFields",
                "Target contract defines AdditionalFields, but parser spec does not supply it."));
        }

        if (spec.AdditionalFieldsPolicy == ParserAdditionalFieldsPolicy.PreserveRawLog && !targetHasAdditionalFields)
        {
            issues.Add(Warning(
                spec,
                "AdditionalFields",
                "Parser spec preserves raw log fields, but target contract does not define AdditionalFields."));
        }
    }

    private static void ValidateColumns(
        ParserSpec spec,
        ParserViewDef parserView,
        CanonicalViewDef target,
        List<ParserSpecViewBridgeIssue> issues)
    {
        var parserColumns = parserView.Columns
            .Select(static column => column.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var targetColumns = target.Columns
            .Select(static column => column.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var column in targetColumns)
        {
            if (!parserColumns.Contains(column))
            {
                issues.Add(Error(spec, column, $"Parser view does not expose target column {column}."));
            }
        }

        foreach (var projection in spec.Projections)
        {
            if (!parserColumns.Contains(projection.TargetColumn))
            {
                issues.Add(Error(spec, projection.TargetColumn, $"Parser spec projection {projection.TargetColumn} is not exposed by parser view."));
            }
        }

        foreach (var intentionalNull in spec.IntentionalNulls)
        {
            if (!targetColumns.Contains(intentionalNull.TargetColumn))
            {
                issues.Add(Error(spec, intentionalNull.TargetColumn, $"Parser spec intentional null {intentionalNull.TargetColumn} is not part of target contract."));
            }
        }
    }

    private static void ValidateProjectionMapping(
        ParserSpec spec,
        ParserViewDef parserView,
        List<ParserSpecViewBridgeIssue> issues)
    {
        var mappedColumns = parserView.Mapping.Projections
            .Select(static projection => projection.TargetColumn)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var projection in spec.Projections)
        {
            if (!mappedColumns.Contains(projection.TargetColumn))
            {
                issues.Add(Error(
                    spec,
                    projection.TargetColumn,
                    $"Parser spec projection {projection.TargetColumn} is not mapped by parser view."));
            }
        }

        var suppliedBySpec = spec.Projections
            .Select(static projection => projection.TargetColumn)
            .Concat(spec.IntentionalNulls.Select(static intentionalNull => intentionalNull.TargetColumn))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var mappedColumn in mappedColumns)
        {
            if (!suppliedBySpec.Contains(mappedColumn))
            {
                issues.Add(Error(
                    spec,
                    mappedColumn,
                    $"Parser view maps {mappedColumn}, but parser spec neither projects it nor marks it intentionally null."));
            }
        }
    }

    private static ParserSpecViewBridgeIssue Warning(ParserSpec spec, string? columnName, string message) =>
        new(ParserSpecValidationSeverity.Warning, spec.QualifiedName, columnName, message);
}

public sealed record ParserSpecViewBridgeIssue(
    ParserSpecValidationSeverity Severity,
    string ParserName,
    string? ColumnName,
    string Message);

public sealed class ParserSpecViewBridgeException : InvalidOperationException
{
    public ParserSpecViewBridgeException(
        string parserName,
        IReadOnlyList<ParserSpecViewBridgeIssue> errors)
        : base($"Parser specification {parserName} does not match the existing parser view.")
    {
        ParserName = parserName;
        Errors = errors;
    }

    public IReadOnlyList<ParserSpecViewBridgeIssue> Errors { get; }
    public string ParserName { get; }
}
