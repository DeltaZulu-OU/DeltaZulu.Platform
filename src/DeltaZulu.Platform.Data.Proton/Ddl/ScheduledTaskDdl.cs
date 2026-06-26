using static DeltaZulu.Platform.Data.Proton.Ddl.ProtonDdlHelpers;

namespace DeltaZulu.Platform.Data.Proton.Ddl;

/// <summary>
/// Fluent builder for Timeplus Proton <c>CREATE OR REPLACE TASK</c> DDL.
/// Scheduled tasks run a historical SELECT query on a fixed interval and write results
/// into a target stream. Use for periodic/scheduled detections.
/// Produces the DDL string; does not execute against Proton.
/// </summary>
public sealed class ScheduledTaskDdl
{
    private readonly string _name;
    private ProtonInterval? _schedule;
    private ProtonInterval? _timeout;
    private string? _targetStream;
    private string? _selectSql;

    public ScheduledTaskDdl(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        _name = name;
    }

    public ScheduledTaskDdl Schedule(ProtonInterval interval)
    { _schedule = interval; return this; }

    public ScheduledTaskDdl Timeout(ProtonInterval interval)
    { _timeout = interval; return this; }

    public ScheduledTaskDdl Into(string targetStream)
    { _targetStream = targetStream; return this; }

    public ScheduledTaskDdl As(string selectSql)
    { _selectSql = selectSql; return this; }

    /// <summary>Builds and returns the <c>CREATE OR REPLACE TASK</c> DDL statement.</summary>
    public string Build()
    {
        if (_schedule is null)
        {
            throw new InvalidOperationException("Schedule interval is required — call Schedule(...).");
        }

        if (_timeout is null)
        {
            throw new InvalidOperationException("Timeout interval is required — call Timeout(...).");
        }

        if (string.IsNullOrWhiteSpace(_targetStream))
        {
            throw new InvalidOperationException("Target stream is required — call Into(...).");
        }

        if (string.IsNullOrWhiteSpace(_selectSql))
        {
            throw new InvalidOperationException("SELECT query is required — call As(...).");
        }

        return $"""
            CREATE OR REPLACE TASK {QuoteName(_name)}
            SCHEDULE {_schedule}
            TIMEOUT {_timeout}
            INTO {QuoteName(_targetStream!)}
            AS
            {_selectSql}
            """;
    }

    /// <summary>Returns a <c>DROP TASK IF EXISTS</c> statement.</summary>
    public string BuildDrop() => $"DROP TASK IF EXISTS {QuoteName(_name)};";

    /// <summary>Returns a <c>SYSTEM PAUSE TASK</c> statement.</summary>
    public string BuildPause() => $"SYSTEM PAUSE TASK {QuoteName(_name)};";

    /// <summary>Returns a <c>SYSTEM RESUME TASK</c> statement.</summary>
    public string BuildResume() => $"SYSTEM RESUME TASK {QuoteName(_name)};";

    /// <summary>Returns a <c>SHOW CREATE TASK</c> statement.</summary>
    public string BuildShow() => $"SHOW CREATE TASK {QuoteName(_name)};";
}