using Workbench.Application.Abstractions;
using Workbench.Domain.Enums;
using YamlDotNet.RepresentationModel;

namespace Workbench.Validation.Checks;

/// <summary>
/// Validates YAML frontmatter in investigation notes. Checks that the frontmatter block
/// (delimited by <c>---</c>) is parseable YAML and that observable entries, if present,
/// have <c>type</c> and <c>value</c> fields. Non-blocking: frontmatter is optional per
/// ADR-0015 and malformed frontmatter should not block merge.
/// </summary>
public sealed class NoteFrontmatterCheck : ICheck
{
    public string Name => "note-frontmatter";
    public bool IsBlocking => false;

    public IReadOnlySet<DraftContentType> ApplicableContentTypes { get; } =
        new HashSet<DraftContentType> { DraftContentType.InvestigationNote }.AsReadOnly();

    public Task<CheckOutcome> RunAsync(CheckContext context, CancellationToken ct = default)
    {
        var notes = context.DraftFiles
            .Where(f => f.ContentType == DraftContentType.InvestigationNote)
            .ToList();

        if (notes.Count == 0)
        {
            return Task.FromResult(CheckOutcome.Skip("No investigation notes in draft set."));
        }

        var warnings = new List<string>();

        foreach (var note in notes)
        {
            var frontmatter = ExtractFrontmatter(note.Content);
            if (frontmatter is null)
            {
                // No frontmatter — valid, just no metadata.
                continue;
            }

            try
            {
                var yaml = new YamlStream();
                using var reader = new StringReader(frontmatter);
                yaml.Load(reader);

                if (yaml.Documents.Count == 0)
                {
                    continue;
                }

                var root = yaml.Documents[0].RootNode as YamlMappingNode;
                if (root is null)
                {
                    warnings.Add($"{note.LogicalPath}: frontmatter root is not a mapping.");
                    continue;
                }

                // Validate observables if present.
                if (root.TryGetChild("observables", out var observablesNode) && observablesNode is YamlSequenceNode seq)
                {
                    for (var i = 0; i < seq.Children.Count; i++)
                    {
                        if (seq.Children[i] is YamlMappingNode entry)
                        {
                            if (!entry.ContainsScalarKey("type"))
                            {
                                warnings.Add($"{note.LogicalPath}: observable [{i}] missing 'type' field.");
                            }

                            if (!entry.ContainsScalarKey("value"))
                            {
                                warnings.Add($"{note.LogicalPath}: observable [{i}] missing 'value' field.");
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                warnings.Add($"{note.LogicalPath}: frontmatter YAML parse error: {ex.Message}");
            }
        }

        if (warnings.Count == 0)
        {
            return Task.FromResult(CheckOutcome.Pass(
                $"Note frontmatter valid ({notes.Count} note(s) checked)."));
        }

        // Non-blocking: return Pass with warnings in the logs, not Fail.
        return Task.FromResult(CheckOutcome.Pass(
            $"Note frontmatter has {warnings.Count} warning(s).",
            "{}",
            string.Join('\n', warnings)));
    }

    /// <summary>
    /// Extracts the YAML frontmatter block delimited by <c>---</c> from the beginning of
    /// a markdown file. Returns <c>null</c> if no frontmatter is present.
    /// </summary>
    public static string? ExtractFrontmatter(string content)
    {
        if (!content.StartsWith("---", StringComparison.Ordinal))
        {
            return null;
        }

        var endIndex = content.IndexOf("\n---", 3, StringComparison.Ordinal);
        if (endIndex < 0)
        {
            return null;
        }

        // Skip the first "---\n" and return up to the closing "---".
        var start = content.IndexOf('\n', 0) + 1;
        return start > endIndex ? null : content[start..endIndex];
    }
}
