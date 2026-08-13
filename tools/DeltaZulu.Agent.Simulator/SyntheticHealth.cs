namespace DeltaZulu.Agent.Simulator;

/// <summary>
/// Random-walk health signals so the fleet UI shows realistic buffer pressure,
/// queue depth, and occasional drops/forward failures. Unrelated to Importing.Core's
/// DemoSeedImportCatalog or Data.SQLite's GovernanceSampleDataSeeder - this generates live
/// heartbeat telemetry for the simulator, not fixture files or dev-database rows.
/// </summary>
public sealed class SyntheticHealth
{
    private readonly Random _random = new();
    private double _bufferPressure = 0.1;
    private long _queueDepth = 50;
    private long _droppedTotal;
    private long _forwardFailedTotal;

    public (double BufferPressure, long QueueDepth, long Dropped, long ForwardFailed, string Status) Next()
    {
        _bufferPressure = Math.Clamp(
            _bufferPressure + (_random.NextDouble() - 0.48) * 0.08, 0.02, 0.98);
        _queueDepth = Math.Max(0, _queueDepth + _random.Next(-40, 45));

        if (_random.NextDouble() < 0.05)
            _droppedTotal += _random.Next(1, 20);
        if (_random.NextDouble() < 0.05)
            _forwardFailedTotal += _random.Next(1, 10);

        var status = _bufferPressure >= 0.85 ? "Degraded" : "Online";
        return (_bufferPressure, _queueDepth, _droppedTotal, _forwardFailedTotal, status);
    }

    /// <summary>
    /// The two fixed named sources every simulated agent reports, plus
    /// <paramref name="extraSourceCount"/> synthetic filler sources beyond that.
    /// The filler knob exists to exercise
    /// <c>AgentCheckInService.MaxSourcesPerHeartbeat</c> end to end against the
    /// real API (e.g. <c>--source-count 1001</c> to trigger the server's rejection)
    /// rather than only in unit tests.
    /// </summary>
    public IReadOnlyList<SourceHealthEntry> NextSources(DateTimeOffset now, int extraSourceCount = 0)
    {
        _securityReadTotal += _random.Next(50, 400);
        _sysmonReadTotal += _random.Next(20, 200);
        if (_random.NextDouble() < 0.08)
            _sysmonErrorTotal += _random.Next(1, 3);

        var sources = new List<SourceHealthEntry>(2 + Math.Max(0, extraSourceCount))
        {
            new(
                "WindowsEventLog", "Security", IsEnabled: true, CanRead: true,
                LastReadAt: now, ReadErrorCount: 0, LastError: null,
                ReadCount: _securityReadTotal,
                KeptAfterFilterCount: (long)(_securityReadTotal * 0.8),
                DiscardedCount: (long)(_securityReadTotal * 0.2),
                ForwardedCount: (long)(_securityReadTotal * 0.8),
                ForwardFailedCount: 0,
                SourceInstanceId: "security-eventlog",
                ResourceFamily: "EventLog",
                Provider: "Microsoft-Windows-Security-Auditing"),
            new(
                "WindowsEventLog", "Microsoft-Windows-Sysmon/Operational", IsEnabled: true,
                CanRead: _sysmonErrorTotal < 5,
                LastReadAt: now, ReadErrorCount: _sysmonErrorTotal,
                LastError: _sysmonErrorTotal > 0 ? "Simulated intermittent channel read failure" : null,
                ReadCount: _sysmonReadTotal,
                KeptAfterFilterCount: _sysmonReadTotal,
                DiscardedCount: 0,
                ForwardedCount: _sysmonReadTotal,
                ForwardFailedCount: 0,
                SourceInstanceId: "sysmon-operational",
                ResourceFamily: "EventLog",
                Provider: "Microsoft-Windows-Sysmon"),
        };

        for (var i = 0; i < extraSourceCount; i++)
        {
            sources.Add(new SourceHealthEntry(
                "File", $"synthetic-{i}", IsEnabled: true, CanRead: true,
                LastReadAt: now, ReadErrorCount: 0, LastError: null,
                ReadCount: 1, KeptAfterFilterCount: 1, DiscardedCount: 0,
                ForwardedCount: 1, ForwardFailedCount: 0,
                SourceInstanceId: $"synthetic-{i}"));
        }

        return sources;
    }

    private long _securityReadTotal;
    private long _sysmonReadTotal;
    private long _sysmonErrorTotal;
}
