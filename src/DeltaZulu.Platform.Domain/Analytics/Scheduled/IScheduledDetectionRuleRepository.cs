namespace DeltaZulu.Platform.Domain.Analytics.Scheduled;

public interface IScheduledDetectionRuleRepository
{
    Task<IReadOnlyList<ScheduledDetectionRule>> ListAsync(CancellationToken ct = default);
    Task<ScheduledDetectionRule?> GetAsync(string id, CancellationToken ct = default);
    Task SaveAsync(ScheduledDetectionRule rule, CancellationToken ct = default);
    Task DeleteAsync(string id, CancellationToken ct = default);
}
