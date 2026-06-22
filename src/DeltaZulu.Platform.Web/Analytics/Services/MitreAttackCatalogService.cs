using Dapper;
using DeltaZulu.Platform.Data.DuckDb;

namespace DeltaZulu.Platform.Web.Analytics.Services;

public sealed class MitreAttackCatalogService
{
    public const string DefaultVersion = "v19.1";

    private readonly DuckDbConnectionFactory _connectionFactory;

    public MitreAttackCatalogService(DuckDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
    }

    public async Task<MitreAttackCatalogSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default)
    {
        var techniques = await SearchTechniquesAsync(null, 500, cancellationToken);
        return new MitreAttackCatalogSnapshot(DefaultVersion, techniques);
    }

    public async Task<IReadOnlyList<MitreAttackTechniqueOption>> SearchTechniquesAsync(
        string? searchText,
        int limit = 25,
        CancellationToken cancellationToken = default)
    {
        var query = (searchText ?? string.Empty).Trim();
        var safeLimit = Math.Clamp(limit, 1, 500);
        var connection = _connectionFactory.GetConnection();

        foreach (var sql in CandidateQueries())
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var rows = await connection.QueryAsync<MitreAttackTechniqueOption>(
                    new CommandDefinition(sql, new { Query = $"%{query}%", Limit = safeLimit }, cancellationToken: cancellationToken));
                return rows.ToArray();
            }
            catch
            {
                // Local ATT&CK workbooks may be imported with different sheet/table names. Try the next known shape.
            }
        }

        return FallbackTechniques
            .Where(t => string.IsNullOrWhiteSpace(query)
                || t.TechniqueId.Contains(query, StringComparison.OrdinalIgnoreCase)
                || t.Name.Contains(query, StringComparison.OrdinalIgnoreCase)
                || t.TacticNames.Contains(query, StringComparison.OrdinalIgnoreCase))
            .Take(safeLimit)
            .ToArray();
    }

    private static IEnumerable<string> CandidateQueries()
    {
        const string Project = "technique_id AS TechniqueId, technique_name AS Name, coalesce(tactic_names, '') AS TacticNames, coalesce(version, 'v19.1') AS Version";
        yield return $"SELECT {Project} FROM attack.enterprise_techniques WHERE technique_id ILIKE @Query OR technique_name ILIKE @Query OR tactic_names ILIKE @Query ORDER BY technique_id LIMIT @Limit";
        yield return $"SELECT {Project} FROM mitre_attack.enterprise_techniques WHERE technique_id ILIKE @Query OR technique_name ILIKE @Query OR tactic_names ILIKE @Query ORDER BY technique_id LIMIT @Limit";
        yield return """
            SELECT t.id AS TechniqueId,
                   t.name AS Name,
                   coalesce(string_agg(DISTINCT ta.name, ', '), '') AS TacticNames,
                   coalesce(max(t.version), 'v19.1') AS Version
            FROM attack.techniques t
            LEFT JOIN attack.technique_tactics tt ON tt.technique_id = t.id
            LEFT JOIN attack.tactics ta ON ta.id = tt.tactic_id
            WHERE t.id ILIKE @Query OR t.name ILIKE @Query OR ta.name ILIKE @Query
            GROUP BY t.id, t.name
            ORDER BY t.id
            LIMIT @Limit
            """;
    }

    private static readonly MitreAttackTechniqueOption[] FallbackTechniques =
    [
        new("T1059", "Command and Scripting Interpreter", "Execution", DefaultVersion),
        new("T1059.001", "PowerShell", "Execution", DefaultVersion),
        new("T1059.003", "Windows Command Shell", "Execution", DefaultVersion),
        new("T1105", "Ingress Tool Transfer", "Command and Control", DefaultVersion),
        new("T1548", "Abuse Elevation Control Mechanism", "Privilege Escalation, Defense Evasion", DefaultVersion),
        new("T1548.002", "Bypass User Account Control", "Privilege Escalation, Defense Evasion", DefaultVersion),
    ];
}

public sealed record MitreAttackCatalogSnapshot(string Version, IReadOnlyList<MitreAttackTechniqueOption> Techniques);

public sealed record MitreAttackTechniqueOption(string TechniqueId, string Name, string TacticNames, string Version)
{
    public string DisplayName => $"{TechniqueId} — {Name}";
    public string StoredValue => TechniqueId;
}
