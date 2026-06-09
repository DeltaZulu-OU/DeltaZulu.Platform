using Dapper;
using DeltaZulu.Workbench.Application.Abstractions;
using DeltaZulu.Workbench.Domain.Detections;
using DeltaZulu.Workbench.Domain.Enums;
using DeltaZulu.Workbench.Domain.Identifiers;

namespace DeltaZulu.Workbench.Persistence.Repositories;

internal sealed class DetectionRepository(DapperSession session) : IDetectionRepository
{
    public async Task<Detection?> GetByIdAsync(DetectionId id, CancellationToken ct = default)
    {
        var row = await session.Connection.QuerySingleOrDefaultAsync<DetectionRow>(
            "SELECT * FROM detections WHERE id = @Id",
            new { Id = id.Value.ToString() },
            session.Transaction);
        return row?.ToDomain();
    }

    public async Task<Detection?> GetBySlugAsync(string slug, CancellationToken ct = default)
    {
        var row = await session.Connection.QuerySingleOrDefaultAsync<DetectionRow>(
            "SELECT * FROM detections WHERE slug = @Slug",
            new { Slug = slug },
            session.Transaction);
        return row?.ToDomain();
    }

    public async Task<IReadOnlyList<Detection>> ListAsync(CancellationToken ct = default)
    {
        var rows = await session.Connection.QueryAsync<DetectionRow>(
            "SELECT * FROM detections ORDER BY updated_at DESC",
            transaction: session.Transaction);
        return rows.Select(r => r.ToDomain()).ToList();
    }

    public void Add(Detection detection)
    {
        session.Connection.Execute("""
            INSERT INTO detections (id, slug, title, summary, lifecycle, current_version_id, created_at, updated_at)
            VALUES (@Id, @Slug, @Title, @Summary, @Lifecycle, @CurrentVersionId, @CreatedAt, @UpdatedAt)
            """,
            ToParams(detection),
            session.Transaction);
    }

    public void Save(Detection detection)
    {
        session.Connection.Execute("""
            UPDATE detections SET title = @Title, summary = @Summary, lifecycle = @Lifecycle,
                current_version_id = @CurrentVersionId, updated_at = @UpdatedAt
            WHERE id = @Id
            """,
            ToParams(detection),
            session.Transaction);
    }

    private static object ToParams(Detection d) => new
    {
        Id = d.Id.Value.ToString(),
        d.Slug,
        d.Title,
        d.Summary,
        Lifecycle = d.Lifecycle.ToString(),
        CurrentVersionId = d.CurrentVersionId?.Value.ToString(),
        CreatedAt = d.CreatedAt.ToString("O"),
        UpdatedAt = d.UpdatedAt.ToString("O"),
    };

    internal sealed class DetectionRow
    {
        public string id { get; set; } = "";
        public string slug { get; set; } = "";
        public string title { get; set; } = "";
        public string summary { get; set; } = "";
        public string lifecycle { get; set; } = "";
        public string? current_version_id { get; set; }
        public string created_at { get; set; } = "";
        public string updated_at { get; set; } = "";

        public Detection ToDomain() => Detection.Reconstitute(
            new DetectionId(Guid.Parse(id)),
            slug, title, summary,
            ParseLifecycle(lifecycle),
            current_version_id is not null ? new VersionId(Guid.Parse(current_version_id)) : null,
            DateTimeOffset.Parse(created_at),
            DateTimeOffset.Parse(updated_at));

        private static DetectionLifecycle ParseLifecycle(string lifecycle) => lifecycle switch
        {
            "Conceived" => DetectionLifecycle.Draft,
            _ => Enum.Parse<DetectionLifecycle>(lifecycle),
        };
    }
}
