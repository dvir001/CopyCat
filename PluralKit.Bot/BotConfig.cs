namespace PluralKit.Bot;

public class BotConfig
{
    public string Token { get; set; }
    public ulong ClientId { get; set; }

    public Dictionary<string, SocialLinkReplacerConfig> SocialLinkReplacers { get; set; } = new();

    public int? MaxShardConcurrency { get; set; }

    public ClusterSettings? Cluster { get; set; }

    public string? GatewayQueueUrl { get; set; }
    public bool UseRedisRatelimiter { get; set; } = false;

    public string? HttpCacheUrl { get; set; }
    public bool HttpUseInnerCache { get; set; } = false;

    public string? HttpListenerAddr { get; set; }
    public bool DisableGateway { get; set; } = false;
    public string? EventAwaiterTarget { get; set; }

    public string? DiscordBaseUrl { get; set; }
    public string? AvatarServiceUrl { get; set; }

    public bool DisableErrorReporting { get; set; } = false;

    public bool IsBetaBot { get; set; } = false!;
    public string BetaBotAPIUrl { get; set; }

    public record ClusterSettings
    {
        // this is zero-indexed
        public string NodeName { get; set; }
        public int TotalShards { get; set; }
        public int TotalNodes { get; set; }

        // Node name eg. "pluralkit-3", want to extract the 3. blame k8s :p
        public int NodeIndex => int.Parse(NodeName.Split("-").Last());
    }
}

public class SocialLinkReplacerConfig
{
    public string SourceHosts { get; set; }
    public string ReplacementHost { get; set; }
    public string? PathPattern { get; set; }
    public string? PathReplacement { get; set; }
    public string? ResolverUrl { get; set; }
    public string? ResolvedUrlTemplate { get; set; }
}