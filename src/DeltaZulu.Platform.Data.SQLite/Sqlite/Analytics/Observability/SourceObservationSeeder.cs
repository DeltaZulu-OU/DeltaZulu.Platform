using DeltaZulu.Platform.Domain.Analytics.Observability;

namespace DeltaZulu.Platform.Data.Sqlite.Analytics.Observability;

public static class SourceObservationSeeder
{
    public static async Task SeedIfEmptyAsync(
        ISourceObservationRepository repository,
        CancellationToken cancellationToken = default)
    {
        var existing = await repository.ListLatestAsync(cancellationToken);
        if (existing.Count > 0)
        {
            return;
        }

        var now = DateTime.UtcNow;
        var snapshots = BuildDemoSnapshots(now);

        foreach (var snapshot in snapshots)
        {
            await repository.UpsertAsync(snapshot, cancellationToken);
        }
    }

    private static IReadOnlyList<SourceObservationSnapshot> BuildDemoSnapshots(DateTime observedAt)
    {
        return [
            new("WindowsEventLog", "Security", "agent-dc01", "dc01.corp.local",
                IsEnabled: true, CanRead: true, LastReadAtUtc: observedAt.AddMinutes(-1),
                ReadErrorCount: 0, LastError: null,
                ReadCount: 84_320, KeptAfterFilterCount: 71_490, DiscardedCount: 12_830,
                ForwardedCount: 71_490, ForwardFailedCount: 0, ObservedAtUtc: observedAt),

            new("WindowsEventLog", "Sysmon/Operational", "agent-dc01", "dc01.corp.local",
                IsEnabled: true, CanRead: true, LastReadAtUtc: observedAt.AddMinutes(-1),
                ReadErrorCount: 0, LastError: null,
                ReadCount: 23_150, KeptAfterFilterCount: 23_150, DiscardedCount: 0,
                ForwardedCount: 23_150, ForwardFailedCount: 0, ObservedAtUtc: observedAt),

            new("WindowsEventLog", "Security", "agent-web01", "web01.corp.local",
                IsEnabled: true, CanRead: true, LastReadAtUtc: observedAt.AddMinutes(-2),
                ReadErrorCount: 0, LastError: null,
                ReadCount: 42_100, KeptAfterFilterCount: 38_200, DiscardedCount: 3_900,
                ForwardedCount: 38_200, ForwardFailedCount: 0, ObservedAtUtc: observedAt),

            new("WindowsEventLog", "Sysmon/Operational", "agent-web01", "web01.corp.local",
                IsEnabled: true, CanRead: true, LastReadAtUtc: observedAt.AddMinutes(-2),
                ReadErrorCount: 0, LastError: null,
                ReadCount: 8_740, KeptAfterFilterCount: 8_740, DiscardedCount: 0,
                ForwardedCount: 8_740, ForwardFailedCount: 0, ObservedAtUtc: observedAt),

            new("WindowsEventLog", "Security", "agent-db01", "db01.corp.local",
                IsEnabled: true, CanRead: true, LastReadAtUtc: observedAt.AddMinutes(-3),
                ReadErrorCount: 2, LastError: "Transient bookmark error at offset 1042",
                ReadCount: 31_800, KeptAfterFilterCount: 29_100, DiscardedCount: 2_700,
                ForwardedCount: 28_900, ForwardFailedCount: 200, ObservedAtUtc: observedAt),

            new("WindowsEventLog", "Sysmon/Operational", "agent-db01", "db01.corp.local",
                IsEnabled: false, CanRead: false, LastReadAtUtc: null,
                ReadErrorCount: 0, LastError: null,
                ReadCount: 0, KeptAfterFilterCount: 0, DiscardedCount: 0,
                ForwardedCount: 0, ForwardFailedCount: 0, ObservedAtUtc: observedAt),

            new("DNSServer", "Microsoft-Windows-DNS-Server/Analytical", "agent-dc01", "dc01.corp.local",
                IsEnabled: true, CanRead: true, LastReadAtUtc: observedAt.AddMinutes(-1),
                ReadErrorCount: 0, LastError: null,
                ReadCount: 156_200, KeptAfterFilterCount: 14_100, DiscardedCount: 142_100,
                ForwardedCount: 14_100, ForwardFailedCount: 0, ObservedAtUtc: observedAt),
        ];
    }
}
