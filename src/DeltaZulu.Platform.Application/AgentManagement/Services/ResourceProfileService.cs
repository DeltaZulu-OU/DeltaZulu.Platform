using DeltaZulu.Platform.Application.AgentManagement.Validation;
using DeltaZulu.Platform.Domain.AgentManagement.Contracts;
using DeltaZulu.Platform.Domain.AgentManagement.Enums;
using DeltaZulu.Platform.Domain.AgentManagement.Identifiers;
using DeltaZulu.Platform.Domain.AgentManagement.Profiles;
using DeltaZulu.Platform.Domain.AgentManagement.ValueObjects;
using DeltaZulu.Platform.Domain.Common;

namespace DeltaZulu.Platform.Application.AgentManagement.Services;

public sealed class ResourceProfileService(
    IResourceProfileRepository profileRepo,
    IResourceProfileVersionRepository versionRepo,
    IAgentManagementUnitOfWork unitOfWork,
    TimeProvider timeProvider,
    ProfileValidationPipelineRunner validationRunner,
    IEnumerable<IProfileValidationCheck> validationChecks)
{
    public async Task<ResourceProfile> CreateProfileAsync(
        TenantId tenantId, string name, ProfileOrigin origin, CancellationToken ct = default)
    {
        var now = timeProvider.GetUtcNow();
        var profile = ResourceProfile.CreateDraft(ResourceProfileId.New(), tenantId, name, origin, now);
        profileRepo.Add(profile);
        await unitOfWork.SaveChangesAsync(ct);
        return profile;
    }

    public async Task<ResourceProfile?> GetByIdAsync(ResourceProfileId id, CancellationToken ct = default) =>
        await profileRepo.GetByIdAsync(id, ct);

    public async Task<IReadOnlyList<ResourceProfile>> ListByTenantAsync(TenantId tenantId, CancellationToken ct = default) =>
        await profileRepo.ListByTenantAsync(tenantId, ct);

    public async Task RenameAsync(ResourceProfileId id, string newName, CancellationToken ct = default)
    {
        var profile = await profileRepo.GetByIdAsync(id, ct)
            ?? throw new DomainException("profile.not_found", $"Profile {id} not found.");

        var now = timeProvider.GetUtcNow();
        profile.Rename(newName, now);
        profileRepo.Save(profile);
        await unitOfWork.SaveChangesAsync(ct);
    }

    public async Task<ResourceProfileVersion> CreateVersionAsync(
        ResourceProfileId profileId, int sequenceNumber, string schemaVersion,
        bool enabled, bool mandatory, ResourceDescriptor resourceDescriptor,
        InputContract inputContract, OutputContract outputContract,
        KqlFilterDefinition? kqlFilter, IReadOnlyList<HostCondition> hostConditions,
        string contentHash, string? author, CancellationToken ct = default)
    {
        _ = await profileRepo.GetByIdAsync(profileId, ct)
            ?? throw new DomainException("profile.not_found", $"Profile {profileId} not found.");

        var now = timeProvider.GetUtcNow();
        var version = ResourceProfileVersion.CreateDraft(
            ProfileVersionId.New(), profileId, sequenceNumber, schemaVersion,
            enabled, mandatory, resourceDescriptor, inputContract, outputContract,
            kqlFilter, hostConditions, contentHash, author, now);

        versionRepo.Add(version);
        await unitOfWork.SaveChangesAsync(ct);
        return version;
    }

    public async Task MarkValidatedAsync(ProfileVersionId id, CancellationToken ct = default)
    {
        var version = await versionRepo.GetByIdAsync(id, ct)
            ?? throw new DomainException("profileversion.not_found", $"Profile version {id} not found.");

        var outcomes = await validationRunner.RunAllAsync(new ProfileValidationContext(version), ct);
        if (ProfileValidationPipelineRunner.HasBlockingFailures(outcomes, validationChecks))
        {
            var failures = outcomes
                .SelectMany(o => o.Findings.Where(f => f.IsBlocking).Select(f => $"{o.CheckName}: {f.Message}"))
                .ToList();
            throw new DomainException("profileversion.validation_failed",
                $"Profile version failed validation: {string.Join(" | ", failures)}");
        }

        var now = timeProvider.GetUtcNow();
        version.MarkValidated(now);
        versionRepo.Save(version);
        await unitOfWork.SaveChangesAsync(ct);
    }

    public async Task PublishAsync(ProfileVersionId id, CancellationToken ct = default)
    {
        var version = await versionRepo.GetByIdAsync(id, ct)
            ?? throw new DomainException("profileversion.not_found", $"Profile version {id} not found.");

        var now = timeProvider.GetUtcNow();
        version.Publish(now);
        versionRepo.Save(version);
        await unitOfWork.SaveChangesAsync(ct);
    }

    public async Task DeprecateAsync(ProfileVersionId id, CancellationToken ct = default)
    {
        var version = await versionRepo.GetByIdAsync(id, ct)
            ?? throw new DomainException("profileversion.not_found", $"Profile version {id} not found.");

        var now = timeProvider.GetUtcNow();
        version.Deprecate(now);
        versionRepo.Save(version);
        await unitOfWork.SaveChangesAsync(ct);
    }

    public async Task ArchiveAsync(ProfileVersionId id, CancellationToken ct = default)
    {
        var version = await versionRepo.GetByIdAsync(id, ct)
            ?? throw new DomainException("profileversion.not_found", $"Profile version {id} not found.");

        var now = timeProvider.GetUtcNow();
        version.Archive(now);
        versionRepo.Save(version);
        await unitOfWork.SaveChangesAsync(ct);
    }

    public async Task<ResourceProfileVersion> CloneVersionAsync(
        ProfileVersionId sourceVersionId, ResourceProfileId targetProfileId, CancellationToken ct = default)
    {
        var source = await versionRepo.GetByIdAsync(sourceVersionId, ct)
            ?? throw new DomainException("profileversion.not_found", $"Profile version {sourceVersionId} not found.");

        var now = timeProvider.GetUtcNow();
        var clone = source.CloneAsDraft(ProfileVersionId.New(), targetProfileId, now);
        versionRepo.Add(clone);
        await unitOfWork.SaveChangesAsync(ct);
        return clone;
    }

    public async Task<IReadOnlyList<ResourceProfileVersion>> ListVersionsAsync(
        ResourceProfileId profileId, CancellationToken ct = default) =>
        await versionRepo.ListByProfileIdAsync(profileId, ct);

    public async Task<ResourceProfileVersion?> GetLatestPublishedVersionAsync(
        ResourceProfileId profileId, CancellationToken ct = default) =>
        await versionRepo.GetLatestPublishedAsync(profileId, ct);
}
