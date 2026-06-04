using Dapper;
using Microsoft.Data.Sqlite;

namespace Workbench.Persistence;

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
    }

    private const string Schema = """
        CREATE TABLE IF NOT EXISTS detections (
            id              TEXT PRIMARY KEY,
            slug            TEXT NOT NULL UNIQUE,
            title           TEXT NOT NULL,
            summary         TEXT NOT NULL DEFAULT '',
            lifecycle       TEXT NOT NULL DEFAULT 'Conceived',
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
            status              TEXT NOT NULL DEFAULT 'Open',
            priority            TEXT NOT NULL DEFAULT 'Normal',
            assignee_id         TEXT,
            linked_detection_id TEXT,
            ext_case_system     TEXT,
            ext_case_external_id TEXT,
            ext_case_url        TEXT,
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
        """;
}
