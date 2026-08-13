using System.Text.Json;
using Dapper;
using DeltaZulu.Platform.Domain.AgentManagement.Contracts;
using DeltaZulu.Platform.Domain.AgentManagement.Enums;
using DeltaZulu.Platform.Domain.AgentManagement.Identifiers;
using DeltaZulu.Platform.Domain.AgentManagement.Profiles;
using DeltaZulu.Platform.Domain.AgentManagement.ValueObjects;

namespace DeltaZulu.Platform.Data.Sqlite.AgentManagement.Repositories;

internal sealed class ResourceProfileVersionRepository(AgentManagementDapperSession session)
    : IResourceProfileVersionRepository
{
    public async Task<ResourceProfileVersion?> GetByIdAsync(ProfileVersionId id, CancellationToken ct = default)
    {
        var row = await session.Connection.QuerySingleOrDefaultAsync<Row>(
            "SELECT * FROM resource_profile_versions WHERE id = @Id",
            new { Id = id.Value.ToString() },
            session.Transaction);
        return row?.ToDomain();
    }

    public async Task<IReadOnlyList<ResourceProfileVersion>> ListByProfileIdAsync(
        ResourceProfileId profileId, CancellationToken ct = default)
    {
        var rows = await session.Connection.QueryAsync<Row>(
            "SELECT * FROM resource_profile_versions WHERE profile_id = @ProfileId ORDER BY sequence_number DESC",
            new { ProfileId = profileId.Value.ToString() },
            session.Transaction);
        return rows.Select(r => r.ToDomain()).ToList();
    }

    public async Task<ResourceProfileVersion?> GetLatestPublishedAsync(
        ResourceProfileId profileId, CancellationToken ct = default)
    {
        var row = await session.Connection.QuerySingleOrDefaultAsync<Row>(
            """
            SELECT * FROM resource_profile_versions
            WHERE profile_id = @ProfileId AND state = 'Published'
            ORDER BY sequence_number DESC LIMIT 1
            """,
            new { ProfileId = profileId.Value.ToString() },
            session.Transaction);
        return row?.ToDomain();
    }

    public void Add(ResourceProfileVersion version) => session.Connection.Execute("""
        INSERT INTO resource_profile_versions
            (id, profile_id, sequence_number, display_version, schema_version, state, enabled, mandatory,
             resource_descriptor_json, input_contract_json, output_contract_json, kql_filter_json,
             host_conditions_json, content_hash, author, created_at, updated_at)
        VALUES
            (@Id, @ProfileId, @SequenceNumber, @DisplayVersion, @SchemaVersion, @State, @Enabled, @Mandatory,
             @ResourceDescriptorJson, @InputContractJson, @OutputContractJson, @KqlFilterJson,
             @HostConditionsJson, @ContentHash, @Author, @CreatedAt, @UpdatedAt)
        """,
        ToParams(version),
        session.Transaction);

    public void Save(ResourceProfileVersion version) => session.Connection.Execute("""
        UPDATE resource_profile_versions
        SET state = @State, updated_at = @UpdatedAt
        WHERE id = @Id
        """,
        ToParams(version),
        session.Transaction);

    private static object ToParams(ResourceProfileVersion v) => new
    {
        Id = v.Id.Value.ToString(),
        ProfileId = v.ProfileId.Value.ToString(),
        v.SequenceNumber,
        v.DisplayVersion,
        v.SchemaVersion,
        State = v.State.ToString(),
        Enabled = v.Enabled ? 1 : 0,
        Mandatory = v.Mandatory ? 1 : 0,
        ResourceDescriptorJson = JsonSerializer.Serialize(v.ResourceDescriptor),
        InputContractJson = JsonSerializer.Serialize(v.InputContract),
        OutputContractJson = JsonSerializer.Serialize(v.OutputContract),
        KqlFilterJson = v.KqlFilter is not null ? JsonSerializer.Serialize(v.KqlFilter) : null,
        HostConditionsJson = JsonSerializer.Serialize(v.HostConditions),
        v.ContentHash,
        v.Author,
        CreatedAt = v.CreatedAt.ToString("O"),
        UpdatedAt = v.UpdatedAt.ToString("O"),
    };

    internal sealed class Row
    {
        public string id { get; set; } = "";
        public string profile_id { get; set; } = "";
        public int sequence_number { get; set; }
        public string display_version { get; set; } = "";
        public string schema_version { get; set; } = "";
        public string state { get; set; } = "";
        public int enabled { get; set; }
        public int mandatory { get; set; }
        public string resource_descriptor_json { get; set; } = "";
        public string input_contract_json { get; set; } = "";
        public string output_contract_json { get; set; } = "";
        public string? kql_filter_json { get; set; }
        public string host_conditions_json { get; set; } = "[]";
        public string content_hash { get; set; } = "";
        public string? author { get; set; }
        public string created_at { get; set; } = "";
        public string updated_at { get; set; } = "";

        public ResourceProfileVersion ToDomain() => ResourceProfileVersion.Reconstitute(
            new ProfileVersionId(Guid.Parse(id)),
            new ResourceProfileId(Guid.Parse(profile_id)),
            sequence_number,
            schema_version,
            Enum.Parse<ProfileState>(state),
            enabled != 0,
            mandatory != 0,
            JsonSerializer.Deserialize<ResourceDescriptor>(resource_descriptor_json)!,
            JsonSerializer.Deserialize<InputContract>(input_contract_json)!,
            JsonSerializer.Deserialize<OutputContract>(output_contract_json)!,
            kql_filter_json is not null ? JsonSerializer.Deserialize<KqlFilterDefinition>(kql_filter_json) : null,
            JsonSerializer.Deserialize<List<HostCondition>>(host_conditions_json) ?? [],
            content_hash,
            author,
            DateTimeOffset.Parse(created_at),
            DateTimeOffset.Parse(updated_at));
    }
}
