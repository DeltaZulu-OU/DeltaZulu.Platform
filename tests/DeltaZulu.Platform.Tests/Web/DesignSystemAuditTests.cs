using System.Text.RegularExpressions;
using DeltaZulu.Platform.Web.Components;

namespace DeltaZulu.Platform.Tests.Web;

[TestClass]
public sealed partial class DesignSystemAuditTests
{
    [TestMethod]
    public void ProductIdentityDocument_DefinesDeltaZuluPlatformAsApplicationIdentity()
    {
        var repositoryRoot = FindRepositoryRoot();
        var identity = File.ReadAllText(Path.Combine(repositoryRoot, "docs", "design", "PRODUCT_IDENTITY.md"));

        Assert.Contains("DeltaZulu Platform is the product name", identity);
        Assert.Contains("Use **Analytics**, **Detection Content Governance**, and **Operations** as the three module names.", identity);
        Assert.Contains("Do not use DZNS as the application name inside the product shell.", identity);
    }

    [TestMethod]
    public void DeltaZuluTheme_UsesSharpMudBlazorStructuralRadiusAndProductTypography()
    {
        var theme = DeltaZuluTheme.Create();
        var fontFamily = string.Join(" ", theme.Typography.H1.FontFamily ?? []);

        Assert.AreEqual("0px", theme.LayoutProperties.DefaultBorderRadius);
        Assert.Contains("IBM Plex Sans", fontFamily);
        Assert.IsFalse(fontFamily.Contains("Newsreader", StringComparison.Ordinal), "Product heading typography must not use Newsreader.");
    }

    [TestMethod]
    public void DesignTokens_KeepStructuralRadiusAliasesSharp()
    {
        var repositoryRoot = FindRepositoryRoot();
        var tokens = File.ReadAllText(Path.Combine(repositoryRoot, "src", "DeltaZulu.Platform.Web", "wwwroot", "css", "deltazulu-tokens.css"));

        Assert.Contains("--radius-structure: 0;", tokens);
        Assert.Contains("--radius-input: 4px;", tokens);
        Assert.Contains("--radius-pill: 999px;", tokens);

        foreach (var alias in new[] { "xs", "sm", "md", "lg", "xl", "2xl" })
        {
            Assert.Contains($"--radius-{alias}: var(--radius-structure);", tokens);
        }

        var disallowedMediumAlias = MediumRadiusLiteralRegex().Match(tokens);
        Assert.IsFalse(disallowedMediumAlias.Success, $"Structural radius aliases must resolve to --radius-structure, found '{disallowedMediumAlias.Value}'.");
    }

    [TestMethod]
    public void DesignTokens_DoNotApplyNewsreaderToGlobalProductHeadings()
    {
        var repositoryRoot = FindRepositoryRoot();
        var tokens = File.ReadAllText(Path.Combine(repositoryRoot, "src", "DeltaZulu.Platform.Web", "wwwroot", "css", "deltazulu-tokens.css"));

        Assert.Contains(".text-display", tokens);
        AssertCssRuleContains(tokens, ".text-h1,\r\nh1", "font-family: var(--font-family-sans);");
        AssertCssRuleDoesNotContain(tokens, ".text-h1,\r\nh1", "font-family: var(--font-family-display);");
    }

    [TestMethod]
    public void SharedStylesheets_DoNotReintroduceLegacyHuntingAliasTokens()
    {
        var repositoryRoot = FindRepositoryRoot();

        // analytics-app.css and its companion drawer stylesheet are the deliberately
        // quarantined carriers of the legacy --hunt-*/--bg-*/--text-* alias layer (see the
        // header comment in analytics-app.css). Every other stylesheet under wwwroot/css is
        // shared/product chrome and must use --dz-product-*/--color-*/--brand-* tokens
        // directly instead of reintroducing these aliases.
        var quarantinedStylesheets = new HashSet<string>(StringComparer.OrdinalIgnoreCase) {
            "analytics-app.css",
            "kql-helper-drawer.css",
        };

        var sharedStylesheets = Directory.EnumerateFiles(
                Path.Combine(repositoryRoot, "src", "DeltaZulu.Platform.Web", "wwwroot", "css"),
                "*.css",
                SearchOption.TopDirectoryOnly)
            .Where(path => !quarantinedStylesheets.Contains(Path.GetFileName(path)))
            .Select(path => (Path: path, Text: File.ReadAllText(path)))
            .ToList();

        Assert.IsNotEmpty(sharedStylesheets, "Expected shared stylesheets under wwwroot/css to scan.");

        foreach (var stylesheet in sharedStylesheets)
        {
            AssertNoLegacyAlias(stylesheet.Path, stylesheet.Text, "--hunt-");
            AssertNoLegacyAlias(stylesheet.Path, stylesheet.Text, "--bg-");
            AssertNoLegacyAlias(stylesheet.Path, stylesheet.Text, "--text-");
        }
    }

    [TestMethod]
    public void RazorComponents_DoNotApplyActionColorToNonActionInputControls()
    {
        var repositoryRoot = FindRepositoryRoot();
        var webRoot = Path.Combine(repositoryRoot, "src", "DeltaZulu.Platform.Web");

        var offenders = Directory.EnumerateFiles(webRoot, "*.razor", SearchOption.AllDirectories)
            .Select(path => (Relative: Path.GetRelativePath(webRoot, path), Text: File.ReadAllText(path)))
            .Where(file => PrimaryColoredInputControlRegex().IsMatch(file.Text))
            .Select(file => file.Relative)
            .ToList();

        Assert.HasCount(0, offenders,
            $"Orange action color (Color.Primary) applied to a non-action input control in: {string.Join(", ", offenders)}. " +
            "Per docs/design/PRODUCT_IDENTITY.md, orange is reserved for actions/CTAs; use Color.Secondary or a status color for form inputs.");
    }

    [TestMethod]
    public void RazorComponents_WrapMudTableInDzTableShell()
    {
        var repositoryRoot = FindRepositoryRoot();
        var webRoot = Path.Combine(repositoryRoot, "src", "DeltaZulu.Platform.Web");

        // DzQueryResultTable is itself the canonical evidence-grade table primitive.
        // Library.razor's "dashboard-list-panel" container predates DzTableShell and already
        // supplies its own bordered/rounded chrome; nesting DzTableShell there would double
        // the border and is tracked as follow-up design-system work rather than papered over.
        var exemptRelativePaths = new HashSet<string>(StringComparer.Ordinal) {
            Path.Combine("Analytics", "Components", "Dz", "DzQueryResultTable.razor"),
            Path.Combine("Analytics", "Pages", "Library.razor"),
        };

        var offenders = Directory.EnumerateFiles(webRoot, "*.razor", SearchOption.AllDirectories)
            .Select(path => (Relative: Path.GetRelativePath(webRoot, path), Text: File.ReadAllText(path)))
            .Where(file => file.Text.Contains("<MudTable", StringComparison.Ordinal)
                && !file.Text.Contains("DzTableShell", StringComparison.Ordinal)
                && !exemptRelativePaths.Contains(file.Relative))
            .Select(file => file.Relative)
            .ToList();

        Assert.HasCount(0, offenders,
            $"Raw <MudTable> without a DzTableShell wrapper (or an explicit, documented exemption) in: {string.Join(", ", offenders)}");
    }

    [TestMethod]
    public void RazorComponents_DoNotUseRawMudPaperOutsideDzPanel()
    {
        var repositoryRoot = FindRepositoryRoot();
        var webRoot = Path.Combine(repositoryRoot, "src", "DeltaZulu.Platform.Web");
        var exemptRelativePath = Path.Combine("Components", "DzPanel.razor");

        var offenders = Directory.EnumerateFiles(webRoot, "*.razor", SearchOption.AllDirectories)
            .Select(path => (Relative: Path.GetRelativePath(webRoot, path), Text: File.ReadAllText(path)))
            .Where(file => file.Text.Contains("<MudPaper", StringComparison.Ordinal)
                && !string.Equals(file.Relative, exemptRelativePath, StringComparison.Ordinal))
            .Select(file => file.Relative)
            .ToList();

        Assert.HasCount(0, offenders,
            $"Raw <MudPaper> outside the canonical DzPanel wrapper in: {string.Join(", ", offenders)}");
    }

    private static void AssertCssRuleContains(string css, string selector, string expected)
    {
        var rule = ExtractRule(css, selector);
        Assert.Contains(expected, rule);
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

    private static void AssertNoLegacyAlias(string path, string text, string aliasPrefix) => Assert.IsFalse(
            text.Contains(aliasPrefix, StringComparison.Ordinal),
            $"Shared stylesheet '{Path.GetFileName(path)}' must not contain legacy alias prefix '{aliasPrefix}'.");

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

    [GeneratedRegex(@"^[^\S\r\n]*--radius-(?:xs|sm|md|lg|xl|2xl):(?![^\S\r\n]*var\(--radius-structure\);)[^\r\n;]+;", RegexOptions.Multiline | RegexOptions.Compiled | RegexOptions.CultureInvariant)]
    private static partial Regex MediumRadiusLiteralRegex();

    [GeneratedRegex(@"<(MudRadio|MudCheckBox|MudSwitch|MudRadioGroup)\b[^>]*?Color\s*=\s*""[^""]*Color\.Primary[^""]*""", RegexOptions.Compiled | RegexOptions.CultureInvariant)]
    private static partial Regex PrimaryColoredInputControlRegex();
}