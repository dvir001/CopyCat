using System.Text.RegularExpressions;

namespace PluralKit.Bot;

public class SocialLinkRewriter
{
    private static readonly Regex UrlRegex = new(@"https?://[^\s<>()]+",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private readonly Replacer[] _replacers;

    public SocialLinkRewriter(BotConfig config)
    {
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

    private string? Rewrite(string link)
    {
        if (!Uri.TryCreate(link, UriKind.Absolute, out var uri))
            return null;

        var replacer = _replacers.FirstOrDefault(candidate => candidate.Matches(uri));
        if (replacer == null)
            return null;

        var builder = new UriBuilder(uri)
        {
            Host = replacer.ReplacementHost,
            Path = replacer.RewritePath(uri.AbsolutePath),
            Port = -1,
        };
        return builder.Uri.AbsoluteUri;
    }

    private static Replacer? CreateReplacer(SocialLinkReplacerConfig config)
    {
        var sourceHosts = config.SourceHosts?
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (sourceHosts == null || sourceHosts.Length == 0 ||
            !TryNormalizeHost(config.ReplacementHost, out var replacementHost))
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

        return new Replacer(sourceHosts, replacementHost, pathRegex, config.PathReplacement);
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

    private record Replacer(string[] SourceHosts, string ReplacementHost, Regex? PathRegex, string? PathReplacement)
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
}