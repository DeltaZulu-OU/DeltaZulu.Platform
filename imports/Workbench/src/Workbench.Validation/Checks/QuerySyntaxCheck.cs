using System.Text.Json;
using Workbench.Application.Abstractions;
using Workbench.Domain.Enums;

namespace Workbench.Validation.Checks;

/// <summary>
/// Interface-backed query syntax check. The check owns check orchestration while
/// <see cref="IQuerySyntaxValidator"/> owns parser-specific validation behind a deterministic
/// adapter boundary.
/// </summary>
public sealed class QuerySyntaxCheck(IQuerySyntaxValidator validator) : ICheck
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly IQuerySyntaxValidator _validator = validator ?? throw new ArgumentNullException(nameof(validator));

    public string Name => "query-syntax";
    public bool IsBlocking => true;

    public IReadOnlySet<DraftContentType> ApplicableContentTypes { get; } =
        new HashSet<DraftContentType> { DraftContentType.HuntingQuery }.AsReadOnly();

    public Task<CheckOutcome> RunAsync(CheckContext context, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var queries = context.DraftFiles
            .Where(f => f.ContentType == DraftContentType.HuntingQuery)
            .ToList();

        if (queries.Count == 0)
        {
            return Task.FromResult(CheckOutcome.Skip("No query files in draft set."));
        }

        var diagnostics = new List<QueryFileDiagnostic>();

        foreach (var query in queries)
        {
            ct.ThrowIfCancellationRequested();

            var result = _validator.Validate(new QuerySyntaxValidationRequest(
                query.LogicalPath,
                query.ContentType,
                query.Content,
                context.DetectionSlug));

            if (!result.IsValid)
            {
                diagnostics.Add(new QueryFileDiagnostic(query.LogicalPath, result.Diagnostics));
            }
        }

        if (diagnostics.Count == 0)
        {
            return Task.FromResult(CheckOutcome.Pass(
                $"Query syntax check passed ({queries.Count} file(s)) using {_validator.GetType().Name}."));
        }

        var logs = string.Join('\n', diagnostics.SelectMany(d => d.Diagnostics.Select(x => x.Format(d.LogicalPath))));
        var details = JsonSerializer.Serialize(new
        {
            validator = _validator.GetType().Name,
            diagnostics = diagnostics.Select(d => new
            {
                logicalPath = d.LogicalPath,
                diagnostics = d.Diagnostics,
            }),
        }, JsonOptions);

        return Task.FromResult(CheckOutcome.Fail(
            $"{diagnostics.Sum(d => d.Diagnostics.Count)} query syntax error(s).",
            details,
            logs));
    }

    private sealed record QueryFileDiagnostic(
        string LogicalPath,
        IReadOnlyList<QuerySyntaxDiagnostic> Diagnostics);
}
