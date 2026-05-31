namespace Hunting.Schema.Definitions.Phase1D;

using Hunting.Core.Schema;

/// <summary>
/// Validates the active parser-spec catalog against active medallion schema conventions.
/// This is a catalog-level guard over parser specs; it does not change parser view generation.
/// </summary>
public static class Phase1DParserSpecCatalogValidator
{
    public static IReadOnlyList<ParserSpecCatalogValidationIssue> ValidateActiveCatalog() => Validate(
            Phase1DParserSpecCatalog.ParserSpecs,
            SchemaConventions.RawTables,
            SchemaConventions.ParserViews,
            SchemaConventions.CanonicalViews);

    public static IReadOnlyList<ParserSpecCatalogValidationIssue> Validate(
        IReadOnlyList<ParserSpec> specs,
        IReadOnlyList<RawTableDef> rawTables,
        IReadOnlyList<ParserViewDef> parserViews,
        IReadOnlyList<CanonicalViewDef> canonicalViews)
    {
        ArgumentNullException.ThrowIfNull(specs);
        ArgumentNullException.ThrowIfNull(rawTables);
        ArgumentNullException.ThrowIfNull(parserViews);
        ArgumentNullException.ThrowIfNull(canonicalViews);

        var issues = new List<ParserSpecCatalogValidationIssue>();

        ValidateDuplicateSpecNames(specs, issues);
        ValidateSpecSetMatchesParserViews(specs, parserViews, issues);
        ValidateSourceObjects(specs, rawTables, issues);
        ValidateTargetContracts(specs, canonicalViews, issues);
        ValidateContractCoverage(specs, canonicalViews, issues);
        ValidateAdditionalFieldsPolicy(specs, canonicalViews, issues);

        return issues
            .OrderBy(static issue => issue.ParserName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(static issue => issue.ColumnName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(static issue => issue.Message, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static void ValidateDuplicateSpecNames(
        IReadOnlyList<ParserSpec> specs,
        List<ParserSpecCatalogValidationIssue> issues)
    {
        foreach (var duplicate in specs.GroupBy(static spec => spec.QualifiedName, StringComparer.OrdinalIgnoreCase).Where(static group => group.Count() > 1))
        {
            issues.Add(new ParserSpecCatalogValidationIssue(
                ParserSpecValidationSeverity.Error,
                duplicate.Key,
                null,
                $"Parser spec name {duplicate.Key} is duplicated."));
        }
    }

    private static void ValidateSpecSetMatchesParserViews(
        IReadOnlyList<ParserSpec> specs,
        IReadOnlyList<ParserViewDef> parserViews,
        List<ParserSpecCatalogValidationIssue> issues)
    {
        var specNames = specs
            .Select(static spec => spec.QualifiedName)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var parserViewNames = parserViews
            .Select(static view => view.QualifiedName)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var missingSpec in parserViewNames.Except(specNames, StringComparer.OrdinalIgnoreCase))
        {
            issues.Add(new ParserSpecCatalogValidationIssue(
                ParserSpecValidationSeverity.Error,
                missingSpec,
                null,
                $"Active parser view {missingSpec} has no parser spec."));
        }

        foreach (var extraSpec in specNames.Except(parserViewNames, StringComparer.OrdinalIgnoreCase))
        {
            issues.Add(new ParserSpecCatalogValidationIssue(
                ParserSpecValidationSeverity.Error,
                extraSpec,
                null,
                $"Parser spec {extraSpec} has no active parser view."));
        }
    }

    private static void ValidateSourceObjects(
        IReadOnlyList<ParserSpec> specs,
        IReadOnlyList<RawTableDef> rawTables,
        List<ParserSpecCatalogValidationIssue> issues)
    {
        var sourceObjects = rawTables
            .Select(static table => table.QualifiedName)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var spec in specs)
        {
            if (!sourceObjects.Contains(spec.SourceObject))
            {
                issues.Add(new ParserSpecCatalogValidationIssue(
                    ParserSpecValidationSeverity.Error,
                    spec.QualifiedName,
                    null,
                    $"Parser spec source object {spec.SourceObject} is not an active Bronze table."));
            }
        }
    }

    private static void ValidateTargetContracts(
        IReadOnlyList<ParserSpec> specs,
        IReadOnlyList<CanonicalViewDef> canonicalViews,
        List<ParserSpecCatalogValidationIssue> issues)
    {
        var targets = canonicalViews
            .SelectMany(static view => new[] { view.Name, view.QualifiedName })
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var spec in specs)
        {
            if (!targets.Contains(spec.TargetContract))
            {
                issues.Add(new ParserSpecCatalogValidationIssue(
                    ParserSpecValidationSeverity.Error,
                    spec.QualifiedName,
                    null,
                    $"Parser spec target contract {spec.TargetContract} is not an active Golden contract."));
            }
        }
    }

    private static void ValidateContractCoverage(
        IReadOnlyList<ParserSpec> specs,
        IReadOnlyList<CanonicalViewDef> canonicalViews,
        List<ParserSpecCatalogValidationIssue> issues)
    {
        foreach (var spec in specs)
        {
            var target = canonicalViews.SingleOrDefault(view =>
                view.Name.Equals(spec.TargetContract, StringComparison.OrdinalIgnoreCase) ||
                view.QualifiedName.Equals(spec.TargetContract, StringComparison.OrdinalIgnoreCase));

            if (target is null)
            {
                continue;
            }

            foreach (var issue in ParserSpecValidator.ValidateAgainstTarget(spec, target))
            {
                issues.Add(new ParserSpecCatalogValidationIssue(
                    issue.Severity,
                    issue.ParserName,
                    issue.ColumnName,
                    issue.Message));
            }
        }
    }

    private static void ValidateAdditionalFieldsPolicy(
        IReadOnlyList<ParserSpec> specs,
        IReadOnlyList<CanonicalViewDef> canonicalViews,
        List<ParserSpecCatalogValidationIssue> issues)
    {
        foreach (var spec in specs)
        {
            var target = canonicalViews.SingleOrDefault(view =>
                view.Name.Equals(spec.TargetContract, StringComparison.OrdinalIgnoreCase) ||
                view.QualifiedName.Equals(spec.TargetContract, StringComparison.OrdinalIgnoreCase));

            if (target is null)
            {
                continue;
            }

            var targetHasAdditionalFields = target.Columns.Any(static column =>
                column.Name.Equals("AdditionalFields", StringComparison.OrdinalIgnoreCase));

            if (spec.AdditionalFieldsPolicy == ParserAdditionalFieldsPolicy.PreserveRawLog && !targetHasAdditionalFields)
            {
                issues.Add(new ParserSpecCatalogValidationIssue(
                    ParserSpecValidationSeverity.Warning,
                    spec.QualifiedName,
                    "AdditionalFields",
                    "Parser spec preserves raw log fields, but target contract does not define AdditionalFields."));
            }

            if (spec.AdditionalFieldsPolicy == ParserAdditionalFieldsPolicy.None && targetHasAdditionalFields)
            {
                issues.Add(new ParserSpecCatalogValidationIssue(
                    ParserSpecValidationSeverity.Warning,
                    spec.QualifiedName,
                    "AdditionalFields",
                    "Target contract defines AdditionalFields, but parser spec does not preserve raw log fields."));
            }
        }
    }
}

public sealed record ParserSpecCatalogValidationIssue(
    ParserSpecValidationSeverity Severity,
    string ParserName,
    string? ColumnName,
    string Message);