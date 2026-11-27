using DeltaZulu.Platform.Domain.Analytics.QueryModel;

namespace DeltaZulu.Platform.Application.Analytics.Nrt;

/// <summary>Evaluates the initial record-local stream plan over a typed, canonical row.</summary>
public static class LocalStreamNrtEvaluator
{
    public static IReadOnlyDictionary<string, object?>? Evaluate(
        RelNode plan, string viewName, IReadOnlyDictionary<string, object?> row)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(row);
        return plan switch
        {
            ScanNode scan => string.Equals(scan.ViewName, viewName, StringComparison.OrdinalIgnoreCase)
                ? row : null,
            FilterNode filter => ApplyFilter(filter, viewName, row),
            ProjectNode project => ApplyProject(project, viewName, row),
            _ => throw new NotSupportedException($"Unsupported stream operator: {plan.GetType().Name}.")
        };
    }

    private static IReadOnlyDictionary<string, object?>? ApplyFilter(
        FilterNode filter, string viewName, IReadOnlyDictionary<string, object?> row)
    {
        var input = Evaluate(filter.Input, viewName, row);
        return input is not null && Predicate(filter.Predicate, input) == true ? input : null;
    }

    private static IReadOnlyDictionary<string, object?>? ApplyProject(
        ProjectNode project, string viewName, IReadOnlyDictionary<string, object?> row)
    {
        var input = Evaluate(project.Input, viewName, row);
        if (input is null)
            return null;
        var result = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var projection in project.Projections)
        {
            if (projection.Expression is not ColumnRef { Qualifier: null } column)
                throw new NotSupportedException("Unsupported stream projection.");
            result.Add(projection.Alias, ReadColumn(input, column.Name));
        }
        return result;
    }

    private static object? ReadColumn(IReadOnlyDictionary<string, object?> row, string name)
        => row.TryGetValue(name, out var value)
            ? value : throw new InvalidOperationException($"Canonical row is missing column '{name}'.");

    private static bool? Predicate(ScalarExpr expression, IReadOnlyDictionary<string, object?> row)
    {
        if (expression is not BinaryScalar binary)
            throw new NotSupportedException("Unsupported stream predicate.");
        if (binary.Op is ScalarBinaryOp.And or ScalarBinaryOp.Or)
        {
            var left = Predicate(binary.Left, row);
            var right = Predicate(binary.Right, row);
            return binary.Op == ScalarBinaryOp.And ? left & right : left | right;
        }
        if (binary.Op is not (ScalarBinaryOp.Eq or ScalarBinaryOp.Neq)
            || binary.Left is not ColumnRef { Qualifier: null } column
            || binary.Right is not LiteralScalar { Value: not null } literal)
            throw new NotSupportedException("Unsupported stream comparison.");

        var value = ReadColumn(row, column.Name);
        // Kusto equality with one null operand is false; inequality is true.
        // Canonical KQL strings must be non-null (empty string represents absence).
        if (value is null)
        {
            if (literal.Kind == LiteralKind.String)
                throw new InvalidOperationException("Canonical KQL string cannot be null.");
            return binary.Op == ScalarBinaryOp.Neq;
        }

        var equal = (literal.Kind, value, literal.Value) switch
        {
            (LiteralKind.String, string a, string b) => string.Equals(a, b, StringComparison.Ordinal),
            (LiteralKind.Bool, bool a, bool b) => a == b,
            (LiteralKind.Int or LiteralKind.Long, int a, int b) => a == b,
            (LiteralKind.Int or LiteralKind.Long, long a, long b) => a == b,
            (LiteralKind.Int or LiteralKind.Long, int a, long b) => a == b,
            (LiteralKind.Int or LiteralKind.Long, long a, int b) => a == b,
            _ => throw new InvalidOperationException($"Incompatible canonical type for '{column.Name}'.")
        };
        return binary.Op == ScalarBinaryOp.Eq ? equal : !equal;
    }
}
