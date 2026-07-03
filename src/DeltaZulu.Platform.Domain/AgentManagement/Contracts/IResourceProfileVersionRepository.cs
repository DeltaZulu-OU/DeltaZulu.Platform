using DeltaZulu.Platform.Domain.AgentManagement.Identifiers;
using DeltaZulu.Platform.Domain.AgentManagement.Profiles;

namespace DeltaZulu.Platform.Domain.AgentManagement.Contracts;

public interface IResourceProfileVersionRepository
{
    Task<ResourceProfileVersion?> GetByIdAsync(ProfileVersionId id, CancellationToken ct = default);

    Task<IReadOnlyList<ResourceProfileVersion>> ListByProfileIdAsync(ResourceProfileId profileId, CancellationToken ct = default);

    Task<ResourceProfileVersion?> GetLatestPublishedAsync(ResourceProfileId profileId, CancellationToken ct = default);

    void Add(ResourceProfileVersion version);

    void Save(ResourceProfileVersion version);
}
