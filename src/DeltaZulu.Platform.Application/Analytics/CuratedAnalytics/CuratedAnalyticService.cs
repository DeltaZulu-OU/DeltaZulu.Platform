using DeltaZulu.Platform.Domain.Analytics.CuratedAnalytics;
using DeltaZulu.Platform.Domain.Analytics.SavedQueries;

namespace DeltaZulu.Platform.Application.Analytics.CuratedAnalytics;

public sealed class CuratedAnalyticService(
    ICuratedAnalyticRepository curatedAnalytics,
    ISavedQueryRepository savedQueries)
{
    public async Task<CuratedAnalyticRecord> PromoteFromSavedQueryAsync(
        string savedQueryId,
        CuratedAnalyticPurpose purpose,
        DateTime now,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(savedQueryId);

        var savedQuery = await savedQueries.GetAsync(savedQueryId, ct)
            ?? throw new InvalidOperationException($"Saved query '{savedQueryId}' not found.");

        var record = new CuratedAnalyticRecord(
            Id: Guid.NewGuid().ToString("N"),
            Name: savedQuery.Name,
            Description: savedQuery.Description,
            QueryText: savedQuery.QueryText,
            Purpose: purpose,
            RequiredViews: null,
            RequiredFields: null,
            ExpectedResultShape: null,
            EntityMappingsJson: null,
            KnownFalsePositives: null,
            SeverityHint: null,
            ConfidenceHint: null,
            RiskHint: null,
            Notes: null,
            PromotedToDetectionSlug: null,
            CreatedAt: now,
            UpdatedAt: now,
            LastRunAt: savedQuery.LastRunAt);

        await curatedAnalytics.SaveAsync(record, ct);
        return record;
    }
}
