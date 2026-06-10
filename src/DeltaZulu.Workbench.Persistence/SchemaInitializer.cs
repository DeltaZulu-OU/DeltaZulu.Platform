using Dapper;
using Microsoft.Data.Sqlite;

namespace DeltaZulu.Workbench.Persistence;

/// <summary>
/// Creates the SQLite schema. Called once at application startup. Idempotent (IF NOT EXISTS).
/// </summary>
public static class SchemaInitializer
{
    public static void Initialize(string connectionString)
    {
        using var conn = new SqliteConnection(connectionString);
        conn.Open();
        conn.Execute(Schema);
        MigrateExistingData(conn);
    }

    private static void MigrateExistingData(SqliteConnection conn)
    {
        // Add columns to issues table that may not exist in older databases. SQLite does
        // not support ALTER TABLE ... ADD COLUMN IF NOT EXISTS, so check table_info first.
        AddColumnIfMissing(conn, "issues", "ext_case_system_type", "INTEGER NOT NULL DEFAULT 0");
        AddColumnIfMissing(conn, "issues", "description", "TEXT");
        AddColumnIfMissing(conn, "issues", "acceptance_criteria", "TEXT");
        AddColumnIfMissing(conn, "issues", "data_source", "TEXT");
        AddColumnIfMissing(conn, "issues", "platform", "TEXT");
        AddColumnIfMissing(conn, "issues", "attack_technique_id", "TEXT");
        AddColumnIfMissing(conn, "issues", "tlp", "TEXT");
        AddColumnIfMissing(conn, "issues", "labels", "TEXT NOT NULL DEFAULT ''");

        // Migrate detection lifecycle values from the old pre-acceptance name to Draft.
        conn.Execute("UPDATE detections SET lifecycle = 'Draft' WHERE lifecycle = 'Conceived'");

        // Migrate issue status values from the old 5-state enum to the 13-state machine.
        conn.Execute("UPDATE issues SET status = 'New'    WHERE status = 'Open'");
        conn.Execute("UPDATE issues SET status = 'Merged' WHERE status = 'Resolved'");
        // InProgress, Blocked, Closed retain the same string names.
    }

    private static void AddColumnIfMissing(
        SqliteConnection conn,
        string tableName,
        string columnName,
        string columnDefinition)
    {
        var columns = conn.Query<SqliteColumnInfo>($"PRAGMA table_info({tableName})");
        if (columns.Any(c => string.Equals(c.name, columnName, StringComparison.OrdinalIgnoreCase)))
            return;

        conn.Execute($"ALTER TABLE {tableName} ADD COLUMN {columnName} {columnDefinition}");
    }

    private sealed class SqliteColumnInfo
    {
        public string name { get; set; } = "";
    }

    private const string Schema = """
        CREATE TABLE IF NOT EXISTS detections (
            id              TEXT PRIMARY KEY,
            slug            TEXT NOT NULL UNIQUE,
            title           TEXT NOT NULL,
            summary         TEXT NOT NULL DEFAULT '',
            lifecycle       TEXT NOT NULL DEFAULT 'Draft',
            current_version_id TEXT,
            created_at      TEXT NOT NULL,
            updated_at      TEXT NOT NULL
        );

        CREATE TABLE IF NOT EXISTS detection_versions (
            id                      TEXT PRIMARY KEY,
            detection_id            TEXT NOT NULL,
            sequence_number         INTEGER NOT NULL,
            display_version         TEXT NOT NULL,
            title                   TEXT NOT NULL,
            change_summary          TEXT NOT NULL,
            author_id               TEXT NOT NULL,
            workflow_profile        TEXT NOT NULL,
            source_change_request_id TEXT NOT NULL,
            linked_issue_id         TEXT,
            accepted_at             TEXT NOT NULL,
            changed_sections        TEXT NOT NULL DEFAULT '',
            git_commit_sha          TEXT NOT NULL,
            checks_summary          TEXT NOT NULL DEFAULT '',
            review_summary          TEXT NOT NULL DEFAULT '',
            UNIQUE(detection_id, sequence_number)
        );

        CREATE TABLE IF NOT EXISTS issues (
            id                  TEXT PRIMARY KEY,
            key                 TEXT NOT NULL UNIQUE,
            title               TEXT NOT NULL,
            type                TEXT NOT NULL,
            status              TEXT NOT NULL DEFAULT 'New',
            ext_case_system     TEXT,
            ext_case_external_id TEXT,
            ext_case_url        TEXT,
            ext_case_system_type INTEGER NOT NULL DEFAULT 0,
            description         TEXT,
            acceptance_criteria TEXT,
            data_source         TEXT,
            platform            TEXT,
            attack_technique_id TEXT,
            tlp                 TEXT,
            labels              TEXT NOT NULL DEFAULT '',
            created_at          TEXT NOT NULL,
            updated_at          TEXT NOT NULL
        );

        CREATE TABLE IF NOT EXISTS change_requests (
            id                  TEXT PRIMARY KEY,
            key                 TEXT NOT NULL UNIQUE,
            title               TEXT NOT NULL,
            detection_id        TEXT NOT NULL,
            author_id           TEXT NOT NULL,
            workflow_profile_id TEXT NOT NULL,
            base_version_id     TEXT,
            status              TEXT NOT NULL DEFAULT 'Draft',
            is_stale            INTEGER NOT NULL DEFAULT 0,
            stale_reason        TEXT,
            linked_issue_id     TEXT,
            created_at          TEXT NOT NULL,
            updated_at          TEXT NOT NULL,
            merged_at           TEXT,
            result_version_id   TEXT,
            close_reason        TEXT
        );

        CREATE TABLE IF NOT EXISTS change_draft_files (
            change_request_id   TEXT NOT NULL,
            logical_path        TEXT NOT NULL,
            content_type        TEXT NOT NULL,
            content             TEXT NOT NULL,
            updated_at          TEXT NOT NULL,
            updated_by          TEXT NOT NULL,
            PRIMARY KEY (change_request_id, logical_path)
        );

        CREATE TABLE IF NOT EXISTS check_runs (
            id                  TEXT PRIMARY KEY,
            change_request_id   TEXT NOT NULL,
            name                TEXT NOT NULL,
            is_blocking         INTEGER NOT NULL DEFAULT 1,
            status              TEXT NOT NULL DEFAULT 'Queued',
            started_at          TEXT,
            completed_at        TEXT,
            summary             TEXT NOT NULL DEFAULT '',
            details_json        TEXT NOT NULL DEFAULT '',
            logs_excerpt        TEXT NOT NULL DEFAULT ''
        );

        CREATE TABLE IF NOT EXISTS reviews (
            id                  TEXT PRIMARY KEY,
            change_request_id   TEXT NOT NULL,
            reviewer_id         TEXT NOT NULL,
            decision            TEXT NOT NULL,
            comment             TEXT NOT NULL DEFAULT '',
            created_at          TEXT NOT NULL,
            is_superseded       INTEGER NOT NULL DEFAULT 0,
            superseded_at       TEXT
        );

        CREATE TABLE IF NOT EXISTS merge_intents (
            change_request_id   TEXT PRIMARY KEY,
            detection_id        TEXT NOT NULL,
            detection_slug      TEXT NOT NULL,
            requested_at        TEXT NOT NULL,
            author_name         TEXT NOT NULL,
            author_email        TEXT NOT NULL,
            commit_message      TEXT NOT NULL,
            state               TEXT NOT NULL,
            commit_sha          TEXT,
            committed_at        TEXT,
            version_id          TEXT,
            completed_at        TEXT
        );
        """;
}