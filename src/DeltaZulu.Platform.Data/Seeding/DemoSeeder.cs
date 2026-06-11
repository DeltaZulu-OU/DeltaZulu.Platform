using Dapper;
using Microsoft.Data.Sqlite;

namespace DeltaZulu.Platform.Data.Seeding;

public static class DemoSeeder
{
    private static readonly string AnalystId = "a0000000-0000-0000-0000-000000000001";
    private static readonly string ReviewerId = "a0000000-0000-0000-0000-000000000002";

    public static void Seed(string connectionString)
    {
        using var conn = new SqliteConnection(connectionString);
        conn.Open();
        using var tx = conn.BeginTransaction();

        ClearAll(conn, tx);

        var now = DateTimeOffset.UtcNow;

        var detections = SeedDetections(conn, tx, now);
        var issues = SeedIssues(conn, tx, now, detections);
        SeedChangeRequests(conn, tx, now, detections, issues);

        tx.Commit();
    }

    private static void ClearAll(SqliteConnection conn, SqliteTransaction tx)
    {
        conn.Execute("DELETE FROM merge_intents", transaction: tx);
        conn.Execute("DELETE FROM reviews", transaction: tx);
        conn.Execute("DELETE FROM check_runs", transaction: tx);
        conn.Execute("DELETE FROM change_draft_files", transaction: tx);
        conn.Execute("DELETE FROM change_requests", transaction: tx);
        conn.Execute("DELETE FROM detection_versions", transaction: tx);
        conn.Execute("DELETE FROM issues", transaction: tx);
        conn.Execute("DELETE FROM detections", transaction: tx);
    }

    private static List<(string Id, string Slug)> SeedDetections(
        SqliteConnection conn, SqliteTransaction tx, DateTimeOffset now)
    {
        var detections = new[]
        {
            new
            {
                Id = Guid.NewGuid().ToString("D"),
                Slug = "lateral-movement-psexec",
                Title = "Lateral Movement via PsExec",
                Summary = "Detects remote service creation indicative of PsExec-style lateral movement.",
                Lifecycle = "Accepted",
                CreatedAt = now.AddDays(-30),
            },
            new
            {
                Id = Guid.NewGuid().ToString("D"),
                Slug = "credential-dumping-lsass",
                Title = "Credential Dumping – LSASS Memory Access",
                Summary = "Identifies processes accessing LSASS memory, a common credential harvesting technique.",
                Lifecycle = "Accepted",
                CreatedAt = now.AddDays(-25),
            },
            new
            {
                Id = Guid.NewGuid().ToString("D"),
                Slug = "persistence-scheduled-task",
                Title = "Persistence via Scheduled Task Creation",
                Summary = "Alerts on scheduled task creation used for persistence by threat actors.",
                Lifecycle = "Accepted",
                CreatedAt = now.AddDays(-20),
            },
            new
            {
                Id = Guid.NewGuid().ToString("D"),
                Slug = "exfil-dns-tunneling",
                Title = "Data Exfiltration via DNS Tunneling",
                Summary = "Detects unusually long DNS queries or high query volume suggesting DNS tunneling.",
                Lifecycle = "Draft",
                CreatedAt = now.AddDays(-5),
            },
            new
            {
                Id = Guid.NewGuid().ToString("D"),
                Slug = "c2-beacon-interval",
                Title = "C2 Beacon Interval Detection",
                Summary = "Identifies periodic outbound connections consistent with command-and-control beaconing.",
                Lifecycle = "Draft",
                CreatedAt = now.AddDays(-2),
            },
            new
            {
                Id = Guid.NewGuid().ToString("D"),
                Slug = "deprecated-old-powershell",
                Title = "Legacy PowerShell Downgrade Attack",
                Summary = "Deprecated: replaced by updated PowerShell monitoring detection.",
                Lifecycle = "Deprecated",
                CreatedAt = now.AddDays(-60),
            },
        };

        const string sql = """
            INSERT INTO detections (id, slug, title, summary, lifecycle, current_version_id, created_at, updated_at)
            VALUES (@Id, @Slug, @Title, @Summary, @Lifecycle, NULL, @CreatedAt, @CreatedAt)
            """;

        foreach (var d in detections)
            conn.Execute(sql, new { d.Id, d.Slug, d.Title, d.Summary, d.Lifecycle, CreatedAt = Iso(d.CreatedAt) }, tx);

        var versionedDetections = detections.Where(d => d.Lifecycle == "Accepted").ToList();
        foreach (var d in versionedDetections)
        {
            var versionId = Guid.NewGuid().ToString("D");
            var crId = Guid.NewGuid().ToString("D");
            conn.Execute("""
                INSERT INTO detection_versions
                    (id, detection_id, sequence_number, display_version, title, change_summary,
                     author_id, workflow_profile, source_change_request_id, linked_issue_id,
                     accepted_at, changed_sections, git_commit_sha, checks_summary, review_summary)
                VALUES
                    (@VersionId, @DetectionId, 1, '1.0', @Title, 'Initial version',
                     @AuthorId, 'StandardReview', @CrId, NULL,
                     @AcceptedAt, 'detection.yml;query.kql', @CommitSha, 'All checks passed', 'Approved')
                """, new {
                VersionId = versionId,
                DetectionId = d.Id,
                d.Title,
                AuthorId = AnalystId,
                CrId = crId,
                AcceptedAt = Iso(d.CreatedAt.AddDays(2)),
                CommitSha = Guid.NewGuid().ToString("N")[..12],
            }, tx);

            conn.Execute("UPDATE detections SET current_version_id = @VersionId WHERE id = @Id",
                new { VersionId = versionId, d.Id }, tx);
        }

        return detections.Select(d => (d.Id, d.Slug)).ToList();
    }

    private static List<(string Id, string Key)> SeedIssues(
        SqliteConnection conn, SqliteTransaction tx, DateTimeOffset now,
        List<(string Id, string Slug)> detections)
    {
        var issues = new[]
        {
            new
            {
                Id = Guid.NewGuid().ToString("D"),
                Key = "REQ-001",
                Title = "Tune PsExec detection – high false-positive rate",
                Type = "Request",
                Status = "New",
                CreatedAt = now.AddDays(-10),
                ExtSystem = (string?)null,
                ExtId = (string?)null,
                ExtUrl = (string?)null,
            },
            new
            {
                Id = Guid.NewGuid().ToString("D"),
                Key = "REQ-002",
                Title = "Add Mimikatz test coverage for LSASS detection",
                Type = "Request",
                Status = "New",
                CreatedAt = now.AddDays(-8),
                ExtSystem = (string?)null,
                ExtId = (string?)null,
                ExtUrl = (string?)null,
            },
            new
            {
                Id = Guid.NewGuid().ToString("D"),
                Key = "REQ-003",
                Title = "Fix schtasks /change variant coverage",
                Type = "Request",
                Status = "InProgress",
                CreatedAt = now.AddDays(-6),
                ExtSystem = (string?)null,
                ExtId = (string?)null,
                ExtUrl = (string?)null,
            },
            new
            {
                Id = Guid.NewGuid().ToString("D"),
                Key = "CASE-001",
                Title = "SOC escalation – suspicious lateral movement in ACME Corp",
                Type = "Case",
                Status = "InProgress",
                CreatedAt = now.AddDays(-2),
                ExtSystem = (string?)"TheHive",
                ExtId = (string?)"HIVE-4821",
                ExtUrl = (string?)"https://thehive.local/cases/HIVE-4821",
            },
            new
            {
                Id = Guid.NewGuid().ToString("D"),
                Key = "CASE-002",
                Title = "FlowIntel – credential theft campaign targeting finance sector",
                Type = "Case",
                Status = "New",
                CreatedAt = now.AddDays(-1),
                ExtSystem = (string?)"FlowIntel",
                ExtId = (string?)"FI-2847",
                ExtUrl = (string?)"https://flowintel.local/case/2847",
            },
        };

        const string sql = """
            INSERT INTO issues
                (id, key, title, type, status,
                 ext_case_system, ext_case_external_id, ext_case_url,
                 created_at, updated_at)
            VALUES
                (@Id, @Key, @Title, @Type, @Status,
                 @ExtSystem, @ExtId, @ExtUrl,
                 @CreatedAt, @CreatedAt)
            """;

        foreach (var i in issues)
        {
            conn.Execute(sql, new {
                i.Id,
                i.Key,
                i.Title,
                i.Type,
                i.Status,
                i.ExtSystem,
                i.ExtId,
                i.ExtUrl,
                CreatedAt = Iso(i.CreatedAt),
            }, tx);
        }

        return issues.Select(i => (i.Id, i.Key)).ToList();
    }

    private static void SeedChangeRequests(
        SqliteConnection conn, SqliteTransaction tx, DateTimeOffset now,
        List<(string Id, string Slug)> detections,
        List<(string Id, string Key)> issues)
    {
        var changes = new[]
        {
            new
            {
                Id = Guid.NewGuid().ToString("D"),
                Key = "CR-001",
                Title = "Tune PsExec detection – exclude IT admin accounts",
                DetectionId = detections[0].Id,
                AuthorId = AnalystId,
                WorkflowProfile = "ControlledReview",
                Status = "ReviewRequired",
                LinkedIssueId = issues[0].Id,
                CreatedAt = now.AddDays(-7),
            },
            new
            {
                Id = Guid.NewGuid().ToString("D"),
                Key = "CR-002",
                Title = "Add Mimikatz test fixtures for LSASS detection",
                DetectionId = detections[1].Id,
                AuthorId = AnalystId,
                WorkflowProfile = "StandardReview",
                Status = "Draft",
                LinkedIssueId = issues[1].Id,
                CreatedAt = now.AddDays(-4),
            },
            new
            {
                Id = Guid.NewGuid().ToString("D"),
                Key = "CR-003",
                Title = "Fix schtasks /change variant coverage",
                DetectionId = detections[2].Id,
                AuthorId = ReviewerId,
                WorkflowProfile = "StandardReview",
                Status = "Draft",
                LinkedIssueId = issues[3].Id,
                CreatedAt = now.AddDays(-3),
            },
            new
            {
                Id = Guid.NewGuid().ToString("D"),
                Key = "CR-004",
                Title = "Initial DNS tunneling detection query",
                DetectionId = detections[3].Id,
                AuthorId = AnalystId,
                WorkflowProfile = "QuickLab",
                Status = "Draft",
                LinkedIssueId = issues[2].Id,
                CreatedAt = now.AddDays(-1),
            },
        };

        const string crSql = """
            INSERT INTO change_requests
                (id, key, title, detection_id, author_id, workflow_profile_id,
                 base_version_id, status, is_stale, stale_reason, linked_issue_id,
                 created_at, updated_at, merged_at, result_version_id, close_reason)
            VALUES
                (@Id, @Key, @Title, @DetectionId, @AuthorId, @WorkflowProfile,
                 NULL, @Status, 0, NULL, @LinkedIssueId,
                 @CreatedAt, @CreatedAt, NULL, NULL, NULL)
            """;

        foreach (var c in changes)
        {
            conn.Execute(crSql, new {
                c.Id,
                c.Key,
                c.Title,
                c.DetectionId,
                c.AuthorId,
                c.WorkflowProfile,
                c.Status,
                c.LinkedIssueId,
                CreatedAt = Iso(c.CreatedAt),
            }, tx);
        }

        SeedDraftFiles(conn, tx, now, changes[0].Id, changes[1].Id, changes[2].Id, changes[3].Id);
        SeedCheckRuns(conn, tx, now, changes[0].Id, changes[2].Id);
        SeedReviews(conn, tx, now, changes[0].Id);
    }

    private static void SeedDraftFiles(
        SqliteConnection conn, SqliteTransaction tx, DateTimeOffset now,
        string cr1, string cr2, string cr3, string cr4)
    {
        var files = new[]
        {
            new { CrId = cr1, Path = "detection.yml", ContentType = "DetectionMetadata", Content = DemoYaml.PsExecMetadata, By = AnalystId },
            new { CrId = cr1, Path = "query.kql", ContentType = "AnalyticsQuery", Content = DemoYaml.PsExecQuery, By = AnalystId },
            new { CrId = cr2, Path = "tests/mimikatz-lsass.yml", ContentType = "TestDefinition", Content = DemoYaml.LsassTestDef, By = AnalystId },
            new { CrId = cr2, Path = "fixtures/mimikatz-dump.ndjson", ContentType = "Fixture", Content = DemoYaml.MimikatzFixture, By = AnalystId },
            new { CrId = cr3, Path = "query.kql", ContentType = "AnalyticsQuery", Content = DemoYaml.SchtasksQuery, By = ReviewerId },
            new { CrId = cr4, Path = "detection.yml", ContentType = "DetectionMetadata", Content = DemoYaml.DnsTunnelMetadata, By = AnalystId },
            new { CrId = cr4, Path = "query.kql", ContentType = "AnalyticsQuery", Content = DemoYaml.DnsTunnelQuery, By = AnalystId },
        };

        const string sql = """
            INSERT INTO change_draft_files (change_request_id, logical_path, content_type, content, updated_at, updated_by)
            VALUES (@CrId, @Path, @ContentType, @Content, @UpdatedAt, @By)
            """;

        foreach (var f in files)
            conn.Execute(sql, new { f.CrId, f.Path, f.ContentType, f.Content, UpdatedAt = Iso(now), f.By }, tx);
    }

    private static void SeedCheckRuns(
        SqliteConnection conn, SqliteTransaction tx, DateTimeOffset now,
        string crInReview, string crFailed)
    {
        var checks = new[]
        {
            new { Id = Guid.NewGuid().ToString("D"), CrId = crInReview, Name = "PackageSchema", Blocking = 1, Status = "Passed", Summary = "Schema valid", Logs = "" },
            new { Id = Guid.NewGuid().ToString("D"), CrId = crInReview, Name = "QuerySyntax", Blocking = 1, Status = "Passed", Summary = "KQL parsed OK", Logs = "" },
            new { Id = Guid.NewGuid().ToString("D"), CrId = crFailed, Name = "PackageSchema", Blocking = 1, Status = "Passed", Summary = "Schema valid", Logs = "" },
            new { Id = Guid.NewGuid().ToString("D"), CrId = crFailed, Name = "QuerySyntax", Blocking = 1, Status = "Failed", Summary = "Syntax error at line 4", Logs = "line 4:12 - unexpected token '|' after 'where'" },
        };

        const string sql = """
            INSERT INTO check_runs (id, change_request_id, name, is_blocking, status, started_at, completed_at, summary, details_json, logs_excerpt)
            VALUES (@Id, @CrId, @Name, @Blocking, @Status, @StartedAt, @CompletedAt, @Summary, '', @Logs)
            """;

        foreach (var c in checks)
        {
            conn.Execute(sql, new {
                c.Id,
                c.CrId,
                c.Name,
                c.Blocking,
                c.Status,
                c.Summary,
                c.Logs,
                StartedAt = Iso(now.AddMinutes(-5)),
                CompletedAt = Iso(now.AddMinutes(-4)),
            }, tx);
        }
    }

    private static void SeedReviews(
        SqliteConnection conn, SqliteTransaction tx, DateTimeOffset now, string crInReview)
    {
        conn.Execute("""
            INSERT INTO reviews (id, change_request_id, reviewer_id, decision, comment, created_at, is_superseded, superseded_at)
            VALUES (@Id, @CrId, @ReviewerId, 'ChangesRequested', 'Please add an exclusion list parameter instead of hardcoding admin accounts.', @CreatedAt, 1, @SupersededAt)
            """, new {
            Id = Guid.NewGuid().ToString("D"),
            CrId = crInReview,
            ReviewerId,
            CreatedAt = Iso(now.AddDays(-5)),
            SupersededAt = Iso(now.AddDays(-3)),
        }, tx);

        conn.Execute("""
            INSERT INTO reviews (id, change_request_id, reviewer_id, decision, comment, created_at, is_superseded, superseded_at)
            VALUES (@Id, @CrId, @ReviewerId, 'Approved', 'Exclusion list approach looks good. Approved.', @CreatedAt, 0, NULL)
            """, new {
            Id = Guid.NewGuid().ToString("D"),
            CrId = crInReview,
            ReviewerId,
            CreatedAt = Iso(now.AddDays(-3)),
        }, tx);
    }

    private static string Iso(DateTimeOffset dt) => dt.UtcDateTime.ToString("O");
}

internal static class DemoYaml
{
    public const string PsExecMetadata = """
        name: lateral-movement-psexec
        title: Lateral Movement via PsExec
        severity: high
        mitre:
          tactics: [lateral-movement]
          techniques: [T1570, T1021.002]
        platforms: [windows]
        data_sources: [process_creation, service_creation]
        exclusions:
          - field: user.name
            values: []
        """;

    public const string PsExecQuery = """
        DeviceProcessEvents
        | where Timestamp > ago(1h)
        | where FileName in~ ("psexec.exe", "psexesvc.exe")
           or (FileName == "services.exe"
               and ProcessCommandLine has "PSEXESVC")
        | project Timestamp, DeviceName, AccountName, FileName, ProcessCommandLine
        | sort by Timestamp desc
        """;

    public const string LsassTestDef = """
        name: mimikatz-lsass-access
        detection: credential-dumping-lsass
        scenarios:
          - name: sekurlsa::logonpasswords
            fixture: fixtures/mimikatz-dump.ndjson
            expected_alerts: 1
          - name: benign-av-scan
            fixture: fixtures/av-lsass-read.ndjson
            expected_alerts: 0
        """;

    public const string MimikatzFixture = """
        {"Timestamp":"2025-11-10T08:12:00Z","DeviceName":"WKS-042","ProcessName":"mimikatz.exe","TargetProcess":"lsass.exe","AccessMask":"0x1010"}
        {"Timestamp":"2025-11-10T08:12:01Z","DeviceName":"WKS-042","ProcessName":"mimikatz.exe","TargetProcess":"lsass.exe","AccessMask":"0x1FFFFF"}
        """;

    public const string SchtasksQuery = """
        DeviceProcessEvents
        | where FileName =~ "schtasks.exe"
        | where ProcessCommandLine has_any ("/create", "/change")
        | where ProcessCommandLine !has "/delete"
        | project Timestamp, DeviceName, AccountName, ProcessCommandLine
        | where
        """;

    public const string DnsTunnelMetadata = """
        name: exfil-dns-tunneling
        title: Data Exfiltration via DNS Tunneling
        severity: medium
        mitre:
          tactics: [exfiltration]
          techniques: [T1048.003]
        platforms: [windows, linux]
        data_sources: [dns_query]
        status: draft
        """;

    public const string DnsTunnelQuery = """
        DnsEvents
        | where Timestamp > ago(24h)
        | extend QueryLength = strlen(Name)
        | where QueryLength > 50
        | summarize
            QueryCount = count(),
            AvgLength = avg(QueryLength),
            DistinctSubdomains = dcount(Name)
          by ClientIP, bin(Timestamp, 5m)
        | where QueryCount > 100 or AvgLength > 60
        """;
}