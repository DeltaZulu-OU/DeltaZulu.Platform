using System.Text.Json;
using DeltaZulu.Platform.Domain.Analytics.Execution;

namespace DeltaZulu.Platform.Domain.Analytics.Investigations;

public sealed class InvestigationService(IInvestigationRepository repository)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public Task SaveInvestigationAsync(InvestigationRecord investigation, CancellationToken cancellationToken = default)
        => repository.SaveInvestigationAsync(investigation, cancellationToken);

    public Task SavePivotAsync(InvestigationPivotRecord pivot, CancellationToken cancellationToken = default)
        => repository.SavePivotAsync(pivot, cancellationToken);

    public async Task<InvestigationQueryRunRecord> PersistQueryRunAsync(
        string investigationId,
        string pivotId,
        string queryText,
        DateTime startedAtUtc,
        long durationMs,
        QueryResult result,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(investigationId);
        ArgumentException.ThrowIfNullOrWhiteSpace(pivotId);
        ArgumentException.ThrowIfNullOrWhiteSpace(queryText);
        ArgumentNullException.ThrowIfNull(result);

        var schema = result.Columns.Select(c => new { c.Name, c.TypeName }).ToArray();
        var diagnostics = result.Diagnostics.All.Select(d => new { d.Code, d.Message, d.Severity }).ToArray();
        var run = new InvestigationQueryRunRecord(
            Guid.NewGuid().ToString("N"),
            investigationId,
            pivotId,
            queryText,
            startedAtUtc,
            durationMs,
            result.Success,
            result.Success ? result.RowCount : null,
            diagnostics.Length == 0 ? null : JsonSerializer.Serialize(diagnostics, JsonOptions),
            JsonSerializer.Serialize(schema, JsonOptions));

        await repository.SaveQueryRunAsync(run, cancellationToken);
        return run;
    }

    public async Task<IReadOnlyList<EvidenceRecord>> PromoteRowsAsync(
        string investigationId,
        string? queryRunId,
        QueryResult result,
        IEnumerable<int> rowIndexes,
        string createdBy,
        string? sourceTable = null,
        Func<int, string?>? sourceReferenceFactory = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(investigationId);
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(rowIndexes);
        ArgumentException.ThrowIfNullOrWhiteSpace(createdBy);

        var promoted = new List<EvidenceRecord>();
        foreach (var rowIndex in rowIndexes.Distinct().Order())
        {
            ArgumentOutOfRangeException.ThrowIfNegative(rowIndex);
            if (rowIndex >= result.RowCount)
            {
                throw new ArgumentOutOfRangeException(nameof(rowIndexes), $"Row index {rowIndex} is outside the result set.");
            }

            var row = result.Columns
                .Select((column, columnIndex) => new EvidenceCell(column.Name, column.TypeName, result.GetValue(rowIndex, columnIndex)))
                .ToArray();

            promoted.Add(new EvidenceRecord(
                Guid.NewGuid().ToString("N"),
                investigationId,
                queryRunId,
                sourceTable,
                sourceReferenceFactory?.Invoke(rowIndex),
                JsonSerializer.Serialize(row, JsonOptions),
                BuildSummary(row),
                createdBy,
                DateTime.UtcNow));
        }

        await repository.SaveEvidenceAsync(promoted, cancellationToken);
        return promoted;
    }

    public Task BulkTagEvidenceAsync(
        IEnumerable<string> evidenceIds,
        IEnumerable<string> tags,
        string addedBy,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(evidenceIds);
        ArgumentNullException.ThrowIfNull(tags);
        ArgumentException.ThrowIfNullOrWhiteSpace(addedBy);

        var now = DateTime.UtcNow;
        var records = evidenceIds
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.Ordinal)
            .SelectMany(_ => tags.Where(tag => !string.IsNullOrWhiteSpace(tag)).Distinct(StringComparer.OrdinalIgnoreCase),
                (id, tag) => new EvidenceTagRecord(id, tag, addedBy, now))
            .ToArray();

        return repository.AddTagsAsync(records, cancellationToken);
    }

    public async Task<InvestigationHandoverSummary> BuildHandoverSummaryAsync(
        string investigationId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(investigationId);

        var investigation = await repository.GetInvestigationAsync(investigationId, cancellationToken)
            ?? throw new InvalidOperationException($"Investigation '{investigationId}' was not found.");
        var pivots = await repository.ListPivotsAsync(investigationId, cancellationToken);
        var runs = await repository.ListQueryRunsAsync(investigationId, cancellationToken);
        var evidence = await repository.ListEvidenceAsync(investigationId, cancellationToken);
        var tags = await repository.ListTagsAsync(investigationId, cancellationToken);
        var comments = await repository.ListCommentsAsync(investigationId, cancellationToken);
        var evidenceLinks = await repository.ListEvidenceLinksAsync(investigationId, cancellationToken);
        var entityLinks = await repository.ListEntityLinksAsync(investigationId, cancellationToken);
        var timeline = BuildTimeline(investigation, pivots, runs, evidence, tags, comments, evidenceLinks, entityLinks);

        return new InvestigationHandoverSummary(
            investigation,
            pivots,
            runs,
            evidence,
            tags,
            comments,
            evidenceLinks,
            entityLinks,
            timeline);
    }

    private static IReadOnlyList<InvestigationTimelineItem> BuildTimeline(
        InvestigationRecord investigation,
        IReadOnlyList<InvestigationPivotRecord> pivots,
        IReadOnlyList<InvestigationQueryRunRecord> runs,
        IReadOnlyList<EvidenceRecord> evidence,
        IReadOnlyList<EvidenceTagRecord> tags,
        IReadOnlyList<EvidenceCommentRecord> comments,
        IReadOnlyList<EvidenceLinkRecord> evidenceLinks,
        IReadOnlyList<EvidenceEntityLinkRecord> entityLinks)
        => new[] { new InvestigationTimelineItem(investigation.CreatedAtUtc, "investigation", investigation.Id, investigation.Title, investigation.CreatedBy) }
            .Concat(pivots.Select(p => new InvestigationTimelineItem(p.CreatedAtUtc, "pivot", p.Id, p.Name, null)))
            .Concat(runs.Select(r => new InvestigationTimelineItem(r.StartedAtUtc, "query-run", r.Id, $"{r.RowCount ?? 0} rows", null)))
            .Concat(evidence.Select(e => new InvestigationTimelineItem(e.CreatedAtUtc, "evidence", e.Id, e.Summary ?? "Evidence promoted", e.CreatedBy)))
            .Concat(tags.Select(t => new InvestigationTimelineItem(t.AddedAtUtc, "tag", t.EvidenceId, t.Tag, t.AddedBy)))
            .Concat(comments.Select(c => new InvestigationTimelineItem(c.CreatedAtUtc, "comment", c.Id, c.Body, c.CreatedBy)))
            .Concat(evidenceLinks.Select(l => new InvestigationTimelineItem(l.CreatedAtUtc, "evidence-link", l.Id, l.Relationship, l.CreatedBy)))
            .Concat(entityLinks.Select(l => new InvestigationTimelineItem(l.CreatedAtUtc, "entity-link", l.Id, $"{l.EntityKind}:{l.EntityKey}", l.CreatedBy)))
            .OrderBy(item => item.OccurredAtUtc)
            .ToArray();

    private static string? BuildSummary(IReadOnlyList<EvidenceCell> row)
        => string.Join(", ", row.Take(3).Select(c => $"{c.Name}={c.Value}"));

    private sealed record EvidenceCell(string Name, string TypeName, object? Value);
}
