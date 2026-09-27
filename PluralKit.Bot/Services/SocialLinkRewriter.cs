using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace PluralKit.Bot;

public class SocialLinkRewriter
{
    private static readonly Regex UrlRegex = new(@"https?://[^\s<>()]+",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private readonly HttpClient _httpClient;
    private readonly Replacer[] _replacers;

    public SocialLinkRewriter(BotConfig config, HttpClient httpClient)
    {
        _httpClient = httpClient;
        _replacers = config.SocialLinkReplacers.Values
            .Select(CreateReplacer)
            .Where(replacer => replacer != null)
            .Cast<Replacer>()
            .ToArray();
    }

    public string[] RewriteLinks(string? content) =>
        string.IsNullOrWhiteSpace(content)
            ? Array.Empty<string>()
            : UrlRegex.Matches(content)
                .Select(match => Rewrite(match.Value.TrimEnd('.', ',', ';', ':', '!', '?')))
                .Where(link => link != null)
                .Distinct(StringComparer.Ordinal)
                .Cast<string>()
                .ToArray();

    public string? RewriteContent(string? content)
    {
        if (string.IsNullOrWhiteSpace(content))
            return null;

        var changed = false;
        var rewritten = UrlRegex.Replace(content, match =>
        {
            var trailingLength = match.Value.Length - match.Value.TrimEnd('.', ',', ';', ':', '!', '?').Length;
            var link = trailingLength == 0 ? match.Value : match.Value[..^trailingLength];
            var replacement = Rewrite(link);
            if (replacement == null)
                return match.Value;

            changed = true;
            return replacement + match.Value[^trailingLength..];
        });

        return changed ? rewritten : null;
    }

    public async Task<string?> RewriteContentAsync(string? content, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(content))
            return null;

        var changed = false;
        var position = 0;
        var rewritten = new StringBuilder(content.Length);
        foreach (Match match in UrlRegex.Matches(content))
        {
            rewritten.Append(content, position, match.Index - position);

            var trailingLength = match.Value.Length - match.Value.TrimEnd('.', ',', ';', ':', '!', '?').Length;
            var link = trailingLength == 0 ? match.Value : match.Value[..^trailingLength];
            var replacement = await RewriteAsync(link, cancellationToken);
            if (replacement == null)
                rewritten.Append(match.Value);
            else
            {
                changed = true;
                rewritten.Append(replacement);
                rewritten.Append(match.Value.AsSpan(match.Value.Length - trailingLength));
            }

            position = match.Index + match.Length;
        }

        rewritten.Append(content, position, content.Length - position);
        return changed ? rewritten.ToString() : null;
    }

    private string? Rewrite(string link)
    {
        if (!Uri.TryCreate(link, UriKind.Absolute, out var uri))
            return null;

        var replacer = _replacers.FirstOrDefault(candidate => candidate.Matches(uri));
        if (replacer?.ReplacementHost == null)
            return null;

        var builder = new UriBuilder(uri)
        {
            Host = replacer.ReplacementHost,
            Path = replacer.RewritePath(uri.AbsolutePath),
            Port = -1,
        };
        return builder.Uri.AbsoluteUri;
    }

    private async Task<string?> RewriteAsync(string link, CancellationToken cancellationToken)
    {
        if (!Uri.TryCreate(link, UriKind.Absolute, out var uri))
            return null;

        var replacer = _replacers.FirstOrDefault(candidate => candidate.Matches(uri));
        if (replacer?.ResolverUrl == null)
            return Rewrite(link);

        try
        {
            var resolverUri = new UriBuilder(replacer.ResolverUrl)
            {
                Query = $"q={Uri.EscapeDataString(link)}",
            }.Uri;
            var response = await _httpClient.GetFromJsonAsync<ResolverResponse>(resolverUri, cancellationToken);
            if (string.IsNullOrWhiteSpace(response?.Data?.Key))
                return null;

            return replacer.ResolvedUrlTemplate!.Replace("{key}", Uri.EscapeDataString(response.Data.Key),
                StringComparison.Ordinal);
        }
        catch (HttpRequestException)
        {
            return null;
        }
        catch (JsonException)
        {
            return null;
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return null;
        }
    }

    private static Replacer? CreateReplacer(SocialLinkReplacerConfig config)
    {
        var sourceHosts = config.SourceHosts?
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (sourceHosts == null || sourceHosts.Length == 0)
            return null;

        var hasReplacementHost = TryNormalizeHost(config.ReplacementHost, out var replacementHost);
        var hasResolver = Uri.TryCreate(config.ResolverUrl, UriKind.Absolute, out var resolverUrl) &&
                          resolverUrl.Scheme == Uri.UriSchemeHttps &&
                          config.ResolvedUrlTemplate?.Contains("{key}", StringComparison.Ordinal) == true;
        if (!hasReplacementHost && !hasResolver)
            return null;

        Regex? pathRegex = null;
        if (!string.IsNullOrWhiteSpace(config.PathPattern))
        {
            try
            {
                pathRegex = new Regex(config.PathPattern,
                    RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
            }
            catch (ArgumentException)
            {
                return null;
            }
        }

        if (!string.IsNullOrWhiteSpace(config.PathReplacement) && pathRegex == null)
            return null;

        return new Replacer(sourceHosts, hasReplacementHost ? replacementHost : null, pathRegex,
            config.PathReplacement, hasResolver ? resolverUrl : null, config.ResolvedUrlTemplate);
    }

    private static bool TryNormalizeHost(string? value, out string host)
    {
        host = "";
        if (string.IsNullOrWhiteSpace(value))
            return false;

        var candidate = value.Contains("://", StringComparison.Ordinal) ? value : $"https://{value}";
        if (!Uri.TryCreate(candidate, UriKind.Absolute, out var uri) || string.IsNullOrWhiteSpace(uri.Host))
            return false;

        host = uri.Host;
        return true;
    }

    private record Replacer(string[] SourceHosts, string? ReplacementHost, Regex? PathRegex, string? PathReplacement,
                            Uri? ResolverUrl, string? ResolvedUrlTemplate)
    {
        public bool Matches(Uri uri) =>
            (PathRegex == null || PathRegex.IsMatch(uri.AbsolutePath)) &&
            SourceHosts.Any(sourceHost =>
                sourceHost.StartsWith("*.", StringComparison.Ordinal)
                    ? uri.Host.EndsWith(sourceHost[1..], StringComparison.OrdinalIgnoreCase)
                    : uri.Host.Equals(sourceHost, StringComparison.OrdinalIgnoreCase));

        public string RewritePath(string path) =>
            PathRegex != null && PathReplacement != null
                ? PathRegex.Replace(path, PathReplacement)
                : path;
    }

    private record ResolverResponse(ResolverResponseData Data);
    private record ResolverResponseData(string Key);
}