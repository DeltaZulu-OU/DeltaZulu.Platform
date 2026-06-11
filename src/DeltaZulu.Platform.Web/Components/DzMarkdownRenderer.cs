using Markdig;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

namespace DeltaZulu.Platform.Web.Components;

/// <summary>Shared Markdown rendering helper used by DeltaZulu Markdown display components.</summary>
public static class DzMarkdownRenderer
{
    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
        .UseAdvancedExtensions()
        .UseYamlFrontMatter()
        .Build();

    /// <summary>
    /// Renders Markdown to HTML, resolving relative links before an optional app-specific mapper
    /// turns them into route hrefs.
    /// </summary>
    public static string RenderToHtml(
        string content,
        string? currentDocumentPath = null,
        Func<string, string>? linkMapper = null,
        bool allowRawHtml = false)
    {
        ArgumentNullException.ThrowIfNull(content);

        if (content.Length == 0)
        {
            return string.Empty;
        }

        var markdown = allowRawHtml ? content : EscapeRawHtml(content);
        var document = Markdown.Parse(markdown, Pipeline);
        RewriteLinks(document, currentDocumentPath, linkMapper);

        using var writer = new StringWriter();
        var renderer = new Markdig.Renderers.HtmlRenderer(writer);
        Pipeline.Setup(renderer);
        renderer.Render(document);
        writer.Flush();
        return writer.ToString();
    }

    private static string EscapeRawHtml(string content) =>
        content.Replace("<", "&lt;", StringComparison.Ordinal)
            .Replace(">", "&gt;", StringComparison.Ordinal);

    private static void RewriteLinks(
        MarkdownDocument document,
        string? currentDocumentPath,
        Func<string, string>? linkMapper)
    {
        foreach (var link in document.Descendants<LinkInline>())
        {
            if (link.Url is null || IsExternalOrAnchor(link.Url))
            {
                continue;
            }

            var resolved = ResolveRelativePath(link.Url, currentDocumentPath);
            link.Url = linkMapper?.Invoke(resolved) ?? resolved;
        }
    }

    private static bool IsExternalOrAnchor(string url) =>
        url.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
        || url.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
        || url.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase)
        || url.StartsWith('#');

    private static string ResolveRelativePath(string relativePath, string? currentDocumentPath)
    {
        if (currentDocumentPath is null)
        {
            return NormalizePath(relativePath);
        }

        var currentDirectory = currentDocumentPath.Contains('/', StringComparison.Ordinal)
            ? currentDocumentPath[..currentDocumentPath.LastIndexOf('/')]
            : string.Empty;

        return NormalizePath($"{currentDirectory}/{relativePath}");
    }

    private static string NormalizePath(string path)
    {
        var parts = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var resolved = new Stack<string>();

        foreach (var part in parts)
        {
            if (part == ".")
            {
                continue;
            }

            if (part == "..")
            {
                if (resolved.Count > 0)
                {
                    resolved.Pop();
                }

                continue;
            }

            resolved.Push(part);
        }

        return string.Join('/', resolved.Reverse());
    }
}