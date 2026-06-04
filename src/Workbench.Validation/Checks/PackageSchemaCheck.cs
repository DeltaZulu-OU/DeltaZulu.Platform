using Workbench.Application.Abstractions;
using Workbench.Domain.Enums;
using YamlDotNet.RepresentationModel;

namespace Workbench.Validation.Checks;

/// <summary>
/// Validates that the detection metadata YAML (<c>detection.yaml</c>) contains required
/// fields. This is a structural check, not a semantic one — it verifies presence and type,
/// not correctness of values.
/// </summary>
public sealed class PackageSchemaCheck : ICheck
{
    public string Name => "package-schema";
    public bool IsBlocking => true;

    public IReadOnlySet<DraftContentType> ApplicableContentTypes { get; } =
        new HashSet<DraftContentType> { DraftContentType.DetectionMetadata }.AsReadOnly();

    private static readonly string[] RequiredFields = ["id", "title", "description", "severity"];

    public Task<CheckOutcome> RunAsync(CheckContext context, CancellationToken ct = default)
    {
        var metadataFiles = context.DraftFiles
            .Where(f => f.ContentType == DraftContentType.DetectionMetadata)
            .ToList();

        if (metadataFiles.Count == 0)
        {
            return Task.FromResult(CheckOutcome.Skip("No detection metadata file in draft set."));
        }

        var errors = new List<string>();

        foreach (var file in metadataFiles)
        {
            try
            {
                var yaml = new YamlStream();
                using var reader = new StringReader(file.Content);
                yaml.Load(reader);

                if (yaml.Documents.Count == 0)
                {
                    errors.Add($"{file.LogicalPath}: empty YAML document.");
                    continue;
                }

                var root = yaml.Documents[0].RootNode as YamlMappingNode;
                if (root is null)
                {
                    errors.Add($"{file.LogicalPath}: root node is not a mapping.");
                    continue;
                }

                var presentKeys = root.Children.Keys
                    .OfType<YamlScalarNode>()
                    .Select(k => k.Value)
                    .Where(v => v is not null)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);

                foreach (var field in RequiredFields)
                {
                    if (!presentKeys.Contains(field))
                    {
                        errors.Add($"{file.LogicalPath}: missing required field '{field}'.");
                    }
                }
            }
            catch (Exception ex)
            {
                errors.Add($"{file.LogicalPath}: YAML parse error: {ex.Message}");
            }
        }

        if (errors.Count == 0)
        {
            return Task.FromResult(CheckOutcome.Pass(
                $"Package schema valid ({metadataFiles.Count} metadata file(s) checked)."));
        }

        var summary = $"{errors.Count} schema error(s) found.";
        var logsExcerpt = string.Join('\n', errors);
        return Task.FromResult(CheckOutcome.Fail(summary, "{}", logsExcerpt));
    }
}
