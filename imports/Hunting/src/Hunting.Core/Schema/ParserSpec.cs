namespace Hunting.Core.Schema;

public enum ParserProjectionKind
{
    Expression,
    JsonPath,
    Literal,
    SourceColumn
}

/// <summary>
/// An explicit null projection required to satisfy the target Golden contract.
/// </summary>
public sealed class ParserIntentionalNullSpec
{
    public ParserIntentionalNullSpec(
        string targetColumn,
        DuckDbType duckDbType,
        string reason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(targetColumn);
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);

        TargetColumn = targetColumn.Trim();
        DuckDbType = duckDbType;
        Reason = reason.Trim();
    }

    public DuckDbType DuckDbType { get; }
    public string Reason { get; }
    public string TargetColumn { get; }
}

/// <summary>
/// A non-null parser projection from source expression/path into a target Golden column.
/// </summary>
public sealed class ParserProjectionSpec
{
    public ParserProjectionSpec(
        string targetColumn,
        string expression,
        string? sourceField = null,
        ParserProjectionKind kind = ParserProjectionKind.Expression)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(targetColumn);
        ArgumentException.ThrowIfNullOrWhiteSpace(expression);

        TargetColumn = targetColumn.Trim();
        Expression = expression.Trim();
        SourceField = string.IsNullOrWhiteSpace(sourceField) ? null : sourceField.Trim();
        Kind = kind;
    }

    public string Expression { get; }
    public ParserProjectionKind Kind { get; }
    public string? SourceField { get; }
    public string TargetColumn { get; }
}

/// <summary>
/// Structured specification for one Silver parser contributor.
/// Phase 1D introduces this as the reviewable contract before parser view generation is moved onto specs.
/// </summary>
public sealed class ParserSpec
{
    public ParserSpec(
        string name,
        string sourceObject,
        string targetContract,
        string sourceName,
        string selector,
        IReadOnlyList<ParserProjectionSpec> projections,
        IReadOnlyList<ParserIntentionalNullSpec> intentionalNulls,
        ParserAdditionalFieldsPolicy additionalFieldsPolicy = ParserAdditionalFieldsPolicy.PreserveRawLog)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceObject);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetContract);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceName);
        ArgumentException.ThrowIfNullOrWhiteSpace(selector);
        ArgumentNullException.ThrowIfNull(projections);
        ArgumentNullException.ThrowIfNull(intentionalNulls);

        if (projections.Count == 0)
        {
            throw new ArgumentException("Parser specification must define at least one projection.", nameof(projections));
        }

        var duplicateProjection = projections
            .GroupBy(static projection => projection.TargetColumn, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(static group => group.Count() > 1);

        if (duplicateProjection is not null)
        {
            throw new ArgumentException($"Parser specification contains duplicate projection for {duplicateProjection.Key}.", nameof(Projections));
        }

        var duplicateNull = intentionalNulls
            .GroupBy(static intentionalNull => intentionalNull.TargetColumn, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(static group => group.Count() > 1);

        if (duplicateNull is not null)
        {
            throw new ArgumentException($"Parser specification contains duplicate intentional null for {duplicateNull.Key}.", nameof(intentionalNulls));
        }

        var overlap = projections
            .Select(static projection => projection.TargetColumn)
            .Intersect(intentionalNulls.Select(static intentionalNull => intentionalNull.TargetColumn), StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();

        if (overlap is not null)
        {
            throw new ArgumentException($"Parser specification cannot both project and intentionally null {overlap}.");
        }

        Name = name.Trim();
        SourceObject = sourceObject.Trim();
        TargetContract = targetContract.Trim();
        SourceName = sourceName.Trim();
        Selector = selector.Trim();
        Projections = projections;
        IntentionalNulls = intentionalNulls;
        AdditionalFieldsPolicy = additionalFieldsPolicy;
    }

    public ParserAdditionalFieldsPolicy AdditionalFieldsPolicy { get; }
    public IReadOnlyList<ParserIntentionalNullSpec> IntentionalNulls { get; }
    public string Name { get; }
    public IReadOnlyList<ParserProjectionSpec> Projections { get; }
    public string QualifiedName => Name.Contains('.', StringComparison.Ordinal) ? Name : $"silver.{Name}";
    public string Selector { get; }
    public string SourceName { get; }
    public string SourceObject { get; }
    public string TargetContract { get; }
}

public enum ParserAdditionalFieldsPolicy
{
    None,
    PreserveRawLog
}
