using DeltaZulu.Platform.Domain.Workbench.Changes;
using DeltaZulu.Platform.Domain.Workbench.Identifiers;

namespace DeltaZulu.Platform.Application.Workbench.Abstractions;

/// <summary>
/// Persistence port for <see cref="ChangeRequest"/> aggregates. Implementations must load
/// sub-entities (draft files, checks, reviews) for invariant evaluation.
/// </summary>
public interface IChangeRequestRepository
{
    Task<ChangeRequest?> GetByIdAsync(ChangeRequestId id, CancellationToken ct = default);

    Task<IReadOnlyList<ChangeRequest>> ListByDetectionAsync(DetectionId detectionId, CancellationToken ct = default);

    Task<IReadOnlyList<ChangeRequest>> ListAsync(CancellationToken ct = default);

    Task<IReadOnlyList<ChangeRequest>> ListOpenByAuthorAsync(UserId authorId, CancellationToken ct = default);

    Task<IReadOnlyList<ChangeRequest>> ListAwaitingReviewAsync(UserId excludeAuthorId, CancellationToken ct = default);

    Task<IReadOnlyList<ChangeRequest>> ListWithFailedBlockingChecksAsync(CancellationToken ct = default);

    void Add(ChangeRequest change);

    /// <summary>
    /// Persists the full aggregate state including sub-entities (draft files, checks, reviews).
    /// Called after domain mutations on a loaded aggregate.
    /// </summary>
    void Save(ChangeRequest change);
}