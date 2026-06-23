namespace DeltaZulu.Platform.Domain.Analytics.Nrt;

public interface INrtRuleRepository
{
    Task<IReadOnlyList<NrtRule>> ListAsync(CancellationToken cancellationToken = default);
    Task<NrtRule?> GetAsync(string id, CancellationToken cancellationToken = default);
    Task SaveAsync(NrtRule rule, CancellationToken cancellationToken = default);
    Task DeleteAsync(string id, CancellationToken cancellationToken = default);
}
