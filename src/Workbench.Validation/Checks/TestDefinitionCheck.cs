using Workbench.Application.Abstractions;
using Workbench.Domain.Enums;
using YamlDotNet.RepresentationModel;

namespace Workbench.Validation.Checks;

/// <summary>
/// Validates that test definition files are non-empty, parseable YAML. Does not execute
/// tests — test execution is a future pipeline stage.
/// </summary>
public sealed class TestDefinitionCheck : ICheck
{
    public string Name => "test-definition";
    public bool IsBlocking => true;

    public IReadOnlySet<DraftContentType> ApplicableContentTypes { get; } =
        new HashSet<DraftContentType> { DraftContentType.TestDefinition }.AsReadOnly();

    public Task<CheckOutcome> RunAsync(CheckContext context, CancellationToken ct = default)
    {
        var tests = context.DraftFiles
            .Where(f => f.ContentType == DraftContentType.TestDefinition)
            .ToList();

        if (tests.Count == 0)
        {
            return Task.FromResult(CheckOutcome.Skip("No test definition files in draft set."));
        }

        var errors = new List<string>();

        foreach (var test in tests)
        {
            if (string.IsNullOrWhiteSpace(test.Content))
            {
                errors.Add($"{test.LogicalPath}: empty YAML document.");
                continue;
            }

            try
            {
                var yaml = new YamlStream();
                using var reader = new StringReader(test.Content);
                yaml.Load(reader);

                if (yaml.Documents.Count == 0)
                {
                    errors.Add($"{test.LogicalPath}: empty YAML document.");
                }
            }
            catch (Exception ex)
            {
                errors.Add($"{test.LogicalPath}: YAML parse error: {ex.Message}");
            }
        }

        if (errors.Count == 0)
        {
            return Task.FromResult(CheckOutcome.Pass(
                $"Test definition check passed ({tests.Count} file(s)). " +
                "Note: structural validation only — test execution is not yet implemented."));
        }

        return Task.FromResult(CheckOutcome.Fail(
            $"{errors.Count} test definition error(s).",
            "{}",
            string.Join('\n', errors)));
    }
}