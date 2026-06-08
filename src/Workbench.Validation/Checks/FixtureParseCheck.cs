using System.Text.Json;
using Workbench.Application.Abstractions;
using Workbench.Domain.Enums;

namespace Workbench.Validation.Checks;

/// <summary>
/// Validates that fixture files (<c>.ndjson</c>, <c>.csv</c>) are parseable. For NDJSON,
/// each line must be valid JSON. For CSV, at least a header line must be present.
/// </summary>
public sealed class FixtureParseCheck : ICheck
{
    public string Name => "fixture-parse";
    public bool IsBlocking => true;

    public IReadOnlySet<DraftContentType> ApplicableContentTypes { get; } =
        new HashSet<DraftContentType> { DraftContentType.Fixture }.AsReadOnly();

    public Task<CheckOutcome> RunAsync(CheckContext context, CancellationToken ct = default)
    {
        var fixtures = context.DraftFiles
            .Where(f => f.ContentType == DraftContentType.Fixture)
            .ToList();

        if (fixtures.Count == 0)
        {
            return Task.FromResult(CheckOutcome.Skip("No fixture files in draft set."));
        }

        var errors = new List<string>();

        foreach (var fixture in fixtures)
        {
            if (string.IsNullOrWhiteSpace(fixture.Content))
            {
                errors.Add($"{fixture.LogicalPath}: fixture content is empty.");
                continue;
            }

            if (fixture.LogicalPath.EndsWith(".ndjson", StringComparison.OrdinalIgnoreCase))
            {
                ValidateNdjson(fixture, errors);
            }
            else if (fixture.LogicalPath.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
            {
                ValidateCsv(fixture, errors);
            }
            // Other fixture formats: pass without further validation in POC.
        }

        return errors.Count == 0
            ? Task.FromResult(CheckOutcome.Pass(
                $"Fixture parse check passed ({fixtures.Count} file(s))."))
            : Task.FromResult(CheckOutcome.Fail(
            $"{errors.Count} fixture parse error(s).",
            "{}",
            string.Join('\n', errors)));
    }

    private static void ValidateNdjson(DraftFileSnapshot file, List<string> errors)
    {
        var lines = file.Content.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i].Trim();
            if (line.Length == 0)
            {
                continue;
            }

            try
            {
                JsonDocument.Parse(line).Dispose();
            }
            catch (JsonException ex)
            {
                errors.Add($"{file.LogicalPath}:{i + 1}: invalid JSON: {ex.Message}");
                if (errors.Count > 20)
                {
                    errors.Add($"{file.LogicalPath}: (truncated after 20 errors)");
                    return;
                }
            }
        }
    }

    private static void ValidateCsv(DraftFileSnapshot file, List<string> errors)
    {
        var lines = file.Content.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        if (lines.Length == 0)
        {
            errors.Add($"{file.LogicalPath}: CSV file has no header line.");
            return;
        }

        var headerFields = lines[0].Split(',');
        if (headerFields.Length < 2)
        {
            errors.Add($"{file.LogicalPath}: CSV header has fewer than 2 fields — likely malformed.");
        }
    }
}
