namespace DeltaZulu.Platform.Domain.Analytics.Observability;

public interface ISourceObservationRepository
{
    Task<IReadOnlyList<SourceObservationSnapshot>> ListLatestAsync(CancellationToken cancellationToken = default);

    Task UpsertAsync(SourceObservationSnapshot snapshot, CancellationToken cancellationToken = default);
}
