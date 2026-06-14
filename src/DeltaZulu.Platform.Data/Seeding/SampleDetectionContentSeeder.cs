using System.Security.Cryptography;
using System.Text;
using Dapper;
using LibGit2Sharp;
using Microsoft.Data.Sqlite;

namespace DeltaZulu.Platform.Data.Seeding;

/// <summary>
/// Seeds sample detection-content files for development and demo environments.
/// </summary>
public static class SampleDetectionContentSeeder
{
    private static readonly SampleDetectionContentFile[] Files =
    {
        new("dns-high-query-volume-per-name/detection.yaml", """
id: dz-sample-dns-high-query-volume-per-name
title: High DNS query volume for a single name
status: sample
enabled: true
severity: Low
confidence: Medium
risk_score: 35
content_type: detection
query_language: kql
query_file: query.kql
schedule:
  type: cron
  expression: '0 */1 * * *'
lookback: 1d
materialization:
  mode: per_result_row
  max_alerts_per_run: 100
source:
  repository: Sentinel-Queries-main sample extract
  original_path: Derived from DNS anomaly patterns in sample extract
  adaptation: rewritten for DeltaZulu golden analytical views
description: >-
  Summarizes DNS telemetry to find device/name pairs with unusually high query counts in a
  short window. The threshold is deliberately conservative for seed content and should be
  tuned per environment.
required_tables:
  - Dns
tactics:
  - Command and Control
techniques:
  - T1071.004
entity_mappings:
  - type: host
    field: DeviceName
  - type: domain
    field: QueryName
false_positive_notes:
  - Administrative activity, vulnerability scans, baselined services, or sanctioned automation may match depending on the rule.
validation_notes:
  - Sample content only; tune thresholds, schemas, and suppression before production use.
"""),
        new("dns-high-query-volume-per-name/query.kql", """
Dns
| where Timestamp > ago(1h)
| summarize QueryCount=count() by DeviceName, QueryName
| where QueryCount > 50
| sort by QueryCount desc
"""),
        new("inbound-ldap-ldaps-exposure/detection.yaml", """
id: dz-sample-inbound-ldap-ldaps-exposure
title: Inbound LDAP or LDAPS connection observed
status: sample
enabled: true
severity: Medium
confidence: Medium
risk_score: 50
content_type: detection
query_language: kql
query_file: query.kql
schedule:
  type: cron
  expression: '0 */1 * * *'
lookback: 1d
materialization:
  mode: per_result_row
  max_alerts_per_run: 100
source:
  repository: Sentinel-Queries-main sample extract
  original_path: Defender for Endpoint/Device-SummarizeLDAPandLDAPStraffic.kql
  adaptation: rewritten for DeltaZulu golden analytical views
description: >-
  Identifies devices accepting inbound LDAP or LDAPS traffic. This sample is useful for
  surfacing directory-service exposure and unexpected LDAP listeners.
required_tables:
  - NetworkSession
tactics:
  - Discovery
  - Lateral Movement
techniques:
  - T1046
entity_mappings:
  - type: host
    field: DeviceName
  - type: ip
    field: RemoteIP
  - type: process
    field: InitiatingProcessFileName
false_positive_notes:
  - Administrative activity, vulnerability scans, baselined services, or sanctioned automation may match depending on the rule.
validation_notes:
  - Sample content only; tune thresholds, schemas, and suppression before production use.
"""),
        new("inbound-ldap-ldaps-exposure/query.kql", """
NetworkSession
| where Timestamp > ago(7d)
| where LocalPort in (389, 636, 3269)
| project Timestamp, DeviceName, LocalIP, LocalPort, RemoteIP, InitiatingProcessFileName, SourceType
| sort by Timestamp desc
"""),
        new("kerberos-preauth-disabled/detection.yaml", """
id: dz-sample-kerberos-preauth-disabled
title: Kerberos preauthentication disabled for account
status: sample
enabled: true
severity: High
confidence: Medium
risk_score: 80
content_type: detection
query_language: kql
query_file: query.kql
schedule:
  type: cron
  expression: '0 */1 * * *'
lookback: 1d
materialization:
  mode: per_result_row
  max_alerts_per_run: 100
source:
  repository: Sentinel-Queries-main sample extract
  original_path: Active Directory/SecurityEvent-AccountPreAuthChanges.kql
  adaptation: rewritten for DeltaZulu golden analytical views
description: >-
  Detects account change events that indicate Kerberos preauthentication was disabled.
  This should be reviewed quickly because it can increase AS-REP roasting exposure.
required_tables:
  - UserManagement
tactics:
  - Credential Access
  - Persistence
techniques:
  - T1558.004
entity_mappings:
  - type: account
    field: TargetAccount
  - type: account
    field: ActorAccount
  - type: host
    field: DeviceName
false_positive_notes:
  - Administrative activity, vulnerability scans, baselined services, or sanctioned automation may match depending on the rule.
validation_notes:
  - Sample content only; tune thresholds, schemas, and suppression before production use.
"""),
        new("kerberos-preauth-disabled/query.kql", """
UserManagement
| where Timestamp > ago(7d)
| where EventId == 4738
| where AccountType =~ "User"
| where UserAccountControl has "2096"
| project Timestamp, TargetAccount, ActorAccount, UserAccountControl, DeviceName, SourceType
| sort by Timestamp desc
"""),
        new("new-low-port-listener/detection.yaml", """
id: dz-sample-new-low-port-listener
title: Low-numbered service listener created on endpoint
status: sample
enabled: true
severity: Medium
confidence: Medium
risk_score: 55
content_type: detection
query_language: kql
query_file: query.kql
schedule:
  type: cron
  expression: '0 */1 * * *'
lookback: 1d
materialization:
  mode: per_result_row
  max_alerts_per_run: 100
source:
  repository: Sentinel-Queries-main sample extract
  original_path: Defender for Endpoint/Device-InterestingPortsOpened.kql
  adaptation: rewritten for DeltaZulu golden analytical views
description: >-
  Finds endpoint telemetry indicating a listener on common low-numbered service ports.
  This is a sample for exposure review and unexpected service creation.
required_tables:
  - NetworkSession
tactics:
  - Persistence
  - Command and Control
techniques:
  - T1571
entity_mappings:
  - type: host
    field: DeviceName
  - type: process
    field: InitiatingProcessFileName
  - type: account
    field: AccountName
false_positive_notes:
  - Administrative activity, vulnerability scans, baselined services, or sanctioned automation may match depending on the rule.
validation_notes:
  - Sample content only; tune thresholds, schemas, and suppression before production use.
"""),
        new("new-low-port-listener/query.kql", """
NetworkSession
| where Timestamp > ago(7d)
| where ActionType =~ "ListeningConnectionCreated"
| where LocalPort in (21, 22, 53, 80, 443, 445, 3389)
| project Timestamp, DeviceName, LocalIP, LocalPort, InitiatingProcessFileName, InitiatingProcessCommandLine, AccountName
| sort by Timestamp desc
"""),
        new("password-not-required-flag-set/detection.yaml", """
id: dz-sample-password-not-required-flag-set
title: Active Directory account set to password not required
status: sample
enabled: true
severity: High
confidence: High
risk_score: 75
content_type: detection
query_language: kql
query_file: query.kql
schedule:
  type: cron
  expression: '0 */1 * * *'
lookback: 1d
materialization:
  mode: per_result_row
  max_alerts_per_run: 100
source:
  repository: Sentinel-Queries-main sample extract
  original_path: Active Directory/SecurityEvent-AccountSetPasswordNotRequired.kql
  adaptation: rewritten for DeltaZulu golden analytical views
description: >-
  Detects account change events where the password-not-required bit appears in
  UserAccountControl telemetry. This is a high-signal account-hardening violation when the
  field is populated by the source parser.
required_tables:
  - UserManagement
tactics:
  - Persistence
  - Defense Evasion
techniques:
  - T1098
entity_mappings:
  - type: account
    field: TargetAccount
  - type: account
    field: ActorAccount
  - type: host
    field: DeviceName
false_positive_notes:
  - Administrative activity, vulnerability scans, baselined services, or sanctioned automation may match depending on the rule.
validation_notes:
  - Sample content only; tune thresholds, schemas, and suppression before production use.
"""),
        new("password-not-required-flag-set/query.kql", """
UserManagement
| where Timestamp > ago(7d)
| where EventId == 4738
| where UserAccountControl has "2082"
| project Timestamp, TargetAccount, ActorAccount, UserAccountControl, DeviceName, SourceType
| sort by Timestamp desc
"""),
        new("powershell-execution-policy-change/detection.yaml", """
id: dz-sample-powershell-execution-policy-change
title: PowerShell execution policy changed by non-system account
status: sample
enabled: true
severity: Medium
confidence: High
risk_score: 55
content_type: detection
query_language: kql
query_file: query.kql
schedule:
  type: cron
  expression: '0 */1 * * *'
lookback: 1d
materialization:
  mode: per_result_row
  max_alerts_per_run: 100
source:
  repository: Sentinel-Queries-main sample extract
  original_path: Defender for Endpoint/Device-PowerShellExecutionModeChanged.kql
  adaptation: rewritten for DeltaZulu golden analytical views
description: >-
  Detects PowerShell command lines that attempt to change the local execution policy
  outside the system account context. This is useful as a low-noise administrative control
  change sample, not as a standalone compromise indicator.
required_tables:
  - ProcessEvent
tactics:
  - Defense Evasion
techniques:
  - T1059.001
entity_mappings:
  - type: host
    field: DeviceName
  - type: account
    field: AccountName
  - type: process
    field: FileName
false_positive_notes:
  - Administrative activity, vulnerability scans, baselined services, or sanctioned automation may match depending on the rule.
validation_notes:
  - Sample content only; tune thresholds, schemas, and suppression before production use.
"""),
        new("powershell-execution-policy-change/query.kql", """
ProcessEvent
| where Timestamp > ago(1d)
| where FileName =~ "powershell.exe"
| where ProcessCommandLine contains "Set-ExecutionPolicy"
| where AccountName !~ "system"
| project Timestamp, DeviceName, AccountName, ProcessCommandLine, ParentFileName, SourceType
| sort by Timestamp desc
"""),
        new("powershell-public-network-connection/detection.yaml", """
id: dz-sample-powershell-public-network-connection
title: PowerShell initiated public network connection
status: sample
enabled: true
severity: Medium
confidence: Medium
risk_score: 60
content_type: detection
query_language: kql
query_file: query.kql
schedule:
  type: cron
  expression: '0 */1 * * *'
lookback: 1d
materialization:
  mode: per_result_row
  max_alerts_per_run: 100
source:
  repository: Sentinel-Queries-main sample extract
  original_path: Defender for Endpoint/Device-PowershellConnectingtoInternet.kql
  adaptation: rewritten for DeltaZulu golden analytical views
description: >-
  Finds non-system PowerShell activity associated with outbound connections to public
  endpoints. This is a triage-oriented sample because benign administration and package
  installation can produce similar telemetry.
required_tables:
  - NetworkSession
tactics:
  - Command and Control
  - Execution
techniques:
  - T1059.001
entity_mappings:
  - type: host
    field: DeviceName
  - type: account
    field: AccountName
  - type: ip
    field: RemoteIP
  - type: process
    field: InitiatingProcessCommandLine
false_positive_notes:
  - Administrative activity, vulnerability scans, baselined services, or sanctioned automation may match depending on the rule.
validation_notes:
  - Sample content only; tune thresholds, schemas, and suppression before production use.
"""),
        new("powershell-public-network-connection/query.kql", """
NetworkSession
| where Timestamp > ago(1d)
| where InitiatingProcessCommandLine contains "powershell"
| where RemoteIPType =~ "Public"
| where AccountName !~ "system"
| where AccountName !~ "local service"
| project Timestamp, DeviceName, AccountName, InitiatingProcessCommandLine, LocalIP, RemoteIP, RemotePort, RemoteUrl
| sort by Timestamp desc
"""),
        new("public-ssh-egress/detection.yaml", """
id: dz-sample-public-ssh-egress
title: Outbound SSH connection to public endpoint
status: sample
enabled: true
severity: Low
confidence: Medium
risk_score: 45
content_type: detection
query_language: kql
query_file: query.kql
schedule:
  type: cron
  expression: '0 */1 * * *'
lookback: 1d
materialization:
  mode: per_result_row
  max_alerts_per_run: 100
source:
  repository: Sentinel-Queries-main sample extract
  original_path: Defender for Endpoint/Device-PublicPort22Allowed.kql
  adaptation: rewritten for DeltaZulu golden analytical views
description: >-
  Detects outbound SSH connections toward public addresses. It is intended as a policy and
  exposure sample rather than a high-confidence compromise rule.
required_tables:
  - NetworkSession
tactics:
  - Command and Control
  - Lateral Movement
techniques:
  - T1021.004
entity_mappings:
  - type: host
    field: DeviceName
  - type: account
    field: AccountName
  - type: ip
    field: RemoteIP
false_positive_notes:
  - Administrative activity, vulnerability scans, baselined services, or sanctioned automation may match depending on the rule.
validation_notes:
  - Sample content only; tune thresholds, schemas, and suppression before production use.
"""),
        new("public-ssh-egress/query.kql", """
NetworkSession
| where Timestamp > ago(1d)
| where RemotePort == 22
| where RemoteIPType =~ "Public"
| project Timestamp, DeviceName, AccountName, InitiatingProcessCommandLine, LocalIP, RemoteIP, RemoteUrl
| sort by Timestamp desc
"""),
        new("rdp-interactive-logon/detection.yaml", """
id: dz-sample-rdp-interactive-logon
title: Remote Desktop interactive logon observed
status: sample
enabled: true
severity: Low
confidence: High
risk_score: 40
content_type: detection
query_language: kql
query_file: query.kql
schedule:
  type: cron
  expression: '0 */1 * * *'
lookback: 1d
materialization:
  mode: per_result_row
  max_alerts_per_run: 100
source:
  repository: Sentinel-Queries-main sample extract
  original_path: Active Directory/SecurityEvent-SummarizeRDPActivity.kql
  adaptation: rewritten for DeltaZulu golden analytical views
description: >-
  Detects successful logons with RDP logon type. It is a baseline and investigation sample
  for lateral movement review, not a final incident rule.
required_tables:
  - Authentication
tactics:
  - Lateral Movement
techniques:
  - T1021.001
entity_mappings:
  - type: host
    field: DeviceName
  - type: account
    field: AccountName
  - type: ip
    field: SourceIP
false_positive_notes:
  - Administrative activity, vulnerability scans, baselined services, or sanctioned automation may match depending on the rule.
validation_notes:
  - Sample content only; tune thresholds, schemas, and suppression before production use.
"""),
        new("rdp-interactive-logon/query.kql", """
Authentication
| where Timestamp > ago(7d)
| where EventId == 4624
| where LogonType == 10
| project Timestamp, DeviceName, AccountName, AccountDomain, SourceIP, LogonType, SourceType
| sort by Timestamp desc
"""),
        new("suspicious-command-line-download/detection.yaml", """
id: dz-sample-suspicious-command-line-download
title: Command-line download utility usage
status: sample
enabled: true
severity: Medium
confidence: Medium
risk_score: 60
content_type: detection
query_language: kql
query_file: query.kql
schedule:
  type: cron
  expression: '0 */1 * * *'
lookback: 1d
materialization:
  mode: per_result_row
  max_alerts_per_run: 100
source:
  repository: Sentinel-Queries-main sample extract
  original_path: Derived from Defender endpoint command-line hunting patterns in sample extract
  adaptation: rewritten for DeltaZulu golden analytical views
description: >-
  Detects common command-line download patterns in process telemetry. The query is
  intentionally simple so it can act as validation content for KQL translation and
  governance workflows.
required_tables:
  - ProcessEvent
tactics:
  - Command and Control
  - Execution
techniques:
  - T1105
entity_mappings:
  - type: host
    field: DeviceName
  - type: account
    field: AccountName
  - type: process
    field: FileName
false_positive_notes:
  - Administrative activity, vulnerability scans, baselined services, or sanctioned automation may match depending on the rule.
validation_notes:
  - Sample content only; tune thresholds, schemas, and suppression before production use.
"""),
        new("suspicious-command-line-download/query.kql", """
ProcessEvent
| where Timestamp > ago(1d)
| where ProcessCommandLine has_any ("Invoke-WebRequest", "DownloadString", "curl", "wget", "bitsadmin")
| project Timestamp, DeviceName, AccountName, FileName, ProcessCommandLine, ParentFileName, SourceType
| sort by Timestamp desc
"""),
    };

    /// <summary>
    /// Writes all sample detection-content files beneath <paramref name="rootDirectory" />.
    /// Existing files are left untouched unless <paramref name="overwrite" /> is true.
    /// </summary>
    public static IReadOnlyList<string> Seed(string rootDirectory, bool overwrite = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootDirectory);

        Directory.CreateDirectory(rootDirectory);
        var written = new List<string>();

        foreach (var file in Files)
        {
            var fullPath = Path.Combine(rootDirectory, file.RelativePath.Replace('/', Path.DirectorySeparatorChar));
            var directory = Path.GetDirectoryName(fullPath);

            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            if (File.Exists(fullPath) && !overwrite)
            {
                continue;
            }

            File.WriteAllText(fullPath, file.Content, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            written.Add(fullPath);
        }

        return written;
    }


    /// <summary>
    /// Seeds the accepted-content Git repository with canonical sample detection files.
    /// The Detection Detail Files tab reads accepted content through the Git-backed
    /// accepted-content store, so plain files outside a committed <c>detections/&lt;slug&gt;</c>
    /// tree are intentionally not enough.
    /// </summary>
    public static IReadOnlyList<string> SeedAcceptedContentRepository(
        string repositoryDirectory,
        bool overwrite = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryDirectory);

        Directory.CreateDirectory(repositoryDirectory);
        if (!Repository.IsValid(repositoryDirectory))
        {
            Repository.Init(repositoryDirectory);
        }

        var written = new List<string>();
        var stagePaths = new List<string>();

        foreach (var file in Files)
        {
            var repositoryPath = "detections/" + file.RelativePath;
            var fullPath = Path.Combine(
                repositoryDirectory,
                repositoryPath.Replace('/', Path.DirectorySeparatorChar));
            var directory = Path.GetDirectoryName(fullPath);

            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            if (!File.Exists(fullPath) || overwrite)
            {
                File.WriteAllText(fullPath, file.Content, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
                written.Add(repositoryPath);
            }

            stagePaths.Add(repositoryPath);
        }

        using var repository = new Repository(repositoryDirectory);
        foreach (var path in stagePaths.Distinct(StringComparer.Ordinal))
        {
            Commands.Stage(repository, path);
        }

        if (HasStagedChanges(repository))
        {
            var committedAt = DateTimeOffset.UtcNow;
            var signature = new Signature(
                "DeltaZulu Sample Seeder",
                "samples@deltazulu.local",
                committedAt);

            repository.Commit("Seed sample detection content", signature, signature);
        }

        return written;
    }

    private static bool HasStagedChanges(Repository repository) => repository.RetrieveStatus().Any(entry =>
                                                                            entry.State.HasFlag(FileStatus.NewInIndex)
                                                                            || entry.State.HasFlag(FileStatus.ModifiedInIndex)
                                                                            || entry.State.HasFlag(FileStatus.DeletedFromIndex)
                                                                            || entry.State.HasFlag(FileStatus.RenamedInIndex)
                                                                            || entry.State.HasFlag(FileStatus.TypeChangeInIndex));

    /// <summary>
    /// Seeds the Governance detection catalog with sample detections so the Detections page
    /// has records to display. This is deliberately idempotent and does not delete or overwrite
    /// user-created detections.
    /// </summary>
    public static IReadOnlyList<string> SeedGovernanceCatalog(string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        using var conn = new SqliteConnection(connectionString);
        conn.Open();
        using var tx = conn.BeginTransaction();

        var seeded = SeedGovernanceCatalog(conn, tx, DateTimeOffset.UtcNow);

        tx.Commit();
        return seeded;
    }

    internal static IReadOnlyList<string> SeedGovernanceCatalog(
        SqliteConnection conn,
        SqliteTransaction tx,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(conn);
        ArgumentNullException.ThrowIfNull(tx);

        var seeded = new List<string>();
        foreach (var sample in CatalogEntries)
        {
            var existingId = conn.QuerySingleOrDefault<string>(
                "SELECT id FROM detections WHERE slug = @Slug",
                new { sample.Slug },
                tx);

            if (!string.IsNullOrWhiteSpace(existingId))
            {
                continue;
            }

            var detectionId = Guid.NewGuid().ToString("D");
            var versionId = Guid.NewGuid().ToString("D");
            var changeRequestId = Guid.NewGuid().ToString("D");
            var createdAt = now.AddDays(-CatalogEntries.Length + seeded.Count);
            var acceptedAt = createdAt.AddMinutes(30);

            conn.Execute("""
                INSERT INTO detections
                    (id, slug, title, summary, lifecycle, current_version_id, created_at, updated_at)
                VALUES
                    (@Id, @Slug, @Title, @Summary, 'Accepted', NULL, @CreatedAt, @UpdatedAt)
                """, new {
                Id = detectionId,
                sample.Slug,
                sample.Title,
                sample.Summary,
                CreatedAt = Iso(createdAt),
                UpdatedAt = Iso(acceptedAt),
            }, tx);

            conn.Execute("""
                INSERT INTO detection_versions
                    (id, detection_id, sequence_number, display_version, title, change_summary,
                     author_id, workflow_profile, source_change_request_id, linked_issue_id,
                     accepted_at, changed_sections, git_commit_sha, checks_summary, review_summary)
                VALUES
                    (@VersionId, @DetectionId, 1, '1.0', @Title, @ChangeSummary,
                     @AuthorId, 'StandardReview', @ChangeRequestId, NULL,
                     @AcceptedAt, 'detection.yaml;query.kql', @GitCommitSha, @ChecksSummary, @ReviewSummary)
                """, new {
                VersionId = versionId,
                DetectionId = detectionId,
                sample.Title,
                ChangeSummary = "Seeded from filtered Sentinel query sample content.",
                AuthorId = "a0000000-0000-0000-0000-000000000001",
                ChangeRequestId = changeRequestId,
                AcceptedAt = Iso(acceptedAt),
                GitCommitSha = ShortHash(sample.Slug),
                ChecksSummary = "Seed content imported for development and demo use.",
                ReviewSummary = "Auto-accepted sample content.",
            }, tx);

            conn.Execute(
                "UPDATE detections SET current_version_id = @VersionId WHERE id = @DetectionId",
                new { VersionId = versionId, DetectionId = detectionId },
                tx);

            seeded.Add(sample.Slug);
        }

        return seeded;
    }

    private static string Iso(DateTimeOffset value) => value.ToUniversalTime().ToString("O");

    private static string ShortHash(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant()[..12];

    private static readonly SampleDetectionCatalogEntry[] CatalogEntries =
    {
        new("powershell-execution-policy-change", "PowerShell execution policy changed by non-system account", "Detects PowerShell command lines that attempt to change the local execution policy outside the system account context."),
        new("powershell-public-network-connection", "PowerShell initiated public network connection", "Finds non-system PowerShell activity associated with outbound connections to public endpoints."),
        new("public-ssh-egress", "Outbound SSH connection to public endpoint", "Detects outbound SSH connections toward public addresses for policy and exposure review."),
        new("inbound-ldap-ldaps-exposure", "Inbound LDAP or LDAPS connection observed", "Identifies devices accepting inbound LDAP or LDAPS traffic."),
        new("rdp-interactive-logon", "Remote Desktop interactive logon observed", "Detects successful logons with RDP logon type for lateral-movement review."),
        new("password-not-required-flag-set", "Active Directory account set to password not required", "Detects account change events where the password-not-required bit appears in UserAccountControl telemetry."),
        new("kerberos-preauth-disabled", "Kerberos preauthentication disabled for account", "Detects account change events that indicate Kerberos preauthentication was disabled."),
        new("new-low-port-listener", "Low-numbered service listener created on endpoint", "Finds endpoint telemetry indicating a listener on common low-numbered service ports."),
        new("suspicious-command-line-download", "Suspicious command line download utility usage", "Detects command lines that use common download utilities or PowerShell download primitives."),
        new("dns-high-query-volume-per-name", "High DNS query volume for a single name", "Summarizes DNS telemetry to find device and name pairs with unusually high query counts."),
    };


    private sealed record SampleDetectionContentFile(string RelativePath, string Content);

    private sealed record SampleDetectionCatalogEntry(string Slug, string Title, string Summary);
}
