using System.Net;

using PluralKit.Bot;

using Xunit;

namespace PluralKit.Tests;

public class SocialLinkRewriterTests
{
    private readonly SocialLinkRewriter _rewriter = new(new BotConfig
    {
        SocialLinkReplacers = new Dictionary<string, SocialLinkReplacerConfig>
        {
            ["X"] = new()
            {
                SourceHosts = "x.com,*.x.com,twitter.com,*.twitter.com",
                ReplacementHost = "https://fxtwitter.com",
            },
            ["Posts"] = new()
            {
                SourceHosts = "social.example,*.social.example",
                ReplacementHost = "embed.example",
                PathPattern = "^/post/",
            },
            ["TwitchClips"] = new()
            {
                SourceHosts = "clips.twitch.tv",
                ReplacementHost = "fxtwitch.seria.moe",
                PathPattern = "^/([^/]+)",
                PathReplacement = "/clip/$1",
            },
        },
    }, new HttpClient());

    [Fact]
    public void RewritesConfiguredHostsAndPreservesUrlParts()
    {
        var links = _rewriter.RewriteLinks(
            "See https://mobile.twitter.com/user/status/123?ref=abc#media and https://x.com/user/status/456.");

        Assert.Equal(new[]
        {
            "https://fxtwitter.com/user/status/123?ref=abc#media",
            "https://fxtwitter.com/user/status/456",
        }, links);
    }

    [Fact]
    public void IgnoresUnconfiguredAndLookalikeHosts()
    {
        var links = _rewriter.RewriteLinks(
            "https://example.com/post https://notx.com/post https://x.com.example.org/post");

        Assert.Empty(links);
    }

    [Fact]
    public void RemovesDuplicateRewrittenLinks()
    {
        var links = _rewriter.RewriteLinks("https://x.com/user/status/123 https://x.com/user/status/123");

        Assert.Single(links);
    }

    [Fact]
    public void AppliesConfiguredPathFilter()
    {
        var links = _rewriter.RewriteLinks(
            "https://social.example/watch/123 https://www.social.example/post/456");

        Assert.Equal(new[] { "https://embed.example/post/456" }, links);
    }

    [Fact]
    public void AppliesConfiguredPathReplacement()
    {
        var links = _rewriter.RewriteLinks("https://clips.twitch.tv/ExampleClip?t=10");

        Assert.Equal(new[] { "https://fxtwitch.seria.moe/clip/ExampleClip?t=10" }, links);
    }

    [Fact]
    public void RewritesLinksInsideOriginalContent()
    {
        var content = _rewriter.RewriteContent("Look at https://x.com/user/status/123! Great post.");

        Assert.Equal("Look at https://fxtwitter.com/user/status/123! Great post.", content);
    }

    [Fact]
    public void ReturnsNullWhenContentHasNoConfiguredLinks()
    {
        Assert.Null(_rewriter.RewriteContent("https://example.com/post"));
    }

    [Fact]
    public async Task ResolvesApiBackedLinks()
    {
        var handler = new StubHttpMessageHandler(request =>
        {
            Assert.Equal("https://resolver.example/?q=https%3A%2F%2Fwww.snapchat.com%2Fspotlight%2Fabc", request.RequestUri?.AbsoluteUri);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"data":{"key":"resolved-key"}}"""),
            };
        });
        var rewriter = new SocialLinkRewriter(new BotConfig
        {
            SocialLinkReplacers = new Dictionary<string, SocialLinkReplacerConfig>
            {
                ["Snapchat"] = new()
                {
                    SourceHosts = "snapchat.com,*.snapchat.com",
                    PathPattern = "^/spotlight/",
                    ResolverUrl = "https://resolver.example",
                    ResolvedUrlTemplate = "https://embed.example/embed/{key}",
                },
            },
        }, new HttpClient(handler));

        var content = await rewriter.RewriteContentAsync("See https://www.snapchat.com/spotlight/abc!");

        Assert.Equal("See https://embed.example/embed/resolved-key!", content);
    }

    [Fact]
    public async Task ResolvesNewgroundsPortalLinks()
    {
        var handler = new StubHttpMessageHandler(request =>
        {
            Assert.Equal(
                "https://embedez.com/api/v1/providers/combined?q=https%3A%2F%2Fwww.newgrounds.com%2Fportal%2Fview%2F805579",
                request.RequestUri?.AbsoluteUri);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"data":{"key":"newgrounds-key"}}"""),
            };
        });
        var rewriter = new SocialLinkRewriter(new BotConfig
        {
            SocialLinkReplacers = new Dictionary<string, SocialLinkReplacerConfig>
            {
                ["NewgroundsPortal"] = new()
                {
                    SourceHosts = "newgrounds.com,*.newgrounds.com",
                    PathPattern = "^/portal/view/[^/]+",
                    ResolverUrl = "https://embedez.com/api/v1/providers/combined",
                    ResolvedUrlTemplate = "https://embedez.com/embed/{key}",
                },
            },
        }, new HttpClient(handler));

        var content = await rewriter.RewriteContentAsync("https://www.newgrounds.com/portal/view/805579");

        Assert.Equal("https://embedez.com/embed/newgrounds-key", content);
    }

    [Fact]
    public async Task ResolvesOnlyFansAndFanslyLinks()
    {
        var handler = new StubHttpMessageHandler(request =>
        {
            Assert.StartsWith("https://embedez.com/api/v1/providers/combined?q=", request.RequestUri?.AbsoluteUri);
            return request.RequestUri?.AbsoluteUri switch
            {
                "https://embedez.com/api/v1/providers/combined?q=https%3A%2F%2Fonlyfans.com%2Falice%2Fposts%2F123456" =>
                    new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent("""{"data":{"key":"onlyfans-key"}}"""),
                    },
                "https://embedez.com/api/v1/providers/combined?q=https%3A%2F%2Fwww.fansly.com%2Fpost%2F987654" =>
                    new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent("""{"data":{"key":"fansly-key"}}"""),
                    },
                _ => throw new Xunit.Sdk.XunitException($"Unexpected resolver URL: {request.RequestUri?.AbsoluteUri}"),
            };
        });
        var rewriter = new SocialLinkRewriter(new BotConfig
        {
            SocialLinkReplacers = new Dictionary<string, SocialLinkReplacerConfig>
            {
                ["OnlyFans"] = new()
                {
                    SourceHosts = "onlyfans.com,*.onlyfans.com",
                    ResolverUrl = "https://embedez.com/api/v1/providers/combined",
                    ResolvedUrlTemplate = "https://embedez.com/embed/{key}",
                },
                ["Fansly"] = new()
                {
                    SourceHosts = "fansly.com,*.fansly.com",
                    ResolverUrl = "https://embedez.com/api/v1/providers/combined",
                    ResolvedUrlTemplate = "https://embedez.com/embed/{key}",
                },
            },
        }, new HttpClient(handler));

        var content = await rewriter.RewriteContentAsync(
            "https://onlyfans.com/alice/posts/123456 and https://www.fansly.com/post/987654");

        Assert.Equal("https://embedez.com/embed/onlyfans-key and https://embedez.com/embed/fansly-key", content);
    }

    private sealed class StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responseFactory):
        HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
                                                               CancellationToken cancellationToken) =>
            Task.FromResult(responseFactory(request));
    }
}