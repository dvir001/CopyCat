using Myriad.Cache;
using Myriad.Extensions;
using Myriad.Gateway;
using Myriad.Rest;
using Myriad.Rest.Exceptions;
using Myriad.Types;

namespace PluralKit.Bot;

public class ReactionAdded: IEventHandler<MessageReactionAddEvent>
{
    private readonly BotConfig _config;
    private readonly IDiscordCache _cache;
    private readonly CommandMessageService _commandMessageService;
    private readonly DiscordApiClient _rest;

    public ReactionAdded(CommandMessageService commandMessageService, IDiscordCache cache,
                         BotConfig config, DiscordApiClient rest)
    {
        _commandMessageService = commandMessageService;
        _cache = cache;
        _config = config;
        _rest = rest;
    }

    public async Task Handle(int shardId, MessageReactionAddEvent evt)
    {
        // ignore any reactions added by *us*
        if (evt.UserId == _config.ClientId)
            return;

        // Ignore reactions from bots (we can't DM them anyway)
        if (evt.GuildId != null && (evt.Member?.User?.Bot ?? false)) return;

        // ❌ deletes a message sent through CopyCat (stored as a command message)
        if (evt.Emoji.Name != "\u274c") return;

        var channel = await _cache.GetChannel(evt.GuildId ?? 0, evt.ChannelId);

        // in DMs, allow deleting any CopyCat message
        if (channel.GuildId == null)
        {
            await HandleCommandDeleteReaction(evt, null, true);
            return;
        }

        var cmessage = await _commandMessageService.GetCommandMessage(evt.MessageId);
        if (cmessage != null)
            await HandleCommandDeleteReaction(evt, cmessage.AuthorId, false);
    }

    private async ValueTask HandleCommandDeleteReaction(MessageReactionAddEvent evt, ulong? authorId, bool isDM)
    {
        // Can only delete your own message
        // (except in DMs, where msg will be null)
        if (authorId != null && authorId != evt.UserId)
            return;

        if (!((await _cache.BotPermissionsIn(evt.GuildId ?? 0, evt.ChannelId)).HasFlag(PermissionSet.ManageMessages) || isDM))
            return;

        try
        {
            await _rest.DeleteMessage(evt.ChannelId, evt.MessageId);
        }
        catch (NotFoundException)
        {
            // Message was deleted by something/someone else before we got to it
        }
        catch (ForbiddenException)
        {
            // user reacted with :x: to their own message
        }
    }
}
