using App.Metrics;

using Autofac;

using Myriad.Cache;
using Myriad.Gateway;
using Myriad.Rest;
using Myriad.Rest.Types;
using Myriad.Rest.Types.Requests;
using Myriad.Types;
using Myriad.Utils;

using PluralKit.Core;

namespace PluralKit.Bot;

public class InteractionContext
{
    private readonly ILifetimeScope _provider;
    private readonly IMetrics _metrics;

    public InteractionContext(ILifetimeScope provider, InteractionCreateEvent evt, PKSystem system, SystemConfig config)
    {
        Event = evt;
        System = system;
        Config = config;
        Cache = provider.Resolve<IDiscordCache>();
        Rest = provider.Resolve<DiscordApiClient>();
        Repository = provider.Resolve<ModelRepository>();
        _metrics = provider.Resolve<IMetrics>();
        _provider = provider;
    }

    internal readonly IDiscordCache Cache;
    internal readonly DiscordApiClient Rest;
    internal readonly ModelRepository Repository;
    public readonly PKSystem System;
    public readonly SystemConfig Config;

    // Tracks whether we've already sent an initial interaction response (or deferred).
    // Once true, further user-facing messages must edit the original response instead
    // of creating a new one (Discord returns 404 Unknown interaction otherwise).
    private bool _hasResponded;

    public InteractionCreateEvent Event { get; }

    public ulong GuildId => Event.GuildId;
    public ulong ChannelId => Event.ChannelId;
    public ulong? MessageId => Event.Message?.Id;
    public GuildMember? Member => Event.Member;
    public User User => Event.Member?.User ?? Event.User;
    public string Token => Event.Token;
    public string? CustomId => Event.Data?.CustomId;
    public IComponentContext Services => _provider;

    public async Task Execute<T>(ApplicationCommand? command, Func<T, Task> handler)
    {
        try
        {
            using (_metrics.Measure.Timer.Time(BotMetrics.ApplicationCommandTime, new MetricTags("Application command", command?.Name ?? "null")))
                await handler(_provider.Resolve<T>());

            _metrics.Measure.Meter.Mark(BotMetrics.ApplicationCommandsRun);
        }
        catch (PKError e)
        {
            await Reply($"{Emojis.Error} {e.Message}");
        }
        catch (TimeoutException)
        {
            // Got a complaint the old error was a bit too patronizing. Hopefully this is better?
            await Reply($"{Emojis.Error} Operation timed out, sorry. Try again, perhaps?");
        }
    }

    public async Task ExecuteAutocomplete<T>(ApplicationCommand? command, Func<T, Task> handler)
    {
        try
        {
            using (_metrics.Measure.Timer.Time(BotMetrics.ApplicationCommandTime, new MetricTags("Application command", command?.Name ?? "null")))
                await handler(_provider.Resolve<T>());
        }
        catch
        {
            // Autocomplete endpoints must respond with type 8 payloads; fallback to empty results.
            await RespondAutocomplete();
        }
    }

    public async Task Reply(string content = null, Embed[]? embeds = null)
    {
        if (_hasResponded)
        {
            await EditReply(content, embeds);
            return;
        }

        await Respond(InteractionResponse.ResponseType.ChannelMessageWithSource,
            new InteractionApplicationCommandCallbackData
            {
                Content = content,
                Embeds = embeds,
                Flags = Message.MessageFlags.Ephemeral
            });
    }

    public async Task EditReply(string? content = null, Embed[]? embeds = null)
    {
        var applicationId = _provider.Resolve<BotConfig>().ClientId;
        await Rest.EditOriginalInteractionResponse(applicationId, Event.Token,
            new WebhookMessageEditRequest
            {
                Content = content,
                Embeds = embeds ?? Array.Empty<Embed>(),
            });
    }

    public async Task Reply(MessageComponent[] components = null, AllowedMentions? mentions = null)
    {
        await Respond(InteractionResponse.ResponseType.ChannelMessageWithSource,
            new InteractionApplicationCommandCallbackData
            {
                Components = components,
                Flags = Message.MessageFlags.Ephemeral | Message.MessageFlags.IsComponentsV2,
                AllowedMentions = mentions ?? new AllowedMentions()
            });
    }

    public async Task Defer()
    {
        await Respond(InteractionResponse.ResponseType.DeferredChannelMessageWithSource,
            new InteractionApplicationCommandCallbackData
            {
                Components = Array.Empty<MessageComponent>(),
                Flags = Message.MessageFlags.Ephemeral,
            });
    }

    public async Task Ignore()
    {
        await Respond(InteractionResponse.ResponseType.DeferredUpdateMessage,
            new InteractionApplicationCommandCallbackData
            {
                Components = Event.Message?.Components ?? Array.Empty<MessageComponent>()
            });
    }

    public async Task Acknowledge()
    {
        await Respond(InteractionResponse.ResponseType.UpdateMessage,
            new InteractionApplicationCommandCallbackData { Components = Array.Empty<MessageComponent>() });
    }

    public async Task Respond(InteractionResponse.ResponseType type,
                              InteractionApplicationCommandCallbackData? data)
    {
        await Rest.CreateInteractionResponse(Event.Id, Event.Token,
            new InteractionResponse { Type = type, Data = data });

        // Autocomplete results are not a "real" response and can be sent repeatedly.
        if (type != InteractionResponse.ResponseType.ApplicationCommandAutocompleteResult)
            _hasResponded = true;
    }

    public async Task RespondAutocomplete(params ApplicationCommandOption.Choice[] choices)
    {
        await Respond(InteractionResponse.ResponseType.ApplicationCommandAutocompleteResult,
            new InteractionApplicationCommandCallbackData
            {
                Choices = choices
            });
    }

    public async Task DeleteReply()
    {
        var applicationId = _provider.Resolve<BotConfig>().ClientId;
        await Rest.DeleteOriginalInteractionResponse(applicationId, Event.Token);
    }
}