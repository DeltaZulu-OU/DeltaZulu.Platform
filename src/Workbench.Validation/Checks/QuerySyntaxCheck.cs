using Workbench.Application.Abstractions;
using Workbench.Domain.Enums;

namespace Workbench.Validation.Checks;

/// <summary>
/// Placeholder query syntax check. In the POC, this validates only that the query content
/// is non-empty. A real implementation would parse KQL, SPL, YARA, or Sigma depending on
/// the detection's query language.
/// </summary>
/// <remarks>
/// Per AGENTS.md: "A placeholder parser is acceptable in the earliest POC if clearly
/// isolated." This check is clearly isolated — replace the body of <see cref="RunAsync"/>
/// when a real parser is integrated.
/// </remarks>
public sealed class QuerySyntaxCheck : ICheck
{
    public string Name => "query-syntax";
    public bool IsBlocking => true;

    public IReadOnlySet<DraftContentType> ApplicableContentTypes { get; } =
        new HashSet<DraftContentType> { DraftContentType.HuntingQuery }.AsReadOnly();

    public Task<CheckOutcome> RunAsync(CheckContext context, CancellationToken ct = default)
    {
        var queries = context.DraftFiles
            .Where(f => f.ContentType == DraftContentType.HuntingQuery)
            .ToList();

        if (queries.Count == 0)
        {
            return Task.FromResult(CheckOutcome.Skip("No query files in draft set."));
        }

        var errors = new List<string>();

        foreach (var query in queries)
        {
            if (string.IsNullOrWhiteSpace(query.Content))
            {
                errors.Add($"{query.LogicalPath}: query content is empty.");
            }

            // TODO: integrate a real KQL/SPL/Sigma parser here.
            // For now, any non-empty content passes.
        }

        if (errors.Count == 0)
        {
            return Task.FromResult(CheckOutcome.Pass(
                $"Query syntax check passed ({queries.Count} file(s)). " +
                "Note: POC stub — only non-empty validation performed."));
        }

        return Task.FromResult(CheckOutcome.Fail(
            $"{errors.Count} query syntax error(s).",
            "{}",
            string.Join('\n', errors)));
    }
}
