using Autofac;

using Myriad.Cache;
using Myriad.Extensions;
using Myriad.Rest;
using Myriad.Types;

using PluralKit.Core;

namespace PluralKit.Bot;

public class ApplicationCommandProxiedMessage
{
    private readonly DiscordApiClient _rest;
    private readonly IDiscordCache _cache;
    private readonly ModelRepository _repo;

    public ApplicationCommandProxiedMessage(DiscordApiClient rest, IDiscordCache cache, ModelRepository repo)
    {
        _rest = rest;
        _cache = cache;
        _repo = repo;
    }

    public async Task DeleteMessage(InteractionContext ctx)
    {
        var messageId = ctx.Event.Data!.TargetId!.Value;

        // check for command messages
        var cmessage = await ctx.Services.Resolve<CommandMessageService>().GetCommandMessage(messageId);
        if (cmessage != null)
        {
            if (cmessage.AuthorId != ctx.User.Id)
                throw new PKError("You can only delete command messages queried by this account.");

            var isDM = (await _repo.GetDmChannel(ctx.User!.Id)) == cmessage.ChannelId;
            await DeleteMessageInner(ctx, cmessage.GuildId, cmessage.ChannelId, messageId, isDM);
            return;
        }

        // and do the same for proxied messages
        var message = await ctx.Repository.GetFullMessage(messageId);
        if (message != null)
        {
            // if user has has a system and their system sent the message, or if user sent the message, do not error
            if (!((ctx.System != null && message.System?.Id == ctx.System.Id) || message.Message.Sender == ctx.User.Id))
                throw new PKError("You can only delete your own messages.");

            await DeleteMessageInner(ctx, message.Message.Guild ?? 0, message.Message.Channel, message.Message.Mid, false);
            return;
        }

        // otherwise, we don't know about this message at all!
        throw Errors.MessageNotFound(messageId);
    }

    internal async Task DeleteMessageInner(InteractionContext ctx, ulong guildId, ulong channelId, ulong messageId, bool isDM = false)
    {
        if (!((await _cache.BotPermissionsIn(guildId, channelId)).HasFlag(PermissionSet.ManageMessages) || isDM))
            throw new PKError("CopyCat does not have the *Manage Messages* permission in this channel, and thus cannot delete the message."
                + " Please contact a server administrator to remedy this.");

        await ctx.Rest.DeleteMessage(channelId, messageId);
        await ctx.Reply($"{Emojis.Success} Message deleted.");
    }
}