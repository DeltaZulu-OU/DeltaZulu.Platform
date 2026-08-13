using System.Text;
using static DeltaZulu.Platform.Data.Proton.Ddl.ProtonDdlHelpers;

namespace DeltaZulu.Platform.Data.Proton.Ddl;

/// <summary>
/// Fluent builder for Timeplus Proton <c>CREATE MATERIALIZED VIEW</c> DDL.
/// Produces the full DDL string; does not execute against Proton.
/// </summary>
public sealed class MaterializedViewDdl
{
    private readonly string _name;
    private bool _ifNotExists = true;
    private string? _targetStream;
    private string? _selectSql;
    private string? _comment;
    private readonly Dictionary<string, string> _settings = new();

    public MaterializedViewDdl(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        _name = name;
    }

    public MaterializedViewDdl IfNotExists(bool value = true)                  { _ifNotExists = value; return this; }
    public MaterializedViewDdl Into(string targetStream)                        { _targetStream = targetStream; return this; }
    public MaterializedViewDdl As(string selectSql)                             { _selectSql = selectSql; return this; }
    public MaterializedViewDdl WithComment(string comment)                      { _comment = comment; return this; }
    public MaterializedViewDdl WithCheckpointInterval(int seconds)              { _settings["checkpoint_interval"] = seconds.ToString(); return this; }
    public MaterializedViewDdl WithCheckpointDisabled()                         { _settings["checkpoint_interval"] = "-1"; return this; }
    public MaterializedViewDdl WithEnableDlq(bool enable = true)               { _settings["enable_dlq"] = enable ? "true" : "false"; return this; }
    public MaterializedViewDdl WithPauseOnStart(bool pause = true)             { _settings["pause_on_start"] = pause ? "true" : "false"; return this; }
    public MaterializedViewDdl WithRecoveryPolicy(RecoveryPolicy policy)        { _settings["recovery_policy"] = $"'{policy.ToString().ToLowerInvariant()}'"; return this; }
    public MaterializedViewDdl WithMemoryWeight(int weight)                     { _settings["memory_weight"] = weight.ToString(); return this; }
    public MaterializedViewDdl WithDefaultHashTable(HashTableMode mode)         { _settings["default_hash_table"] = $"'{mode.ToString().ToLowerInvariant()}'"; return this; }
    public MaterializedViewDdl WithDefaultHashJoin(HashTableMode mode)          { _settings["default_hash_join"] = $"'{mode.ToString().ToLowerInvariant()}'"; return this; }
    public MaterializedViewDdl WithMaxHotKeys(int maxKeys)                      { _settings["max_hot_keys"] = maxKeys.ToString(); return this; }

    /// <summary>Builds and returns the <c>CREATE MATERIALIZED VIEW</c> DDL statement.</summary>
    public string Build()
    {
        if (string.IsNullOrWhiteSpace(_selectSql))
            throw new InvalidOperationException("SELECT query is required — call As(...).");

        var sb = new StringBuilder("CREATE MATERIALIZED VIEW ");
        if (_ifNotExists) sb.Append("IF NOT EXISTS ");
        sb.Append(QuoteName(_name));

        if (_targetStream is not null)
        {
            sb.Append("\nINTO ");
            sb.Append(QuoteName(_targetStream));
        }

        sb.Append("\nAS\n");
        sb.Append(_selectSql);

        if (_settings.Count > 0)
        {
            sb.Append("\nSETTINGS\n    ");
            sb.Append(string.Join(",\n    ", _settings.Select(kv => $"{kv.Key}={kv.Value}")));
        }

        if (_comment is not null)
            sb.Append($"\nCOMMENT '{EscapeSingleQuote(_comment)}'");

        return sb.ToString();
    }

    /// <summary>Returns a <c>DROP VIEW IF EXISTS</c> statement for this view.</summary>
    public string BuildDrop() =>
        $"DROP VIEW IF EXISTS {QuoteName(_name)};";

    /// <summary>Returns an <c>ALTER VIEW … MODIFY QUERY SETTING</c> statement.</summary>
    public string BuildAlterSetting(string key, object value) =>
        $"ALTER VIEW {QuoteName(_name)} MODIFY QUERY SETTING {key}={value};";

    /// <summary>Returns an <c>ALTER VIEW … MODIFY COMMENT</c> statement.</summary>
    public string BuildAlterComment(string comment) =>
        $"ALTER VIEW {QuoteName(_name)} MODIFY COMMENT '{EscapeSingleQuote(comment)}';";
}

public enum RecoveryPolicy { Strict, BestEffort }
public enum HashTableMode   { Memory, Hybrid }
