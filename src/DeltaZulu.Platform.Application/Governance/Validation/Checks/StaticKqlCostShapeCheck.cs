using System.Text.Json;
using System.Text.RegularExpressions;
using DeltaZulu.Platform.Domain.Governance.Contracts;
using DeltaZulu.Platform.Domain.Governance.Enums;
using Kusto.Language;
using Kusto.Language.Syntax;

namespace DeltaZulu.Platform.Application.Governance.Validation.Checks;

/// <summary>
/// Performs credential-free AST linting for query shapes that commonly cause unbounded work.
/// This check intentionally runs before any execution-backed validation.
/// </summary>
public sealed partial class StaticKqlCostShapeCheck : ICheck
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public string Name => "query-static-cost-shape";
    public bool IsBlocking => true;

    public IReadOnlySet<DraftContentType> ApplicableContentTypes { get; } =
        new HashSet<DraftContentType> { DraftContentType.AnalyticsQuery }.AsReadOnly();

    public Task<CheckOutcome> RunAsync(CheckContext context, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var findings = new List<CostShapeFinding>();
        var queryCount = 0;

        foreach (var file in context.DraftFiles.Where(f => f.ContentType == DraftContentType.AnalyticsQuery))
        {
            ct.ThrowIfCancellationRequested();
            queryCount++;
            findings.AddRange(Analyze(file.LogicalPath, file.Content));
        }

        if (queryCount == 0)
        {
            return Task.FromResult(CheckOutcome.Skip("No query files in draft set."));
        }

        if (findings.Count == 0)
        {
            return Task.FromResult(CheckOutcome.Pass(
                $"Static cost-shape check passed ({queryCount} query file(s))."));
        }

        var logs = string.Join('\n', findings.Select(f =>
            $"{f.LogicalPath}: {f.RuleId}: {f.Message}"));
        var details = JsonSerializer.Serialize(new { findings }, JsonOptions);
        return Task.FromResult(CheckOutcome.Fail(
            $"{findings.Count} static query cost-shape finding(s).",
            details,
            logs));
    }

    internal static IReadOnlyList<CostShapeFinding> Analyze(string logicalPath, string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return [];
        }

        var disabledRules = DisableDirectiveRegex().Matches(query)
            .SelectMany(match => match.Groups[1].Value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var syntax = KustoCode.Parse(query).Syntax;
        var findings = new List<CostShapeFinding>();

        AddIf("KQL001", !HasTimeWindow(syntax),
            "Query has no recognizable time-window filter.");
        AddIf("KQL002", syntax.GetDescendants<JoinOperator>().Any() && !HasTimeWindow(syntax),
            "Join is not protected by a recognizable time-window filter.");
        AddIf("KQL003", syntax.GetDescendants<UnionOperator>().Any(node => node.ToString().Contains('*', StringComparison.Ordinal)),
            "Wildcard union can fan out across an unbounded table set.");
        AddIf("KQL004", syntax.GetDescendants<MvExpandOperator>().Any() && !HasRowBound(syntax),
            "mv-expand has no downstream take/top row bound.");
        AddIf("KQL005", syntax.GetDescendants<SortOperator>().Any() && !HasRowBound(syntax),
            "sort has no downstream take/top row bound.");
        AddIf("KQL006", CrossClusterRegex().IsMatch(query),
            "Cross-cluster or cross-database query fanout requires explicit review.");
        AddIf("KQL007", CaseFoldEqualityRegex().IsMatch(query),
            "Case-folding both sides of equality can prevent index use; prefer a native case-insensitive operator.");

        return findings;

        void AddIf(string ruleId, bool condition, string message)
        {
            if (condition && !disabledRules.Contains(ruleId))
            {
                findings.Add(new CostShapeFinding(logicalPath, ruleId, message));
            }
        }
    }

    private static bool HasTimeWindow(SyntaxNode syntax) => syntax.GetDescendants<FilterOperator>()
        .Select(node => node.ToString())
        .Any(text => TimeWindowRegex().IsMatch(text));

    private static bool HasRowBound(SyntaxNode syntax) =>
        syntax.GetDescendants<TakeOperator>().Any() || syntax.GetDescendants<TopOperator>().Any();

    [GeneratedRegex(@"(?im)^\s*//\s*disable\s+([A-Z0-9_, -]+)\s*$", RegexOptions.CultureInvariant)]
    private static partial Regex DisableDirectiveRegex();

    [GeneratedRegex(@"(?i)\b(ago|between|startofday|startofweek|startofmonth)\s*\(|\b(datetime|timestamp|timegenerated)\b\s*(?:>=|>|between)", RegexOptions.CultureInvariant)]
    private static partial Regex TimeWindowRegex();

    [GeneratedRegex(@"(?i)\b(?:cluster|database)\s*\(", RegexOptions.CultureInvariant)]
    private static partial Regex CrossClusterRegex();

    [GeneratedRegex(@"(?i)\btolower\s*\([^)]*\)\s*==|==\s*tolower\s*\(", RegexOptions.CultureInvariant)]
    private static partial Regex CaseFoldEqualityRegex();
}

internal sealed record CostShapeFinding(string LogicalPath, string RuleId, string Message);
