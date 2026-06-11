namespace DeltaZulu.Platform.Web.Hunting.Dashboards.Markdown;

using System.Net;
using System.Text.RegularExpressions;
using Markdig;

public static partial class DashboardMarkdownRenderer
{
    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
        .UseAdvancedExtensions()
        .DisableHtml()
        .Build();

    public static string ToHtml(string markdown)
    {
        if (string.IsNullOrWhiteSpace(markdown))
        {
            return string.Empty;
        }

        var html = Markdown.ToHtml(markdown, Pipeline);
        return SanitizeLinks(html);
    }

    private static string SanitizeLinks(string html)
        => AnchorHrefRegex().Replace(
            html,
            match => {
                var decodedUrl = WebUtility.HtmlDecode(match.Groups[3].Value).Trim();
                if (!IsSafeUrl(decodedUrl))
                {
                    return match.Groups[1].Value + match.Groups[4].Value;
                }

                var encodedUrl = WebUtility.HtmlEncode(decodedUrl);
                return $"{match.Groups[1].Value} href=\"{encodedUrl}\" target=\"_blank\" rel=\"noopener noreferrer\"{match.Groups[4].Value}";
            });

    private static bool IsSafeUrl(string url)
        => Uri.TryCreate(url, UriKind.RelativeOrAbsolute, out var parsed)
            && (!parsed.IsAbsoluteUri
                || parsed.Scheme == Uri.UriSchemeHttp
                || parsed.Scheme == Uri.UriSchemeHttps
                || parsed.Scheme == Uri.UriSchemeMailto);

    [GeneratedRegex("(<a\\b[^>]*?)\\s+href=(['\\\"])(.*?)\\2([^>]*>)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex AnchorHrefRegex();
}