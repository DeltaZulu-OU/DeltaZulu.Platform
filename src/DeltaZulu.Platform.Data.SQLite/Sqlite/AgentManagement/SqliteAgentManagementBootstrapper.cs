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

        """;
}
