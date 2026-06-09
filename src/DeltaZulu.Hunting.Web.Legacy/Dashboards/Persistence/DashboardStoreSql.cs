namespace DeltaZulu.Hunting.Web.Dashboards.Persistence;

internal static class DashboardStoreSql
{
    public const string CreateSchema =
        """
        CREATE TABLE IF NOT EXISTS dashboards (
            id TEXT PRIMARY KEY,
            name TEXT NOT NULL,
            description TEXT NULL,
            widget_count INTEGER NOT NULL DEFAULT 0,
            definition_json TEXT NOT NULL,
            created_at_utc TEXT NOT NULL,
            updated_at_utc TEXT NOT NULL
        );

        CREATE INDEX IF NOT EXISTS idx_dashboards_updated_at_utc
            ON dashboards (updated_at_utc DESC, name ASC);
        """;

    public const string List =
        """
        SELECT
            id AS Id,
            name AS Name,
            description AS Description,
            widget_count AS WidgetCount,
            created_at_utc AS CreatedAtUtc,
            updated_at_utc AS UpdatedAtUtc
        FROM dashboards
        ORDER BY updated_at_utc DESC, name ASC;
        """;

    public const string Get =
        """
        SELECT
            definition_json AS DefinitionJson
        FROM dashboards
        WHERE id = @Id;
        """;

    public const string Upsert =
        """
        INSERT INTO dashboards (
            id,
            name,
            description,
            widget_count,
            definition_json,
            created_at_utc,
            updated_at_utc
        )
        VALUES (
            @Id,
            @Name,
            @Description,
            @WidgetCount,
            @DefinitionJson,
            @CreatedAtUtc,
            @UpdatedAtUtc
        )
        ON CONFLICT(id) DO UPDATE SET
            name = excluded.name,
            description = excluded.description,
            widget_count = excluded.widget_count,
            definition_json = excluded.definition_json,
            updated_at_utc = excluded.updated_at_utc;
        """;

    public const string Delete =
        """
        DELETE FROM dashboards
        WHERE id = @Id;
        """;
}
