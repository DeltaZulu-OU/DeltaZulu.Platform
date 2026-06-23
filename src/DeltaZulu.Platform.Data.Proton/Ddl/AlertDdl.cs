using System.Text;
using static DeltaZulu.Platform.Data.Proton.Ddl.ProtonDdlHelpers;

namespace DeltaZulu.Platform.Data.Proton.Ddl;

/// <summary>
/// Fluent builder for Timeplus Proton <c>CREATE ALERT</c> DDL.
/// Alerts continuously monitor a source stream and invoke a Python UDF when conditions are met.
/// The SELECT query inside an Alert must be a simple statement against a stream — stateful
/// operations (joins, aggregations) belong in a Materialized View that the alert then consumes.
/// Produces the DDL string; does not execute against Proton.
/// </summary>
public sealed class AlertDdl
{
    private readonly string _name;
    private bool _ifNotExists = true;
    private int _batchEvents;
    private ProtonInterval? _batchTimeout;
    private int _limitAlerts;
    private ProtonInterval? _limitPer;
    private string? _udfName;
    private string? _selectSql;

    public AlertDdl(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        _name = name;
    }

    public AlertDdl IfNotExists(bool value = true) { _ifNotExists = value; return this; }

    /// <summary>
    /// Configures event batching. The UDF is invoked when <paramref name="n"/> events accumulate
    /// OR <paramref name="timeout"/> elapses, whichever comes first.
    /// </summary>
    public AlertDdl BatchEvents(int n, ProtonInterval timeout)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(n);
        _batchEvents = n;
        _batchTimeout = timeout;
        return this;
    }

    /// <summary>
    /// Suppresses alert flooding: the UDF fires at most <paramref name="m"/> times per <paramref name="per"/> interval.
    /// Optional — omit for no rate limiting.
    /// </summary>
    public AlertDdl LimitAlerts(int m, ProtonInterval per)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(m);
        _limitAlerts = m;
        _limitPer = per;
        return this;
    }

    /// <summary>Specifies the Python UDF to invoke when the alert fires.</summary>
    public AlertDdl Call(string udfName) { _udfName = udfName; return this; }

    /// <summary>The streaming SELECT query that feeds the alert. Must be a simple stream SELECT.</summary>
    public AlertDdl As(string selectSql) { _selectSql = selectSql; return this; }

    /// <summary>Builds and returns the <c>CREATE ALERT</c> DDL statement.</summary>
    public string Build()
    {
        if (_batchEvents <= 0 || _batchTimeout is null)
            throw new InvalidOperationException("Batch configuration is required — call BatchEvents(...).");
        if (string.IsNullOrWhiteSpace(_udfName))
            throw new InvalidOperationException("UDF name is required — call Call(...).");
        if (string.IsNullOrWhiteSpace(_selectSql))
            throw new InvalidOperationException("SELECT query is required — call As(...).");

        var sb = new StringBuilder("CREATE ALERT ");
        if (_ifNotExists) sb.Append("IF NOT EXISTS ");
        sb.Append(QuoteName(_name));
        sb.Append($"\nBATCH {_batchEvents} EVENTS WITH TIMEOUT {_batchTimeout}");

        if (_limitAlerts > 0 && _limitPer is not null)
            sb.Append($"\nLIMIT {_limitAlerts} ALERTS PER {_limitPer}");

        sb.Append($"\nCALL {QuoteIdentifier(_udfName!)}");
        sb.Append($"\nAS\n{_selectSql}");

        return sb.ToString();
    }

    /// <summary>Returns a <c>DROP ALERT IF EXISTS</c> statement.</summary>
    public string BuildDrop() => $"DROP ALERT IF EXISTS {QuoteName(_name)};";

    /// <summary>Returns a <c>SHOW CREATE ALERT</c> statement.</summary>
    public string BuildShow() => $"SHOW CREATE ALERT {QuoteName(_name)};";
}
