using DeltaZulu.Platform.Application.Workbench.Abstractions;
using DeltaZulu.Platform.Domain.Workbench.Enums;
using YamlDotNet.RepresentationModel;

namespace DeltaZulu.Platform.Application.Workbench.Validation.Checks;

/// <summary>
/// Validates that test definition files are non-empty, parseable YAML and runs the
/// POC's minimal static assertions against the draft query content.
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

        var queryText = string.Join(
            "\n",
            context.DraftFiles
                .Where(f => f.ContentType == DraftContentType.HuntingQuery)
                .OrderBy(f => f.LogicalPath, StringComparer.Ordinal)
                .Select(f => f.Content));

        var errors = new List<string>();
        var executedAssertions = 0;

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

                if (yaml.Documents.Count == 0 || yaml.Documents[0].RootNode is not YamlMappingNode root)
                {
                    errors.Add($"{test.LogicalPath}: empty YAML document.");
                    continue;
                }

                executedAssertions += RunAssertions(test.LogicalPath, root, queryText, errors);
            }
            catch (Exception ex)
            {
                errors.Add($"{test.LogicalPath}: YAML parse error: {ex.Message}");
            }
        }

        if (errors.Count == 0)
        {
            var assertionMessage = executedAssertions == 0
                ? "No executable assertions were declared."
                : $"Executed {executedAssertions} static assertion(s).";

            return Task.FromResult(CheckOutcome.Pass(
                $"Test definition check passed ({tests.Count} file(s)). {assertionMessage}"));
        }

        return Task.FromResult(CheckOutcome.Fail(
            $"{errors.Count} test definition error(s).",
            "{}",
            string.Join('\n', errors)));
    }

    private static int RunAssertions(
        string logicalPath,
        YamlMappingNode root,
        string queryText,
        List<string> errors)
    {
        if (!root.TryGetChild("assertions", out var assertionsNode))
        {
            return 0;
        }

        if (assertionsNode is not YamlSequenceNode assertions)
        {
            errors.Add($"{logicalPath}: assertions must be a YAML sequence.");
            return 0;
        }

        var executed = 0;

        for (var i = 0; i < assertions.Children.Count; i++)
        {
            var assertionPath = $"{logicalPath}: assertions[{i}]";

            if (assertions.Children[i] is not YamlMappingNode assertion)
            {
                errors.Add($"{assertionPath}: assertion must be a mapping with type and value fields.");
                continue;
            }

            if (!assertion.TryGetScalar("type", out var type) || string.IsNullOrWhiteSpace(type))
            {
                errors.Add($"{assertionPath}: missing assertion type.");
                continue;
            }

            if (!assertion.TryGetScalar("value", out var value) || string.IsNullOrEmpty(value))
            {
                errors.Add($"{assertionPath}: missing assertion value.");
                continue;
            }

            switch (type.Trim())
            {
                case "queryContains":
                    executed++;
                    if (!queryText.Contains(value, StringComparison.OrdinalIgnoreCase))
                    {
                        errors.Add($"{assertionPath}: query does not contain '{value}'.");
                    }

                    break;

                case "queryDoesNotContain":
                    executed++;
                    if (queryText.Contains(value, StringComparison.OrdinalIgnoreCase))
                    {
                        errors.Add($"{assertionPath}: query contains forbidden value '{value}'.");
                    }

                    break;

                default:
                    errors.Add($"{assertionPath}: unsupported assertion type '{type}'.");
                    break;
            }
        }

        return executed;
    }
}