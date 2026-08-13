
using System.Text;
using DeltaZulu.Platform.Domain.Analytics.Mapping;
using DeltaZulu.Platform.Domain.Analytics.Schema;

namespace DeltaZulu.Platform.Data.DuckDb.Sql;
/// <summary>
/// <para>
/// Generates transient DuckDB DDL (CREATE SCHEMA, CREATE TABLE, CREATE VIEW)
/// from C# schema models. SQL is generated, executed, and discarded — never
/// persisted as a source artifact.
/// </para>
/// <para>
/// Emission order matters: schemas → bronze tables → silver tables →
/// silver parser views → golden public hunting views.
/// </para>
/// </summary>
public sealed class SchemaEmitter
{
    private static readonly string[] first = new[]
            {
                "bronze",
                "silver",
                "golden"
            };

    #region Top-level orchestration

    /// <summary>
    /// Emit all DDL statements needed to build the database from scratch,
    /// in dependency order.
    /// </summary>
    public IReadOnlyList<string> EmitAll(
        IEnumerable<RawTableDef> rawTables,
        IEnumerable<InternalTableDef> internalTables,
        IEnumerable<ParserViewDef> parserViews,
        IEnumerable<CanonicalViewDef> canonicalViews,
        IEnumerable<InternalViewDef>? internalViews = null)
    {
        var rawTableList = rawTables.ToList();
        var internalTableList = internalTables.ToList();
        var parserViewList = parserViews.ToList();
        var canonicalViewList = canonicalViews.ToList();
        var internalViewList = internalViews?.ToList() ?? [];

        var schemaNames = first.Concat(rawTableList.Select(static obj => obj.Schema))
            .Concat(internalTableList.Select(static obj => obj.Schema))
            .Concat(internalViewList.Select(static obj => obj.Schema))
            .Concat(parserViewList.Select(static obj => obj.Schema))
            .Concat(canonicalViewList.Select(static obj => obj.Schema))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var statements = schemaNames
            .ConvertAll(schema => $"CREATE SCHEMA IF NOT EXISTS {DuckDbSqlText.EscapeIdent(schema)}")
;

        // Raw tables
        foreach (var t in rawTableList)
        {
            statements.Add(EmitCreateTable(t));
        }

        // Internal tables
        foreach (var t in internalTableList)
        {
            statements.Add(EmitCreateTable(t));
        }

        // Internal views (after internal tables they depend on)
        foreach (var v in internalViewList)
        {
            statements.Add(EmitInternalView(v));
        }

        // Parser views
        foreach (var v in parserViewList)
        {
            statements.Add(EmitParserView(v));
        }

        // Public hunting views
        foreach (var v in canonicalViewList)
        {
            statements.Add(EmitCanonicalView(v));
        }

        return statements;
    }

    #endregion Top-level orchestration

    #region CREATE TABLE

    public string EmitCreateTable(SchemaObjectDef table)
    {
        ArgumentNullException.ThrowIfNull(table);
        if (table.Columns is null)
        {
            throw new InvalidOperationException(
                $"Schema object {table.QualifiedName} has null column metadata.");
        }

        var sb = new StringBuilder();
        sb.Append("CREATE TABLE IF NOT EXISTS ");
        sb.Append(DuckDbSqlText.EscapeQualifiedIdent(table.QualifiedName));
        sb.Append(" (\n");

        for (var i = 0; i < table.Columns.Count; i++)
        {
            var col = table.Columns[i];
            ArgumentNullException.ThrowIfNull(col);

            sb.Append("    ");
            sb.Append(DuckDbSqlText.EscapeIdent(col.Name));
            sb.Append(' ');
            sb.Append(col.DuckDbType.ToSql());

            if (i < table.Columns.Count - 1)
            {
                sb.Append(',');
            }

            sb.Append('\n');
        }

        sb.Append(')');
        return sb.ToString();
    }

    #endregion CREATE TABLE

    #region Internal view (internal.v_*)

    public string EmitInternalView(InternalViewDef view)
    {
        ArgumentNullException.ThrowIfNull(view);

        if (string.IsNullOrWhiteSpace(view.SqlBody))
        {
            throw new InvalidOperationException(
                $"Internal view {view.QualifiedName} has no SQL body.");
        }

        return $"CREATE OR REPLACE VIEW {DuckDbSqlText.EscapeQualifiedIdent(view.QualifiedName)} AS\n{view.SqlBody.Trim()}";
    }

    #endregion Internal view (internal.v_*)

    #region Parser view (silver.v_*)

    public string EmitParserView(ParserViewDef view)
    {
        ArgumentNullException.ThrowIfNull(view);

        if (view.Columns is null)
        {
            throw new InvalidOperationException(
                $"Parser view {view.QualifiedName} has null column metadata.");
        }

        if (view.Mapping is null)
        {
            throw new InvalidOperationException(
                $"Parser view {view.QualifiedName} has null mapping metadata.");
        }

        if (view.Mapping.Projections is null)
        {
            throw new InvalidOperationException(
                $"Parser view {view.QualifiedName} has null projection metadata.");
        }

        var sb = new StringBuilder();
        sb.Append("CREATE OR REPLACE VIEW ");
        sb.Append(DuckDbSqlText.EscapeQualifiedIdent(view.QualifiedName));
        sb.Append(" AS\nSELECT\n");

        // Build column type lookup for typed NULL emission
        var columnTypes = view.Columns.ToDictionary(c => c.Name, c => c.DuckDbType,
            StringComparer.OrdinalIgnoreCase);

        for (var i = 0; i < view.Mapping.Projections.Count; i++)
        {
            var proj = view.Mapping.Projections[i];
            sb.Append("    ");

            // Emit typed NULL when projection is a null literal — prevents
            // DuckDB from assigning INTEGER as the default NULL type
            if (proj.Expression is LiteralExpr { Value: null }
                && columnTypes.TryGetValue(proj.TargetColumn, out var colType))
            {
                sb.Append($"CAST(NULL AS {colType.ToSql()})");
            }
            else
            {
                sb.Append(EmitMappingExpr(proj.Expression));
            }

            sb.Append(" AS ");
            sb.Append(DuckDbSqlText.EscapeIdent(proj.TargetColumn));
            if (i < view.Mapping.Projections.Count - 1)
            {
                sb.Append(',');
            }

            sb.Append('\n');
        }

        sb.Append("FROM ");
        sb.Append(DuckDbSqlText.EscapeQualifiedIdent(view.Mapping.SourceObject));

        if (view.Mapping.Filter is not null)
        {
            sb.Append("\nWHERE ");
            sb.Append(EmitMappingExpr(view.Mapping.Filter));
        }

        return sb.ToString();
    }

    #endregion Parser view (silver.v_*)

    #region Canonical view (golden.*)

    public string EmitCanonicalView(CanonicalViewDef view)
    {
        ArgumentNullException.ThrowIfNull(view);

        if (view.Columns is null)
        {
            throw new InvalidOperationException(
                $"Canonical view {view.QualifiedName} has null column metadata.");
        }

        if (view.ParserViews is null)
        {
            throw new InvalidOperationException(
                $"Canonical view {view.QualifiedName} has null parser-view metadata.");
        }

        if (view.ParserViews.Count == 0)
        {
            throw new InvalidOperationException(
                $"Canonical view {view.QualifiedName} has no parser views.");
        }

        var sb = new StringBuilder();
        sb.Append("CREATE OR REPLACE VIEW ");
        sb.Append(DuckDbSqlText.EscapeQualifiedIdent(view.QualifiedName));
        sb.Append(" AS\n");

        var canonicalColumns = string.Join(",\n    ", view.Columns.Select(c => {
            ArgumentNullException.ThrowIfNull(c);
            return DuckDbSqlText.EscapeIdent(c.Name);
        }));

        for (var i = 0; i < view.ParserViews.Count; i++)
        {
            if (i > 0)
            {
                sb.Append("\nUNION ALL\n");
            }

            sb.Append("SELECT\n    ");
            sb.Append(canonicalColumns);
            sb.Append("\nFROM ");
            sb.Append(DuckDbSqlText.EscapeQualifiedIdent(view.ParserViews[i]));
        }

        return sb.ToString();
    }

    #endregion Canonical view (golden.*)

    #region Mapping expression → SQL

    private static string EmitMappingLiteral(LiteralExpr lit)
    {
        if (lit.Value is null)
        {
            return "NULL";
        }

        if (lit.Value is string s)
        {
            return $"'{EscapeSql(s)}'";
        }

        if (lit.Value is bool b)
        {
            return b ? "TRUE" : "FALSE";
        }

        return lit.Value.ToString()!;
    }

    private static string EscapeSql(string s) => s.Replace("'", "''");

    private string EmitMappingBinary(BinaryExpr bin)
    {
        var left = EmitMappingExpr(bin.Left);
        var right = EmitMappingExpr(bin.Right);
        var op = bin.Op switch
        {
            BinaryOp.Eq => "=",
            BinaryOp.Neq => "!=",
            BinaryOp.Lt => "<",
            BinaryOp.Lte => "<=",
            BinaryOp.Gt => ">",
            BinaryOp.Gte => ">=",
            BinaryOp.And => "AND",
            BinaryOp.Or => "OR",
            _ => throw new NotSupportedException($"Unsupported mapping binary op: {bin.Op}")
        };
        return $"({left} {op} {right})";
    }

    private string EmitMappingCase(CaseExpr cs)
    {
        var sb = new StringBuilder("CASE");
        foreach (var branch in cs.Branches)
        {
            sb.Append($" WHEN {EmitMappingExpr(branch.When)} THEN {EmitMappingExpr(branch.Then)}");
        }
        sb.Append($" ELSE {EmitMappingExpr(cs.Else)} END");
        return sb.ToString();
    }

    private string EmitMappingExpr(ExprDef expr) => expr switch
    {
        ColumnExpr col => DuckDbSqlText.EscapeIdent(col.Name),
        LiteralExpr lit => EmitMappingLiteral(lit),
        JsonTextExpr json => $"json_extract_string({EmitMappingExpr(json.JsonColumn)}, '{EscapeSql(json.Path)}')",
        JsonExistsExpr json => $"json_exists({EmitMappingExpr(json.JsonColumn)}, '{EscapeSql(json.Path)}')",
        RegexExtractExpr re => $"regexp_extract({EmitMappingExpr(re.Input)}, '{EscapeSql(re.Pattern)}', {re.Group})",
        CastExpr cast => $"CAST({EmitMappingExpr(cast.Input)} AS {cast.TargetType.ToSql()})",
        TryCastExpr cast => $"TRY_CAST({EmitMappingExpr(cast.Input)} AS {cast.TargetType.ToSql()})",
        FunctionExpr fn => $"{fn.Name}({string.Join(", ", fn.Args.Select(EmitMappingExpr))})",
        BinaryExpr bin => EmitMappingBinary(bin),
        CaseExpr cs => EmitMappingCase(cs),
        _ => throw new NotSupportedException($"Unsupported mapping expression: {expr.GetType().Name}")
    };
}

    #endregion Mapping expression → SQL