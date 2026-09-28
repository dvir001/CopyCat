using App.Metrics;

using Autofac;

using Myriad.Cache;
using Myriad.Extensions;
using Myriad.Gateway;
using Myriad.Rest;
using Myriad.Types;

using PluralKit.Core;

namespace PluralKit.Bot;

public class MessageCreated: IEventHandler<MessageCreateEvent>
{
    private readonly Bot _bot;
    private readonly IDiscordCache _cache;
    private readonly BotConfig _config;
    private readonly LastMessageCacheService _lastMessageCache;
    private readonly IMetrics _metrics;
    private readonly ModelRepository _repo;
    private readonly ILifetimeScope _services;
    private readonly PrivateChannelService _dmCache;
    private readonly DiscordApiClient _rest;
    private readonly SocialLinkRewriter _socialLinkRewriter;
    private readonly WebhookExecutorService _webhookExecutor;

    public MessageCreated(LastMessageCacheService lastMessageCache,
                          IMetrics metrics,
                          ILifetimeScope services, BotConfig config,
                          ModelRepository repo, IDiscordCache cache,
                          Bot bot, PrivateChannelService dmCache,
                          WebhookExecutorService webhookExecutor, DiscordApiClient rest,
                          SocialLinkRewriter socialLinkRewriter)
    {
        _lastMessageCache = lastMessageCache;
        _metrics = metrics;
        _services = services;
        _config = config;
        _repo = repo;
        _cache = cache;
        _bot = bot;
        _dmCache = dmCache;
        _webhookExecutor = webhookExecutor;
        _rest = rest;
        _socialLinkRewriter = socialLinkRewriter;
    }

    public (ulong?, ulong?) ErrorChannelFor(MessageCreateEvent evt, ulong userId) => (evt.GuildId, evt.ChannelId);
    private bool IsDuplicateMessage(Message msg) =>
        // We consider a message duplicate if it has the same ID as the previous message that hit the gateway
        // use only the local cache here
        // http gateway sets last message before forwarding the message here, so this will always return true
        _lastMessageCache._GetLastMessage(msg.ChannelId)?.Current.Id == msg.Id;

    public async Task Handle(int shardId, MessageCreateEvent evt)
    {
        if (evt.Author.Id == _config.ClientId) return;
        if (evt.Type != Message.MessageType.Default && evt.Type != Message.MessageType.Reply) return;
        if (IsDuplicateMessage(evt)) return;

        var botPermissions = await _cache.BotPermissionsIn(evt.GuildId ?? 0, evt.ChannelId);
        if (!botPermissions.HasFlag(PermissionSet.SendMessages)) return;

        // spawn off saving the private channel into another thread
        // it is not a fatal error if this fails, and it shouldn't block message processing
        _ = _dmCache.TrySavePrivateChannel(evt);

        var guild = evt.GuildId != null ? await _cache.GetGuild(evt.GuildId.Value) : null;
        var channel = await _cache.GetChannel(evt.GuildId ?? 0, evt.ChannelId);
        var rootChannel = await _cache.GetRootChannel(evt.GuildId ?? 0, evt.ChannelId);

        // Log metrics and message info
        _metrics.Measure.Meter.Mark(BotMetrics.MessagesReceived);
        _lastMessageCache.AddMessage(evt);

        // CopyCat only reacts to regular user messages (reply pings for /s-sent webhook messages)
        if (evt.Author.Bot || evt.WebhookId != null || evt.Author.System == true)
            return;

        try
        {
            if (evt.GuildId != null)
            {
                if (await TryEmbedSocialLinks(evt, guild, channel, rootChannel, botPermissions))
                    return;
                await TryHandleWebhookReplyPing(evt, guild, channel, rootChannel, botPermissions);
            }
        }
        catch (Exception exc)
        {
            await _bot.HandleError(this, evt, _services, exc, true);
        }
    }

    private async Task<bool> TryEmbedSocialLinks(MessageCreateEvent evt, Guild guild, Channel channel,
                                                 Channel rootChannel, PermissionSet botPermissions)
    {
        if (!botPermissions.HasFlag(PermissionSet.EmbedLinks) ||
            !botPermissions.HasFlag(PermissionSet.ManageWebhooks) ||
            !botPermissions.HasFlag(PermissionSet.ManageMessages) ||
            evt.Attachments.Length > 0 && !botPermissions.HasFlag(PermissionSet.AttachFiles))
            return false;

        var content = await _socialLinkRewriter.RewriteContentAsync(evt.Content);
        if (content == null || content.Length > 2000)
            return false;

        var avatarUrl = !string.IsNullOrWhiteSpace(evt.Member?.Avatar)
            ? $"https://cdn.discordapp.com/guilds/{guild.Id}/users/{evt.Author.Id}/avatars/{evt.Member.Avatar}.png?size=4096"
            : !string.IsNullOrWhiteSpace(evt.Author.Avatar)
                ? $"https://cdn.discordapp.com/avatars/{evt.Author.Id}/{evt.Author.Avatar}.png?size=4096"
                : null;

        var sent = await _webhookExecutor.ExecuteWebhook(new ProxyRequest
        {
            GuildId = guild.Id,
            ChannelId = rootChannel.Id,
            ThreadId = channel.IsThread() ? channel.Id : null,
            MessageId = evt.Id,
            Name = evt.Member?.Nick ?? evt.Author.GlobalName ?? evt.Author.Username,
            AvatarUrl = avatarUrl,
            Content = content,
            Attachments = evt.Attachments,
            FileSizeLimit = guild.FileSizeLimit(),
            Embeds = Array.Empty<Embed>(),
            Stickers = evt.StickerItems ?? Array.Empty<Sticker>(),
            AllowEveryone = false,
            MessageReference = null,
            Components = ApplicationCommandProxiedMessage.MessageControls(),
            Flags = 0,
            Tts = false,
            Poll = null,
        });

        await _repo.AddCommandMessage(new PluralKit.Core.CommandMessage
        {
            Mid = sent.Id,
            Guild = guild.Id,
            Channel = sent.ChannelId,
            Sender = evt.Author.Id,
            OriginalMid = evt.Id,
        });
        await _rest.DeleteMessage(evt.ChannelId, evt.Id);
        return true;
    }

    private async Task TryHandleWebhookReplyPing(MessageCreateEvent evt, Guild guild, Channel channel,
                                                 Channel rootChannel, PermissionSet botPermissions)
    {
        if (evt.Type != Message.MessageType.Reply ||
            evt.MessageReference?.ChannelId != evt.ChannelId ||
            evt.MessageReference.MessageId is not { } repliedToId ||
            !botPermissions.HasFlag(PermissionSet.ManageWebhooks))
            return;

        var replyPingUserId = await _repo.GetMessageSender(repliedToId);
        if (replyPingUserId == null || replyPingUserId == evt.Author.Id || evt.MentionsUser(replyPingUserId.Value))
            return;

        var avatarUrl = !string.IsNullOrWhiteSpace(evt.Member?.Avatar)
            ? $"https://cdn.discordapp.com/guilds/{guild.Id}/users/{evt.Author.Id}/avatars/{evt.Member.Avatar}.png?size=4096"
            : !string.IsNullOrWhiteSpace(evt.Author.Avatar)
                ? $"https://cdn.discordapp.com/avatars/{evt.Author.Id}/{evt.Author.Avatar}.png?size=4096"
                : null;

        await _webhookExecutor.ExecuteWebhook(new ProxyRequest
        {
            GuildId = guild.Id,
            ChannelId = rootChannel.Id,
            ThreadId = channel.IsThread() ? channel.Id : null,
            MessageId = evt.Id,
            Name = evt.Member?.Nick ?? evt.Author.GlobalName ?? evt.Author.Username,
            AvatarUrl = avatarUrl,
            Content = $"-# <@{replyPingUserId}>",
            Attachments = Array.Empty<Message.Attachment>(),
            FileSizeLimit = guild.FileSizeLimit(),
            Embeds = Array.Empty<Embed>(),
            Stickers = Array.Empty<Sticker>(),
            AllowEveryone = false,
            Flags = 0,
            Tts = false,
            Poll = null,
        });
    }
}