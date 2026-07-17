namespace DeltaZulu.Platform.Application.Analytics.Nrt;

/// <summary>
/// Options that control the Proton ALERT artifact created alongside an NRT materialized view.
/// The ALERT monitors the MV and invokes <see cref="UdfName"/> when the batch threshold is met.
/// The UDF is responsible for writing to the <c>alert_dispatch</c> stream.
/// </summary>
public sealed record NrtAlertOptions
{
    /// <summary>Name of the Proton Python UDF to invoke when the alert fires.</summary>
    public required string UdfName { get; init; }

    /// <summary>Number of events that must accumulate before the UDF is invoked.</summary>
    public int BatchEvents { get; init; } = 1;

    /// <summary>
    /// Maximum time to wait for <see cref="BatchEvents"/> to accumulate.
    /// The UDF fires after this duration even if the batch is not full.
    /// </summary>
    public TimeSpan BatchTimeout { get; init; } = TimeSpan.FromSeconds(5);

    /// <summary>Optional rate-limit: fire at most this many alerts per <see cref="LimitPer"/>.</summary>
    public int? LimitAlerts { get; init; }

    /// <summary>Rate-limit window; required when <see cref="LimitAlerts"/> is set.</summary>
    public TimeSpan? LimitPer { get; init; }
}
