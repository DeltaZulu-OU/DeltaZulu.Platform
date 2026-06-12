using System.Text.RegularExpressions;
using DeltaZulu.Platform.Web.Components;

namespace DeltaZulu.Platform.Tests.Web;

[TestClass]
public sealed class DesignSystemAuditTests
{
    private static readonly Regex MediumRadiusLiteralPattern = new(
        @"^[^\S\r\n]*--radius-(?:xs|sm|md|lg|xl|2xl):(?![^\S\r\n]*var\(--radius-structure\);)[^\r\n;]+;",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.Multiline);

    [TestMethod]
    public void ProductIdentityDocument_DefinesDeltaZuluPlatformAsApplicationIdentity()
    {
        var repositoryRoot = FindRepositoryRoot();
        var identity = File.ReadAllText(Path.Combine(repositoryRoot, "docs", "design", "PRODUCT_IDENTITY.md"));

        StringAssert.Contains(identity, "DeltaZulu Platform is the product name");
        StringAssert.Contains(identity, "Use **Analytics**, **Detection Content Governance**, and **Operations** as the three module names.");
        StringAssert.Contains(identity, "Do not use DZNS as the application name inside the product shell.");
    }

    [TestMethod]
    public void DeltaZuluTheme_UsesSharpMudBlazorStructuralRadiusAndProductTypography()
    {
        var theme = DeltaZuluTheme.Create();
        var fontFamily = string.Join(" ", theme.Typography.H1.FontFamily ?? []);

        Assert.AreEqual("0px", theme.LayoutProperties.DefaultBorderRadius);
        StringAssert.Contains(fontFamily, "IBM Plex Sans");
        Assert.IsFalse(fontFamily.Contains("Newsreader", StringComparison.Ordinal), "Product heading typography must not use Newsreader.");
    }

    [TestMethod]
    public void DesignTokens_KeepStructuralRadiusAliasesSharp()
    {
        var repositoryRoot = FindRepositoryRoot();
        var tokens = File.ReadAllText(Path.Combine(repositoryRoot, "src", "DeltaZulu.Platform.Web", "wwwroot", "deltazulu-tokens.css"));

        StringAssert.Contains(tokens, "--radius-structure: 0;");
        StringAssert.Contains(tokens, "--radius-input: 4px;");
        StringAssert.Contains(tokens, "--radius-pill: 999px;");

        foreach (var alias in new[] { "xs", "sm", "md", "lg", "xl", "2xl" })
        {
            StringAssert.Contains(tokens, $"--radius-{alias}: var(--radius-structure);");
        }

        var disallowedMediumAlias = MediumRadiusLiteralPattern.Match(tokens);
        Assert.IsFalse(disallowedMediumAlias.Success, $"Structural radius aliases must resolve to --radius-structure, found '{disallowedMediumAlias.Value}'.");
    }

    [TestMethod]
    public void DesignTokens_DoNotApplyNewsreaderToGlobalProductHeadings()
    {
        var repositoryRoot = FindRepositoryRoot();
        var tokens = File.ReadAllText(Path.Combine(repositoryRoot, "src", "DeltaZulu.Platform.Web", "wwwroot", "deltazulu-tokens.css"));

        StringAssert.Contains(tokens, ".text-display");
        AssertCssRuleContains(tokens, ".text-h1,\nh1", "font-family: var(--font-family-sans);");
        AssertCssRuleDoesNotContain(tokens, ".text-h1,\nh1", "font-family: var(--font-family-display);");
    }

    [TestMethod]
    public void SharedStylesheets_DoNotReintroduceLegacyHuntingAliasTokens()
    {
        var repositoryRoot = FindRepositoryRoot();
        var sharedStylesheets = Directory.EnumerateFiles(
                Path.Combine(repositoryRoot, "src", "DeltaZulu.Platform.Web", "wwwroot"),
                "*.css",
                SearchOption.TopDirectoryOnly)
            .Select(path => (Path: path, Text: File.ReadAllText(path)))
            .ToList();

        foreach (var stylesheet in sharedStylesheets)
        {
            AssertNoLegacyAlias(stylesheet.Path, stylesheet.Text, "--hunt-");
            AssertNoLegacyAlias(stylesheet.Path, stylesheet.Text, "--bg-");
            AssertNoLegacyAlias(stylesheet.Path, stylesheet.Text, "--text-");
        }
    }

    private static void AssertCssRuleContains(string css, string selector, string expected)
    {
        var rule = ExtractRule(css, selector);
        StringAssert.Contains(rule, expected);
    }

    private static void AssertCssRuleDoesNotContain(string css, string selector, string disallowed)
    {
        var rule = ExtractRule(css, selector);
        Assert.IsFalse(rule.Contains(disallowed, StringComparison.Ordinal), $"Selector '{selector}' must not contain '{disallowed}'.");
    }

    private static string ExtractRule(string css, string selector)
    {
        css = css.Replace("\r\n", "\n", StringComparison.Ordinal);
        selector = selector.Replace("\r\n", "\n", StringComparison.Ordinal);

        var selectorIndex = css.IndexOf(selector, StringComparison.Ordinal);
        Assert.IsGreaterThanOrEqualTo(0, selectorIndex, $"Expected selector '{selector}'.");

        var openBraceIndex = css.IndexOf('{', selectorIndex);
        Assert.IsGreaterThanOrEqualTo(0, openBraceIndex, $"Expected selector '{selector}' to open a rule.");

        var closeBraceIndex = css.IndexOf('}', openBraceIndex);
        Assert.IsGreaterThanOrEqualTo(0, closeBraceIndex, $"Expected selector '{selector}' to close a rule.");

        return css[selectorIndex..(closeBraceIndex + 1)];
    }

    private static void AssertNoLegacyAlias(string path, string text, string aliasPrefix)
    {
        Assert.IsFalse(
            text.Contains(aliasPrefix, StringComparison.Ordinal),
            $"Shared stylesheet '{Path.GetFileName(path)}' must not contain legacy alias prefix '{aliasPrefix}'.");
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "DeltaZulu.Platform.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        Assert.Fail("Could not locate repository root from test base directory.");
        return string.Empty;
    }
}
