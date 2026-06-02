namespace Hunting.Tests.Web;

[TestClass]
public sealed class MonacoLanguageMetadataGuardTests
{
    [TestMethod]
    public void MonacoLanguage_ConsumesGeneratedTermsAndHandlesHyphenatedCompletionFragments()
    {
        var source = ReadRepositorySource("src", "Hunting.Web", "wwwroot", "js", "monaco.js");

        Assert.Contains("const keywords = language.keywords ?? [];", source);
        Assert.Contains("const operators = language.operators ?? [];", source);
        Assert.Contains("const renderKinds = language.renderKinds ?? [];", source);
        Assert.Contains("hyphenatedKeywords.map", source);
        Assert.Contains("[hyphenatedKeywordPattern, 'keyword']", source);
        Assert.Contains("triggerCharacters: ['|', ' ', '.', '-']", source);
        Assert.Contains("linePrefix.match(/!?[A-Za-z_][\\w-]*$/)", source);
        Assert.DoesNotContain("'mv-expand'", source);
    }

    [TestMethod]
    public void LanguageService_SetsGeneratedMetadataBeforeRegisteringMonacoLanguage()
    {
        var source = ReadRepositorySource("src", "Hunting.Web", "Services", "LanguageService.cs");
        var setSchema = source.IndexOf("await SetSchemaAsync(schema);", StringComparison.Ordinal);
        var registerLanguage = source.IndexOf(
            "await _jsRuntime.InvokeVoidAsync(\"huntingMonaco.registerKqlLanguage\");",
            StringComparison.Ordinal);

        Assert.IsGreaterThanOrEqualTo(0, setSchema);
        Assert.IsGreaterThan(setSchema, registerLanguage);
    }

    private static string ReadRepositorySource(params string[] relativePath)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            var candidate = Path.Combine([directory.FullName, .. relativePath]);
            if (File.Exists(candidate))
            {
                return File.ReadAllText(candidate);
            }

            directory = directory.Parent;
        }

        Assert.Fail($"Could not locate {Path.Combine(relativePath)} from the test output directory.");
        return string.Empty;
    }
}