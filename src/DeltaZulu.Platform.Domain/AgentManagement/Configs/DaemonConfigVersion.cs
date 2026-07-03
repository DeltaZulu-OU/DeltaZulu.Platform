using DeltaZulu.Platform.Domain.AgentManagement.Enums;
using DeltaZulu.Platform.Domain.AgentManagement.Identifiers;
using DeltaZulu.Platform.Domain.AgentManagement.ValueObjects;
using DeltaZulu.Platform.Domain.Common;

namespace DeltaZulu.Platform.Domain.AgentManagement.Configs;

public sealed class DaemonConfigVersion : Entity<ConfigVersionId>
{
    public ConfigPolicyId ConfigPolicyId { get; }
    public int SequenceNumber { get; }
    public ProfileState State { get; private set; }
    public PipelineConfig Pipeline { get; }
    public BufferConfig Buffer { get; }
    public RelpConfig Relp { get; }
    public TlsConfig Tls { get; }
    public DiagnosticsConfig Diagnostics { get; }
    public string ProfilesPath { get; }
    public string ContentHash { get; }
    public string? Author { get; }
    public DateTimeOffset CreatedAt { get; }
    public DateTimeOffset UpdatedAt { get; private set; }

    private DaemonConfigVersion(
        ConfigVersionId id, ConfigPolicyId configPolicyId, int sequenceNumber,
        PipelineConfig pipeline, BufferConfig buffer, RelpConfig relp,
        TlsConfig tls, DiagnosticsConfig diagnostics, string profilesPath,
        string contentHash, string? author, DateTimeOffset createdAt)
        : base(id)
    {
        ConfigPolicyId = configPolicyId;
        SequenceNumber = sequenceNumber;
        State = ProfileState.Draft;
        Pipeline = pipeline;
        Buffer = buffer;
        Relp = relp;
        Tls = tls;
        Diagnostics = diagnostics;
        ProfilesPath = profilesPath;
        ContentHash = contentHash;
        Author = author;
        CreatedAt = createdAt;
        UpdatedAt = createdAt;
    }

    public static DaemonConfigVersion Create(
        ConfigVersionId id, ConfigPolicyId configPolicyId, int sequenceNumber,
        PipelineConfig pipeline, BufferConfig buffer, RelpConfig relp,
        TlsConfig tls, DiagnosticsConfig diagnostics, string profilesPath,
        string contentHash, string? author, DateTimeOffset now)
    {
        if (sequenceNumber < 1)
            throw new DomainException("configversion.sequence_invalid",
                "Config version sequence number must be 1 or greater.");

        ArgumentNullException.ThrowIfNull(pipeline);
        ArgumentNullException.ThrowIfNull(buffer);
        ArgumentNullException.ThrowIfNull(relp);
        ArgumentNullException.ThrowIfNull(tls);
        ArgumentNullException.ThrowIfNull(diagnostics);

        if (string.IsNullOrWhiteSpace(contentHash))
            throw new DomainException("configversion.content_hash_empty",
                "Content hash must not be empty.");

        if (pipeline.OutputMode == PipelineOutputMode.Forward && relp.Endpoints.Count == 0)
            throw new DomainException("configversion.pipe005_forward_requires_relp",
                "Forward output mode requires at least one RELP endpoint.");

        if (pipeline.OutputMode == PipelineOutputMode.File && string.IsNullOrWhiteSpace(pipeline.FilePath))
            throw new DomainException("configversion.pipe006_file_requires_path",
                "File output mode requires a file path.");

        return new DaemonConfigVersion(id, configPolicyId, sequenceNumber,
            pipeline, buffer, relp, tls, diagnostics, profilesPath,
            contentHash, author, now);
    }

    public static DaemonConfigVersion Reconstitute(
        ConfigVersionId id, ConfigPolicyId configPolicyId, int sequenceNumber,
        ProfileState state, PipelineConfig pipeline, BufferConfig buffer,
        RelpConfig relp, TlsConfig tls, DiagnosticsConfig diagnostics,
        string profilesPath, string contentHash, string? author,
        DateTimeOffset createdAt, DateTimeOffset updatedAt) =>
        new(id, configPolicyId, sequenceNumber, pipeline, buffer, relp,
            tls, diagnostics, profilesPath, contentHash, author, createdAt)
        { State = state, UpdatedAt = updatedAt };

    public void MarkValidated(DateTimeOffset now)
    {
        if (State != ProfileState.Draft)
            throw new DomainException("configversion.invalid_transition",
                $"Cannot transition from {State} to {ProfileState.Validated}.");

        State = ProfileState.Validated;
        UpdatedAt = now;
    }

    public void Publish(DateTimeOffset now)
    {
        if (State != ProfileState.Validated)
            throw new DomainException("configversion.invalid_transition",
                $"Cannot transition from {State} to {ProfileState.Published}.");

        State = ProfileState.Published;
        UpdatedAt = now;
    }
}
