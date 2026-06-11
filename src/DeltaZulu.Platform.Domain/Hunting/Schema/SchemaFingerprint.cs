
using System.Security.Cryptography;
using System.Text;

namespace DeltaZulu.Platform.Domain.Hunting.Schema;
/// <summary>
/// Deterministic fingerprint helpers for schema objects.
/// Phase 1B uses these fingerprints as the stable basis for provenance and drift detection.
/// </summary>
public static class SchemaFingerprint
{
    public const string RawTableKind = "raw_table";
    public const string InternalTableKind = "internal_table";
    public const string ParserViewKind = "parser_view";
    public const string CanonicalViewKind = "canonical_view";

    public static SchemaObjectFingerprint FromRawTable(RawTableDef table)
    {
        ArgumentNullException.ThrowIfNull(table);

        return Compute(
            table.QualifiedName,
            RawTableKind,
            [
                $"source_description={NormalizeText(table.SourceDescription)}",
                .. ColumnParts(table.Columns)
            ]);
    }

    public static SchemaObjectFingerprint FromInternalTable(InternalTableDef table)
    {
        ArgumentNullException.ThrowIfNull(table);

        return Compute(
            table.QualifiedName,
            InternalTableKind,
            ColumnParts(table.Columns));
    }

    public static SchemaObjectFingerprint FromParserView(ParserViewDef view, string emittedSql)
    {
        ArgumentNullException.ThrowIfNull(view);
        ArgumentException.ThrowIfNullOrWhiteSpace(emittedSql);

        return Compute(
            view.QualifiedName,
            ParserViewKind,
            [
                $"source_name={NormalizeText(view.SourceName)}",
                $"canonical_target={NormalizeText(view.CanonicalTarget)}",
                $"source_object={NormalizeText(view.Mapping.SourceObject)}",
                $"sql={NormalizeSql(emittedSql)}",
                .. ColumnParts(view.Columns)
            ]);
    }

    public static SchemaObjectFingerprint FromCanonicalView(CanonicalViewDef view, string emittedSql)
    {
        ArgumentNullException.ThrowIfNull(view);
        ArgumentException.ThrowIfNullOrWhiteSpace(emittedSql);

        return Compute(
            view.QualifiedName,
            CanonicalViewKind,
            [
                $"parser_views={string.Join("|", view.ParserViews.Select(NormalizeText))}",
                $"sql={NormalizeSql(emittedSql)}",
                .. ColumnParts(view.Columns)
            ]);
    }

    public static string NormalizeSql(string sql)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sql);

        var builder = new StringBuilder(sql.Length);
        var previousWasWhitespace = false;

        foreach (var ch in sql.Replace("\r\n", "\n").Replace('\r', '\n'))
        {
            if (char.IsWhiteSpace(ch))
            {
                if (!previousWasWhitespace)
                {
                    builder.Append(' ');
                    previousWasWhitespace = true;
                }

                continue;
            }

            builder.Append(ch);
            previousWasWhitespace = false;
        }

        return builder.ToString().Trim();
    }

    private static SchemaObjectFingerprint Compute(
        string objectName,
        string objectKind,
        IEnumerable<string> parts)
    {
        var normalizedObjectName = NormalizeText(objectName);
        var normalizedObjectKind = NormalizeText(objectKind);

        var payload = string.Join("\n",
        [
            $"object_name={normalizedObjectName}",
            $"object_kind={normalizedObjectKind}",
            .. parts
        ]);

        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(payload));
        var hash = Convert.ToHexString(bytes).ToLowerInvariant();

        return new SchemaObjectFingerprint(normalizedObjectName, normalizedObjectKind, hash);
    }

    private static IEnumerable<string> ColumnParts(IReadOnlyList<ColumnDef> columns)
    {
        for (var i = 0; i < columns.Count; i++)
        {
            var column = columns[i];

            yield return string.Join("|",
                $"column_index={i}",
                $"name={NormalizeText(column.Name)}",
                $"duckdb_type={column.DuckDbType.ToSql()}",
                $"kusto_type={column.KustoType.ToKustoName()}",
                $"nullable={column.Nullable}");
        }
    }

    private static string NormalizeText(string value) => value.Trim();
}

/// <summary>
/// Stable schema-object fingerprint value.
/// </summary>
public sealed record SchemaObjectFingerprint(
    string ObjectName,
    string ObjectKind,
    string SchemaHash);