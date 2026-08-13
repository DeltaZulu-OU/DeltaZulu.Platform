using System.Text;
using DeltaZulu.Platform.Domain.Analytics.Mapping;
using DeltaZulu.Platform.Domain.Analytics.Schema;
using DeltaZulu.Platform.Domain.Common;
using static DeltaZulu.Platform.Data.Proton.Ddl.ProtonDdlHelpers;

namespace DeltaZulu.Platform.Data.Proton;

/// <summary>
/// <para>
/// Generates Proton DDL from the C# medallion schema catalog — the single source of truth
/// shared with DuckDB via <c>SchemaEmitter</c>.
/// </para>
/// <para>
/// Emission order: Bronze streams → Golden streams → Silver materialized views.
/// Golden streams must exist before Silver MVs that write INTO them.
/// Drop order is the reverse: Silver MVs → Golden streams → Bronze streams.
/// </para>
/// </summary>
public sealed class ProtonSchemaEmitter : ISchemaEmitter
{
    public string TargetDialect => "proton";

    // -------------------------------------------------------------------------
    // Top-level orchestration
    // -------------------------------------------------------------------------

    public IReadOnlyList<string> EmitAll(
        IEnumerable<RawTableDef> rawTables,
        IEnumerable<ParserViewDef> parserViews,
        IEnumerable<CanonicalViewDef> canonicalViews)
    {
        var raw = rawTables.ToList();
        var parser = parserViews.ToList();
        var golden = canonicalViews.ToList();

        var statements = new List<string>(raw.Count + golden.Count + parser.Count);

        foreach (var t in raw)
        {
            statements.Add(EmitStream(t));
        }

        foreach (var v in golden)
        {
            statements.Add(EmitStream(v));
        }

        foreach (var v in parser)
        {
            statements.Add(EmitSilverMv(v));
        }

        return statements;
    }

    public IReadOnlyList<string> EmitDropAll(
        IEnumerable<RawTableDef> rawTables,
        IEnumerable<ParserViewDef> parserViews,
        IEnumerable<CanonicalViewDef> canonicalViews)
    {
        var raw = rawTables.ToList();
        var parser = parserViews.ToList();
        var golden = canonicalViews.ToList();

        var statements = new List<string>(raw.Count + golden.Count + parser.Count);

        foreach (var v in parser)
        {
            statements.Add(EmitDropSilverMv(v.QualifiedName));
        }

        foreach (var v in golden)
        {
            statements.Add(EmitDropStream(v.QualifiedName));
        }

        foreach (var t in raw)
        {
            statements.Add(EmitDropStream(t.QualifiedName));
        }

        return statements;
    }

    // -------------------------------------------------------------------------
    // Stream DDL  (Bronze raw tables + Golden canonical streams)
    // -------------------------------------------------------------------------

    public string EmitStream(SchemaObjectDef def)
    {
        ArgumentNullException.ThrowIfNull(def);
        ArgumentOutOfRangeException.ThrowIfZero(def.Columns.Count, nameof(def));

        var sb = new StringBuilder("CREATE STREAM IF NOT EXISTS ");
        sb.Append(QuoteName(def.QualifiedName))
            .Append(" (\n");

        for (var i = 0; i < def.Columns.Count; i++)
        {
            var col = def.Columns[i];
            sb.Append("    ")
                .Append(QuoteIdentifier(col.Name))
                .Append(' ')
                .Append(col.ToProtonColumnType());
            if (i < def.Columns.Count - 1)
            {
                sb.Append(',');
            }

            sb.Append('\n');
        }

        sb.Append(");");
        return sb.ToString();
    }

    public string EmitDropStream(string qualifiedName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(qualifiedName);
        return $"DROP STREAM IF EXISTS {QuoteName(qualifiedName)};";
    }

    // -------------------------------------------------------------------------
    // Silver materialized view DDL
    // -------------------------------------------------------------------------

    public string EmitSilverMv(ParserViewDef view)
    {
        ArgumentNullException.ThrowIfNull(view);

        var targetStream = $"golden.{view.CanonicalTarget}";
        var columnTypes = view.Columns.ToDictionary(c => c.Name, c => c, StringComparer.OrdinalIgnoreCase);

        var sb = new StringBuilder("CREATE MATERIALIZED VIEW IF NOT EXISTS ");
        sb.Append(QuoteName(view.QualifiedName));
        sb.Append("\nINTO ");
        sb.Append(QuoteName(targetStream));
        sb.Append("\nAS\nSELECT\n");

        for (var i = 0; i < view.Mapping.Projections.Count; i++)
        {
            var proj = view.Mapping.Projections[i];
            sb.Append("    ");

            if (proj.Expression is LiteralExpr { Value: null }
                && columnTypes.TryGetValue(proj.TargetColumn, out var colDef))
            {
                // Emit a typed NULL so Proton does not infer an incorrect column type
                var nullType = $"nullable({colDef.DuckDbType.ToProtonSql()})";
                sb.Append($"CAST(NULL, '{SqlLiteralEscaping.EscapeSingleQuotes(nullType)}')");
            }
            else
            {
                sb.Append(EmitExpr(proj.Expression));
            }

            sb.Append(" AS ");
            sb.Append(QuoteIdentifier(proj.TargetColumn));
            if (i < view.Mapping.Projections.Count - 1)
            {
                sb.Append(',');
            }

            sb.Append('\n');
        }

        sb.Append("FROM ");
        sb.Append(QuoteName(view.Mapping.SourceObject));

        if (view.Mapping.Filter is not null)
        {
            sb.Append("\nWHERE ");
            sb.Append(EmitExpr(view.Mapping.Filter));
        }

        return sb.ToString();
    }

    public string EmitDropSilverMv(string qualifiedName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(qualifiedName);
        return $"DROP VIEW IF EXISTS {QuoteName(qualifiedName)};";
    }

    // -------------------------------------------------------------------------
    // Mapping expression → Proton SQL
    // -------------------------------------------------------------------------

    private string EmitExpr(ExprDef expr) => expr switch {
        ColumnExpr col => QuoteIdentifier(col.Name),
        LiteralExpr lit => EmitLiteral(lit),
        JsonTextExpr json => $"JSON_VALUE({EmitExpr(json.JsonColumn)}, '{SqlLiteralEscaping.EscapeSingleQuotes(json.Path)}')",
        JsonExistsExpr json => $"isNotNull(JSON_VALUE({EmitExpr(json.JsonColumn)}, '{SqlLiteralEscaping.EscapeSingleQuotes(json.Path)}'))",
        RegexExtractExpr re => EmitRegexExtract(re),
        CastExpr cast => $"CAST({EmitExpr(cast.Input)}, '{SqlLiteralEscaping.EscapeSingleQuotes(cast.TargetType.ToProtonSql())}')",
        TryCastExpr cast => $"accurateCastOrNull({EmitExpr(cast.Input)}, '{SqlLiteralEscaping.EscapeSingleQuotes(cast.TargetType.ToProtonSql())}')",
        FunctionExpr fn => $"{fn.Name}({string.Join(", ", fn.Args.Select(EmitExpr))})",
        BinaryExpr bin => EmitBinary(bin),
        CaseExpr cs => EmitCase(cs),
        _ => throw new NotSupportedException($"Unsupported mapping expression: {expr.GetType().Name}")
    };

    private static string EmitLiteral(LiteralExpr lit)
    {
        if (lit.Value is null)
        {
            return "NULL";
        }

        if (lit.Value is string s)
        {
            return $"'{SqlLiteralEscaping.EscapeSingleQuotes(s)}'";
        }

        if (lit.Value is bool b)
        {
            return b ? "true" : "false";
        }

        return lit.Value.ToString()!;
    }

    private string EmitRegexExtract(RegexExtractExpr re)
    {
        // Proton's extract() returns the first captured group.
        // For group 0 (full-match semantics), wrap the pattern in a capture group.
        var pattern = re.Group == 0 ? $"({re.Pattern})" : re.Pattern;
        return $"extract({EmitExpr(re.Input)}, '{SqlLiteralEscaping.EscapeSingleQuotes(pattern)}')";
    }

    private string EmitBinary(BinaryExpr bin)
    {
        var op = bin.Op switch {
            BinaryOp.Eq => "=",
            BinaryOp.Neq => "!=",
            BinaryOp.Lt => "<",
            BinaryOp.Lte => "<=",
            BinaryOp.Gt => ">",
            BinaryOp.Gte => ">=",
            BinaryOp.And => "AND",
            BinaryOp.Or => "OR",
            _ => throw new NotSupportedException($"Unsupported binary op: {bin.Op}")
        };
        return $"({EmitExpr(bin.Left)} {op} {EmitExpr(bin.Right)})";
    }

    private string EmitCase(CaseExpr cs)
    {
        var sb = new StringBuilder("CASE");
        foreach (var branch in cs.Branches)
        {
            sb.Append($" WHEN {EmitExpr(branch.When)} THEN {EmitExpr(branch.Then)}");
        }

        sb.Append($" ELSE {EmitExpr(cs.Else)} END");
        return sb.ToString();
    }

}