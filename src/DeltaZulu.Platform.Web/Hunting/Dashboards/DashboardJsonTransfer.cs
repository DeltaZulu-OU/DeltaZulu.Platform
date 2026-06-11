
using System.Text.Json;

namespace DeltaZulu.Platform.Web.Hunting.Dashboards;
public static class DashboardJsonTransfer
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    public static string Export(DashboardDefinition dashboard)
    {
        ArgumentNullException.ThrowIfNull(dashboard);
        return JsonSerializer.Serialize(dashboard, Options);
    }

    public static DashboardDefinition ImportAsCopy(string json, DateTime? nowUtc = null)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            throw new InvalidOperationException("Dashboard JSON is empty.");
        }

        DashboardDefinition? imported;
        try
        {
            imported = JsonSerializer.Deserialize<DashboardDefinition>(json, Options);
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException("Dashboard JSON could not be parsed.", ex);
        }

        if (imported?.IsValid() != true)
        {
            throw new InvalidOperationException("Dashboard JSON did not contain a dashboard definition.");
        }

        var now = nowUtc ?? DateTime.UtcNow;
        var copy = imported with
        {
            Id = Guid.NewGuid().ToString("N"),
            Name = BuildImportedName(imported.Name),
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        var validationErrors = DashboardModelValidator.Validate(copy);
        if (validationErrors.Count > 0)
        {
            throw new InvalidOperationException(string.Join(" ", validationErrors));
        }

        return copy;
    }

    private static string BuildImportedName(string name)
    {
        var trimmed = string.IsNullOrWhiteSpace(name)
            ? "Imported dashboard"
            : name.Trim();

        return trimmed.EndsWith("(imported)", StringComparison.OrdinalIgnoreCase)
            ? trimmed
            : $"{trimmed} (imported)";
    }
}