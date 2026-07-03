using DeltaZulu.Platform.Domain.AgentManagement.Enums;
using DeltaZulu.Platform.Domain.AgentManagement.Identifiers;
using DeltaZulu.Platform.Domain.AgentManagement.ValueObjects;
using DeltaZulu.Platform.Domain.Common;

namespace DeltaZulu.Platform.Domain.AgentManagement.Profiles;

public sealed class ResourceProfileVersion : Entity<ProfileVersionId>
{
    public ResourceProfileId ProfileId { get; }
    public int SequenceNumber { get; }
    public string DisplayVersion { get; }
    public string SchemaVersion { get; }
    public ProfileState State { get; private set; }
    public bool Enabled { get; }
    public bool Mandatory { get; }
    public ResourceDescriptor ResourceDescriptor { get; }
    public InputContract InputContract { get; }
    public OutputContract OutputContract { get; }
    public KqlFilterDefinition? KqlFilter { get; }
    public IReadOnlyList<HostCondition> HostConditions { get; }
    public string ContentHash { get; }
    public string? Author { get; }
    public DateTimeOffset CreatedAt { get; }
    public DateTimeOffset UpdatedAt { get; private set; }

    private ResourceProfileVersion(
        ProfileVersionId id, ResourceProfileId profileId, int sequenceNumber,
        string schemaVersion, bool enabled, bool mandatory,
        ResourceDescriptor resourceDescriptor, InputContract inputContract,
        OutputContract outputContract, KqlFilterDefinition? kqlFilter,
        IReadOnlyList<HostCondition> hostConditions, string contentHash,
        string? author, DateTimeOffset createdAt)
        : base(id)
    {
        ProfileId = profileId;
        SequenceNumber = sequenceNumber;
        DisplayVersion = $"v{sequenceNumber}";
        SchemaVersion = schemaVersion;
        State = ProfileState.Draft;
        Enabled = enabled;
        Mandatory = mandatory;
        ResourceDescriptor = resourceDescriptor;
        InputContract = inputContract;
        OutputContract = outputContract;
        KqlFilter = kqlFilter;
        HostConditions = hostConditions;
        ContentHash = contentHash;
        Author = author;
        CreatedAt = createdAt;
        UpdatedAt = createdAt;
    }

    public static ResourceProfileVersion CreateDraft(
        ProfileVersionId id, ResourceProfileId profileId, int sequenceNumber,
        string schemaVersion, bool enabled, bool mandatory,
        ResourceDescriptor resourceDescriptor, InputContract inputContract,
        OutputContract outputContract, KqlFilterDefinition? kqlFilter,
        IReadOnlyList<HostCondition> hostConditions, string contentHash,
        string? author, DateTimeOffset now)
    {
        if (sequenceNumber < 1)
            throw new DomainException("profileversion.sequence_invalid",
                "Profile version sequence number must be 1 or greater.");

        ArgumentNullException.ThrowIfNull(resourceDescriptor);
        ArgumentNullException.ThrowIfNull(inputContract);
        ArgumentNullException.ThrowIfNull(outputContract);
        ArgumentNullException.ThrowIfNull(hostConditions);

        if (string.IsNullOrWhiteSpace(schemaVersion))
            throw new DomainException("profileversion.schema_version_empty",
                "Schema version must not be empty.");

        if (string.IsNullOrWhiteSpace(contentHash))
            throw new DomainException("profileversion.content_hash_empty",
                "Content hash must not be empty.");

        return new ResourceProfileVersion(id, profileId, sequenceNumber,
            schemaVersion, enabled, mandatory, resourceDescriptor, inputContract,
            outputContract, kqlFilter, hostConditions, contentHash, author, now);
    }

    public static ResourceProfileVersion Reconstitute(
        ProfileVersionId id, ResourceProfileId profileId, int sequenceNumber,
        string schemaVersion, ProfileState state, bool enabled, bool mandatory,
        ResourceDescriptor resourceDescriptor, InputContract inputContract,
        OutputContract outputContract, KqlFilterDefinition? kqlFilter,
        IReadOnlyList<HostCondition> hostConditions, string contentHash,
        string? author, DateTimeOffset createdAt, DateTimeOffset updatedAt) =>
        new(id, profileId, sequenceNumber, schemaVersion, enabled, mandatory,
            resourceDescriptor, inputContract, outputContract, kqlFilter,
            hostConditions, contentHash, author, createdAt)
        { State = state, UpdatedAt = updatedAt };

    public void MarkValidated(DateTimeOffset now)
    {
        if (State != ProfileState.Draft)
            throw new DomainException("profileversion.invalid_transition",
                $"Cannot transition from {State} to {ProfileState.Validated}.");

        State = ProfileState.Validated;
        UpdatedAt = now;
    }

    public void Publish(DateTimeOffset now)
    {
        if (State != ProfileState.Validated)
            throw new DomainException("profileversion.invalid_transition",
                $"Cannot transition from {State} to {ProfileState.Published}.");

        State = ProfileState.Published;
        UpdatedAt = now;
    }

    public void Deprecate(DateTimeOffset now)
    {
        if (State != ProfileState.Published)
            throw new DomainException("profileversion.invalid_transition",
                $"Cannot transition from {State} to {ProfileState.Deprecated}.");

        State = ProfileState.Deprecated;
        UpdatedAt = now;
    }

    public void Archive(DateTimeOffset now)
    {
        if (State != ProfileState.Deprecated)
            throw new DomainException("profileversion.invalid_transition",
                $"Cannot transition from {State} to {ProfileState.Archived}.");

        State = ProfileState.Archived;
        UpdatedAt = now;
    }

    public ResourceProfileVersion CloneAsDraft(
        ProfileVersionId newId, ResourceProfileId targetProfileId, DateTimeOffset now) =>
        new(newId, targetProfileId, 1, SchemaVersion, Enabled, Mandatory,
            ResourceDescriptor, InputContract, OutputContract, KqlFilter,
            HostConditions, ContentHash, Author, now);
}
