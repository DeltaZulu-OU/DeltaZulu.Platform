using Dapper;
using DeltaZulu.Platform.Data.AgentManagement;
using Microsoft.Data.Sqlite;

namespace DeltaZulu.Platform.Data.Sqlite.AgentManagement;

public sealed class SqliteAgentManagementBootstrapper(string connectionString)
    : IAgentManagementPersistenceBootstrapper
{
    public Task EnsureInitializedAsync(CancellationToken cancellationToken = default)
    {
        using var conn = new SqliteConnection(connectionString);
        conn.Open();
        conn.Execute(Schema);
        return Task.CompletedTask;
    }

    private const string Schema = """
        CREATE TABLE IF NOT EXISTS agents (
            id                  TEXT PRIMARY KEY,
            tenant_id           TEXT NOT NULL,
            hostname            TEXT NOT NULL,
            platform            TEXT NOT NULL,
            tags                TEXT NOT NULL DEFAULT '',
            agent_version       TEXT,
            status              TEXT NOT NULL DEFAULT 'Online',
            current_bundle_id   TEXT,
            desired_bundle_id   TEXT,
            last_seen_at        TEXT,
            created_at          TEXT NOT NULL,
            updated_at          TEXT NOT NULL
        );

        CREATE UNIQUE INDEX IF NOT EXISTS ix_agents_tenant_hostname
            ON agents (tenant_id, hostname);

        CREATE TABLE IF NOT EXISTS agent_groups (
            id                  TEXT PRIMARY KEY,
            tenant_id           TEXT NOT NULL,
            name                TEXT NOT NULL,
            selectors_json      TEXT,
            created_at          TEXT NOT NULL,
            updated_at          TEXT NOT NULL,
            UNIQUE(tenant_id, name)
        );

        CREATE TABLE IF NOT EXISTS resource_profiles (
            id                  TEXT PRIMARY KEY,
            tenant_id           TEXT NOT NULL,
            name                TEXT NOT NULL,
            origin              TEXT NOT NULL,
            enabled             INTEGER NOT NULL DEFAULT 1,
            created_at          TEXT NOT NULL,
            updated_at          TEXT NOT NULL
        );

        CREATE TABLE IF NOT EXISTS resource_profile_versions (
            id                      TEXT PRIMARY KEY,
            profile_id              TEXT NOT NULL,
            sequence_number         INTEGER NOT NULL,
            display_version         TEXT NOT NULL,
            schema_version          TEXT NOT NULL,
            state                   TEXT NOT NULL DEFAULT 'Draft',
            enabled                 INTEGER NOT NULL DEFAULT 1,
            mandatory               INTEGER NOT NULL DEFAULT 0,
            resource_descriptor_json TEXT NOT NULL,
            input_contract_json     TEXT NOT NULL,
            output_contract_json    TEXT NOT NULL,
            kql_filter_json         TEXT,
            host_conditions_json    TEXT NOT NULL DEFAULT '[]',
            content_hash            TEXT NOT NULL,
            author                  TEXT,
            created_at              TEXT NOT NULL,
            updated_at              TEXT NOT NULL,
            UNIQUE(profile_id, sequence_number)
        );

        CREATE TABLE IF NOT EXISTS daemon_config_policies (
            id                  TEXT PRIMARY KEY,
            tenant_id           TEXT NOT NULL,
            name                TEXT NOT NULL,
            created_at          TEXT NOT NULL,
            updated_at          TEXT NOT NULL
        );

        CREATE TABLE IF NOT EXISTS daemon_config_versions (
            id                      TEXT PRIMARY KEY,
            config_policy_id        TEXT NOT NULL,
            sequence_number         INTEGER NOT NULL,
            state                   TEXT NOT NULL DEFAULT 'Draft',
            pipeline_json           TEXT NOT NULL,
            buffer_json             TEXT NOT NULL,
            relp_json               TEXT NOT NULL,
            tls_json                TEXT NOT NULL,
            diagnostics_json        TEXT NOT NULL,
            profiles_path           TEXT NOT NULL,
            content_hash            TEXT NOT NULL,
            author                  TEXT,
            created_at              TEXT NOT NULL,
            updated_at              TEXT NOT NULL,
            UNIQUE(config_policy_id, sequence_number)
        );

        CREATE TABLE IF NOT EXISTS policy_assignments (
            id                  TEXT PRIMARY KEY,
            tenant_id           TEXT NOT NULL,
            scope_type          TEXT NOT NULL,
            scope_id            TEXT NOT NULL,
            profile_ids_json    TEXT NOT NULL DEFAULT '[]',
            config_policy_id    TEXT,
            precedence          INTEGER NOT NULL DEFAULT 0,
            created_at          TEXT NOT NULL,
            updated_at          TEXT NOT NULL
        );

        CREATE TABLE IF NOT EXISTS enrollment_tokens (
            id            TEXT PRIMARY KEY,
            tenant_id     TEXT NOT NULL,
            name          TEXT NOT NULL,
            token_hash    TEXT NOT NULL,
            expires_at    TEXT NOT NULL,
            max_uses      INTEGER NOT NULL,
            use_count     INTEGER NOT NULL DEFAULT 0,
            created_by    TEXT,
            revoked_at    TEXT,
            created_at    TEXT NOT NULL,
            updated_at    TEXT NOT NULL
        );

        CREATE UNIQUE INDEX IF NOT EXISTS ix_enrollment_tokens_hash
            ON enrollment_tokens (token_hash);

        CREATE TABLE IF NOT EXISTS agent_credentials (
            agent_id               TEXT PRIMARY KEY,
            secret_hash            TEXT NOT NULL,
            certificate_thumbprint TEXT,
            created_at             TEXT NOT NULL,
            rotated_at             TEXT
        );

        CREATE UNIQUE INDEX IF NOT EXISTS ix_agent_credentials_secret_hash
            ON agent_credentials (secret_hash);

        CREATE TABLE IF NOT EXISTS agent_group_members (
            group_id  TEXT NOT NULL,
            agent_id  TEXT NOT NULL,
            added_at  TEXT NOT NULL,
            PRIMARY KEY (group_id, agent_id)
        );

        CREATE INDEX IF NOT EXISTS ix_agent_group_members_agent
            ON agent_group_members (agent_id);

        CREATE TABLE IF NOT EXISTS policy_bundles (
            id                        TEXT PRIMARY KEY,
            tenant_id                 TEXT NOT NULL,
            agent_id                  TEXT NOT NULL,
            content_hash              TEXT NOT NULL,
            document_json             TEXT NOT NULL,
            assignment_ids_json       TEXT NOT NULL DEFAULT '[]',
            profile_version_ids_json  TEXT NOT NULL DEFAULT '[]',
            config_version_id         TEXT,
            created_at                TEXT NOT NULL,
            UNIQUE (agent_id, content_hash)
        );

        CREATE TABLE IF NOT EXISTS bundle_acks (
            id        TEXT PRIMARY KEY,
            agent_id  TEXT NOT NULL,
            bundle_id TEXT NOT NULL,
            status    TEXT NOT NULL,
            error     TEXT,
            acked_at  TEXT NOT NULL
        );

        CREATE INDEX IF NOT EXISTS ix_bundle_acks_agent
            ON bundle_acks (agent_id, acked_at);

        CREATE TABLE IF NOT EXISTS agent_commands (
            id              TEXT PRIMARY KEY,
            tenant_id       TEXT NOT NULL,
            agent_id        TEXT NOT NULL,
            type            TEXT NOT NULL,
            status          TEXT NOT NULL DEFAULT 'Pending',
            requested_by    TEXT,
            timeout_seconds INTEGER NOT NULL,
            requested_at    TEXT NOT NULL,
            delivered_at    TEXT,
            completed_at    TEXT,
            result_json     TEXT,
            error           TEXT,
            updated_at      TEXT NOT NULL
        );

        CREATE INDEX IF NOT EXISTS ix_agent_commands_agent_status
            ON agent_commands (agent_id, status);

        CREATE TABLE IF NOT EXISTS policy_assignment_pins (
            assignment_id      TEXT NOT NULL,
            kind               TEXT NOT NULL,
            target_id          TEXT NOT NULL,
            pinned_version_id  TEXT NOT NULL,
            PRIMARY KEY (assignment_id, kind, target_id)
        );

        """;
}
