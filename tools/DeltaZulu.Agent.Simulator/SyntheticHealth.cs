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

    public IReadOnlyList<SourceHealthEntry> NextSources(DateTimeOffset now)
    {
        _securityReadTotal += _random.Next(50, 400);
        _sysmonReadTotal += _random.Next(20, 200);
        if (_random.NextDouble() < 0.08)
            _sysmonErrorTotal += _random.Next(1, 3);

        return
        [
            new SourceHealthEntry(
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
            new SourceHealthEntry(
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
        ];
    }

    private long _securityReadTotal;
    private long _sysmonReadTotal;
    private long _sysmonErrorTotal;
}
