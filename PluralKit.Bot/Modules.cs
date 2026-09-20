using App.Metrics;

using Autofac;

using Myriad.Cache;
using Myriad.Gateway;
using Myriad.Rest;

using NodaTime;

using PluralKit.Core;

using Sentry;

using Serilog;

using IClock = NodaTime.IClock;

namespace PluralKit.Bot;

public class BotModule: Module
{
    protected override void Load(ContainerBuilder builder)
    {
        // Clients
        builder.Register(c =>
        {
            var botConfig = c.Resolve<BotConfig>();
            return new GatewaySettings
            {
                Token = botConfig.Token,
                MaxShardConcurrency = botConfig.MaxShardConcurrency,
                GatewayQueueUrl = botConfig.GatewayQueueUrl,
                UseRedisRatelimiter = botConfig.UseRedisRatelimiter,
                Intents = GatewayIntent.Guilds |
                          GatewayIntent.DirectMessages |
                          GatewayIntent.DirectMessageReactions |
                          GatewayIntent.GuildEmojis |
                          GatewayIntent.GuildMessages |
                          GatewayIntent.GuildWebhooks |
                          GatewayIntent.GuildMessageReactions |
                          GatewayIntent.MessageContent
            };
        }).AsSelf().SingleInstance();
        builder.RegisterType<Cluster>().AsSelf().SingleInstance();
        builder.Register<IDiscordCache>(c =>
        {
            var botConfig = c.Resolve<BotConfig>();

            if (botConfig.HttpCacheUrl != null)
            {
                var cache = new HttpDiscordCache(
                    c.Resolve<ILogger>(),
                    c.Resolve<HttpClient>(),
                    botConfig.HttpCacheUrl,
                    botConfig.EventAwaiterTarget,
                    botConfig.Cluster?.TotalShards ?? 1,
                    botConfig.ClientId,
                    botConfig.HttpUseInnerCache
                );

                var metrics = c.Resolve<IMetrics>();

                cache.OnDebug += (_, ev) =>
                {
                    var (remote, key) = ev;
                    metrics.Measure.Meter.Mark(BotMetrics.CacheDebug, new MetricTags(
                        new[] { "remote", "key" },
                        new[] { remote.ToString(), key }
                    ));
                };

                return cache;
            }

            return new MemoryDiscordCache(botConfig.ClientId);
        }).AsSelf().SingleInstance();
        builder.RegisterType<PrivateChannelService>().AsSelf().SingleInstance();

        builder.Register(c =>
        {
            var client = new DiscordApiClient(
                c.Resolve<BotConfig>().Token,
                c.Resolve<ILogger>(),
                c.Resolve<BotConfig>().DiscordBaseUrl
            );

            var metrics = c.Resolve<IMetrics>();

            client.OnResponseEvent += (_, ev) =>
            {
                var (endpoint, statusCode, ticks) = ev;
                var timer = metrics.Provider.Timer.Instance(BotMetrics.DiscordApiRequests, new MetricTags(
                    new[] { "endpoint", "status_code" },
                    new[] { endpoint, statusCode.ToString() }
                ));
                timer.Record(ticks / 10, TimeUnit.Microseconds);
            };

            return client;
        }).AsSelf().SingleInstance();

        // Application commands
        builder.RegisterType<ApplicationCommandTree>().AsSelf();
        builder.RegisterType<ApplicationCommandSay>().AsSelf();
        builder.RegisterType<ApplicationCommandTts>().AsSelf();
        builder.RegisterType<ApplicationCommandTtsReply>().AsSelf();
        builder.RegisterType<ApplicationCommandProxiedMessage>().AsSelf();

        // Bot core
        builder.RegisterType<Bot>().AsSelf().SingleInstance();
        builder.RegisterType<MessageCreated>().As<IEventHandler<MessageCreateEvent>>();
        builder.RegisterType<MessageDeleted>().As<IEventHandler<MessageDeleteEvent>>()
            .As<IEventHandler<MessageDeleteBulkEvent>>();
        builder.RegisterType<ReactionAdded>().As<IEventHandler<MessageReactionAddEvent>>();
        builder.RegisterType<InteractionCreated>().As<IEventHandler<InteractionCreateEvent>>();

        // Bot services
        builder.RegisterType<WebhookExecutorService>().AsSelf().SingleInstance();
        builder.RegisterType<WebhookCacheService>().AsSelf().SingleInstance();
        builder.RegisterType<ShardInfoService>().AsSelf().SingleInstance();
        builder.RegisterType<CpuStatService>().AsSelf().SingleInstance();
        builder.RegisterType<PeriodicStatCollector>().AsSelf().SingleInstance();
        builder.RegisterType<LastMessageCacheService>().AsSelf().SingleInstance();
        builder.RegisterType<ErrorMessageService>().AsSelf().SingleInstance();
        builder.RegisterType<CommandMessageService>().AsSelf().SingleInstance();
        builder.RegisterType<InteractionDispatchService>().AsSelf().SingleInstance();
        builder.RegisterType<TtsVoiceService>().AsSelf().SingleInstance();
        builder.RegisterType<HttpListenerService>().AsSelf().SingleInstance();
        builder.RegisterType<RuntimeConfigService>().AsSelf().SingleInstance();

        // Sentry stuff
        builder.Register(_ => new Scope(null)).AsSelf().InstancePerLifetimeScope();
        builder.RegisterType<SentryEnricher>()
            .As<ISentryEnricher<MessageCreateEvent>>()
            .As<ISentryEnricher<MessageDeleteEvent>>()
            .As<ISentryEnricher<MessageUpdateEvent>>()
            .As<ISentryEnricher<MessageDeleteBulkEvent>>()
            .As<ISentryEnricher<MessageReactionAddEvent>>()
            .SingleInstance();

        // Utils
        builder.Register(c => new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(5),
            DefaultRequestHeaders = { { "User-Agent", DiscordApiClient.UserAgent } }
        }).AsSelf().SingleInstance();
        builder.RegisterInstance(SystemClock.Instance).As<IClock>();
        builder.RegisterType<SerilogGatewayEnricherFactory>().AsSelf().SingleInstance();
    }
}