using System.Text.Json;
using System.Text.RegularExpressions;

using Myriad.Cache;
using Myriad.Extensions;
using Myriad.Rest.Exceptions;
using Myriad.Types;

using PluralKit.Core;

namespace PluralKit.Bot;

public class ApplicationCommandTts
{
    private static readonly Regex UrlRegex = new(@"(http|https)(:\/\/)?(www\.)?([-a-zA-Z0-9@:%._\+~#=]{1,256})?\.?([a-zA-Z0-9()]{1,6})?\b([-a-zA-Z0-9()@:%_\+.~#?&//=]*)$",
        RegexOptions.CultureInvariant);
    internal static readonly VoiceDefinition[] VoiceCatalog =
    {
        new("ar_JO-kareem-medium",                "Arabic — Kareem"),
        new("ca_ES-upc_ona-medium",               "Catalan — Ona"),
        new("cabal",                               "CABAL"),
        new("cs_CZ-jirka-medium",                 "Czech — Jirka"),
        new("cy_GB-gwryw_gogleddol-medium",       "Welsh — Gwryw Gogleddol"),
        new("da_DK-talesyntese-medium",           "Danish — Talesyntese"),
        new("de_DE-thorsten-medium",              "German — Thorsten"),
        new("el_GR-rapunzelina-low",              "Greek — Rapunzelina"),
        new("en_GB-alan-medium",                  "Alan (UK, male)"),
        new("en_GB-alba-medium",                  "Alba (UK, female, Scottish)"),
        new("en_GB-aru-medium",                   "Aru (UK, female)"),
        new("en_GB-cori-high",                    "Cori (UK, female, high quality)"),
        new("en_GB-jenny_dioco-medium",           "Jenny (UK, female)"),
        new("en_GB-northern_english_male-medium", "Northern English (UK, male)"),
        new("en_GB-southern_english_female-low",  "Southern English (UK, female)"),
        new("en_US-alex-jones-medium",              "Alex Jones (US, male)"),
        new("en_US-amy-medium",                   "Amy (US, female)"),
        new("en_US-bryce-medium",                 "Bryce (US, male)"),
        new("en_US-danny-low",                    "Danny (US, male)"),
        new("en_US-hfc_female-medium",            "HFC Female (US)"),
        new("en_US-hfc_male-medium",              "HFC Male (US)"),
        new("en_US-joe-medium",                   "Joe (US, male)"),
        new("en_US-john-medium",                  "John (US, male)"),
        new("en_US-kathleen-low",                 "Kathleen (US, female)"),
        new("en_US-kristin-medium",               "Kristin (US, female)"),
        new("en_US-kusal-medium",                 "Kusal (US, male)"),
        new("en_US-lessac-high",                  "Lessac (US, female, high quality)"),
        new("en_US-ljspeech-high",                "LJSpeech (US, female, high quality)"),
        new("en_US-norman-medium",                "Norman (US, male)"),
        new("en_US-ryan-high",                    "Ryan (US, male, high quality)"),
        new("en_US-trump-speech-medium",          "Trump Speech (US, male)"),
        new("es_AR-daniela-high",                 "Spanish (Argentina) — Daniela"),
        new("es_ES-davefx-medium",                "Spanish (Spain) — Dave"),
        new("es_MX-ald-medium",                   "Spanish (Mexico) — Ald"),
        new("fa_IR-gyro-medium",                  "Farsi — Gyro"),
        new("fi_FI-harri-medium",                 "Finnish — Harri"),
        new("fr_FR-siwis-medium",                 "French — Siwis"),
        new("hi_IN-pratham-medium",               "Hindi — Pratham"),
        new("hu_HU-anna-medium",                  "Hungarian — Anna"),
        new("is_IS-bui-medium",                   "Icelandic — Bui"),
        new("it_IT-paola-medium",                 "Italian — Paola"),
        new("ka_GE-natia-medium",                 "Georgian — Natia"),
        new("kk_KZ-raya-x_low",                   "Kazakh — Raya"),
        new("lb_LU-marylux-medium",               "Luxembourgish — Marylux"),
        new("lv_LV-aivars-medium",                "Latvian — Aivars"),
        new("ml_IN-meera-medium",                 "Malayalam — Meera"),
        new("morshu",                              "Morshu"),
        new("ne_NP-chitwan-medium",               "Nepali — Chitwan"),
        new("nl_BE-nathalie-medium",              "Dutch (Belgium) — Nathalie"),
        new("nl_NL-ronnie-medium",                "Dutch (Netherlands) — Ronnie"),
        new("no_NO-talesyntese-medium",           "Norwegian — Talesyntese"),
        new("pl_PL-gosia-medium",                 "Polish — Gosia"),
        new("pt_BR-faber-medium",                 "Portuguese (Brazil) — Faber"),
        new("ro_RO-mihai-medium",                 "Romanian — Mihai"),
        new("ru_RU-dmitri-medium",                "Russian — Dmitri"),
        new("sk_SK-lili-medium",                  "Slovak — Lili"),
        new("sl_SI-artur-medium",                 "Slovenian — Artur"),
        new("sr_RS-serbski_institut-medium",      "Serbian — Serbski Institut"),
        new("sv_SE-nst-medium",                   "Swedish — NST"),
        new("sw_CD-lanfrica-medium",              "Swahili — Lanfrica"),
        new("tr_TR-fettah-medium",                "Turkish — Fettah"),
        new("uk_UA-lada-x_low",                   "Ukrainian — Lada"),
        new("vi_VN-vais1000-medium",              "Vietnamese — Vais1000"),
        new("zh_CN-huayan-medium",                "Chinese — Huayan"),
    };

    private readonly IDiscordCache _cache;
    private readonly WebhookExecutorService _webhookExecutor;
    private readonly TtsVoiceService _tts;

    public ApplicationCommandTts(IDiscordCache cache, WebhookExecutorService webhookExecutor, TtsVoiceService tts)
    {
        _cache = cache;
        _webhookExecutor = webhookExecutor;
        _tts = tts;
    }

    public async Task SendAsInvoker(InteractionContext ctx)
    {
        if (ctx.Event.GuildId == 0)
            throw new PKError("The /tts command only works in servers.");

        // TTS generation can take longer than Discord's 3-second interaction deadline,
        // so acknowledge (defer) immediately. Any error after this point edits the
        // deferred ephemeral reply instead of creating a new response.
        await ctx.Defer();

        var text = GetOptionalStringOption(ctx.Event.Data?.Options, "text");
        if (string.IsNullOrWhiteSpace(text))
            throw new PKError("Provide text to speak.");

        if (text.Length > 2000)
            throw new PKError("Message text cannot be longer than 2000 characters.");

        var voice = ParseVoice(GetOptionalStringOption(ctx.Event.Data?.Options, "voice"));
        var attachment = GetOptionalAttachmentOption(ctx.Event.Data, "attachment");

        var guild = await _cache.GetGuild(ctx.GuildId);
        var messageChannel = await _cache.GetOrFetchChannel(ctx.Rest, ctx.GuildId, ctx.ChannelId);
        var rootChannel = await _cache.GetRootChannel(ctx.GuildId, ctx.ChannelId);

        if (!DiscordUtils.IsValidGuildChannel(messageChannel))
            throw new PKError("CopyCat cannot send through /tts in this channel type.");

        var senderPermissions = PermissionExtensions.PermissionsFor(guild, rootChannel, ctx.User.Id, ctx.Member,
            isThread: messageChannel.Id != rootChannel.Id);
        if (!senderPermissions.HasFlag(PermissionSet.SendMessages) &&
            !(messageChannel.Id != rootChannel.Id && senderPermissions.HasFlag(PermissionSet.SendMessagesInThreads)))
            throw new PKError("You do not have permission to send messages in this channel.");

        var botPermissions = await _cache.BotPermissionsIn(ctx.GuildId, rootChannel.Id);
        if (!botPermissions.HasFlag(PermissionSet.SendMessages))
            throw new PKError("CopyCat does not have permission to send messages in this channel.");
        if (!botPermissions.HasFlag(PermissionSet.ManageWebhooks))
            throw new PKError("CopyCat does not have the Manage Webhooks permission in this channel.");

        var threadId = messageChannel.IsThread() ? messageChannel.Id : (ulong?)null;
        var proxyName = ctx.Member?.Nick ?? ctx.User.GlobalName ?? ctx.User.Username;
        var avatarUrl = BuildAvatarUrl(ctx);
        var replyTo = GetOptionalStringOption(ctx.Event.Data?.Options, "reply_to");
        var replyTarget = TryParseReplyTarget(replyTo, ctx.GuildId, messageChannel.Id);
        var reply = await TryBuildReplyEmbed(ctx, replyTarget);
        var embeds = reply == null ? Array.Empty<Embed>() : new[] { reply.Embed };

        // <@pingUserId> must appear in message content for Discord to send the notification ping.
        // Keep it as a small -# footnote so it doesn't clutter the visible message text.
        // The mention is also included in the embed description for a cleaner visual (see TryBuildReplyEmbed).
        var content = text;
        if (reply?.PingUserId is { } pingUserId)
            content = $"{content}\n-# <@{pingUserId}>";

        using var generatedClip = await _tts.GenerateClip(voice.Id, await ResolveMentionsForTts(ctx, text));

        var sent = await _webhookExecutor.ExecuteWebhook(new ProxyRequest
        {
            GuildId = ctx.GuildId,
            ChannelId = rootChannel.Id,
            ThreadId = threadId,
            MessageId = ctx.Event.Id,
            Name = proxyName,
            AvatarUrl = avatarUrl,
            Content = content,
            Attachments = attachment == null ? Array.Empty<Message.Attachment>() : new[] { attachment },
            GeneratedFiles = new[] { generatedClip.File },
            FileSizeLimit = guild.FileSizeLimit(),
            Embeds = embeds,
            Stickers = Array.Empty<Sticker>(),
            AllowEveryone = senderPermissions.HasFlag(PermissionSet.MentionEveryone),
            MessageReference = null,
            Flags = 0,
            Tts = false,
            Poll = null,
        });
        await ctx.Repository.AddCommandMessage(new PluralKit.Core.CommandMessage
        {
            Mid = sent.Id,
            Guild = ctx.GuildId,
            Channel = sent.ChannelId,
            Sender = ctx.User.Id,
            OriginalMid = ctx.Event.Id
        });

        // Remove the deferred ephemeral "thinking" indicator now that the message was sent.
        try
        {
            await ctx.DeleteReply();
        }
        catch
        {
            // Keep successful send behavior even if ephemeral cleanup fails.
        }
    }

    public async Task Autocomplete(InteractionContext ctx)
    {
        var focused = GetFocusedOption(ctx.Event.Data?.Options);
        if (focused == null || !string.Equals(focused.Name, "voice", StringComparison.OrdinalIgnoreCase))
        {
            await ctx.RespondAutocomplete();
            return;
        }

        var input = focused.Value?.ToString()?.Trim() ?? string.Empty;
        var matches = VoiceCatalog
            .Where(v => _tts.IsVoiceAvailable(v.Id))
            .Where(v => string.IsNullOrWhiteSpace(input)
                || v.Id.Contains(input, StringComparison.OrdinalIgnoreCase)
                || v.Name.Contains(input, StringComparison.OrdinalIgnoreCase))
            .Take(25)
            .Select(v => new ApplicationCommandOption.Choice(v.Name.Length > 100 ? v.Name[..100] : v.Name, v.Id))
            .ToArray();

        await ctx.RespondAutocomplete(matches);
    }

    private static VoiceDefinition ParseVoice(string value)
    {
        var match = VoiceCatalog.FirstOrDefault(v => string.Equals(v.Id, value, StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrWhiteSpace(match.Id))
            return match;

        throw new PKError("Unknown voice. Use autocomplete on the voice option.");
    }

    private static ApplicationCommandInteractionDataOption? GetFocusedOption(ApplicationCommandInteractionDataOption[]? options)
    {
        if (options == null)
            return null;

        foreach (var option in options)
        {
            if (option.Focused == true)
                return option;

            var nested = GetFocusedOption(option.Options);
            if (nested != null)
                return nested;
        }

        return null;
    }

    private static string GetOptionalStringOption(ApplicationCommandInteractionDataOption[]? options, string optionName)
    {
        var option = options?.FirstOrDefault(o => string.Equals(o.Name, optionName, StringComparison.OrdinalIgnoreCase));
        if (option == null)
            return string.Empty;

        return option.Value switch
        {
            string s => s,
            JsonElement e when e.ValueKind == JsonValueKind.String => e.GetString() ?? string.Empty,
            JsonElement e => e.ToString() ?? string.Empty,
            _ => option.Value?.ToString() ?? string.Empty
        };
    }

    private static Message.Attachment? GetOptionalAttachmentOption(ApplicationCommandInteractionData? data, string optionName)
    {
        var option = data?.Options?.FirstOrDefault(o => string.Equals(o.Name, optionName, StringComparison.OrdinalIgnoreCase));
        if (option == null)
            return null;

        var attachmentId = option.Value switch
        {
            string s when ulong.TryParse(s, out var id) => id,
            JsonElement e when e.ValueKind == JsonValueKind.String && ulong.TryParse(e.GetString(), out var id) => id,
            JsonElement e when e.ValueKind == JsonValueKind.Number && e.TryGetUInt64(out var id) => id,
            ulong id => id,
            long id when id >= 0 => (ulong)id,
            int id when id >= 0 => (ulong)id,
            _ => 0UL
        };

        if (attachmentId == 0)
            return null;

        if (data?.Resolved?.Attachments == null || !data.Resolved.Attachments.TryGetValue(attachmentId, out var attachment))
            throw new PKError("Could not resolve the selected attachment.");

        return attachment;
    }

    private static ReplyTarget? TryParseReplyTarget(string rawReplyTarget, ulong guildId, ulong defaultChannelId)
    {
        if (string.IsNullOrWhiteSpace(rawReplyTarget))
            return null;

        var input = rawReplyTarget.Trim();

        if (ulong.TryParse(input, out var messageId))
            return new ReplyTarget(guildId, defaultChannelId, messageId);

        var match = Regex.Match(input, @"^https://(?:canary\.|ptb\.)?discord\.com/channels/(\d+)/(\d+)/(\d+)$",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        if (!match.Success)
            throw new PKError("reply_to must be a message ID or a Discord message link.");

        var linkGuildId = ulong.Parse(match.Groups[1].Value);
        var linkChannelId = ulong.Parse(match.Groups[2].Value);
        var linkMessageId = ulong.Parse(match.Groups[3].Value);

        if (linkGuildId != guildId)
            throw new PKError("reply_to must point to a message in this server.");

        return new ReplyTarget(linkGuildId, linkChannelId, linkMessageId);
    }

    private static async Task<ReplyRender?> TryBuildReplyEmbed(InteractionContext ctx, ReplyTarget? replyTarget)
    {
        if (replyTarget == null)
            return null;

        var jumpLink = $"https://discord.com/channels/{replyTarget.GuildId}/{replyTarget.ChannelId}/{replyTarget.MessageId}";
        Message? referenced;
        try
        {
            referenced = await ctx.Rest.GetMessage(replyTarget.ChannelId, replyTarget.MessageId);
        }
        catch (ForbiddenException)
        {
            return new ReplyRender(BuildReplyFallbackEmbed(jumpLink), null);
        }
        catch (NotFoundException)
        {
            return new ReplyRender(BuildReplyFallbackEmbed(jumpLink), null);
        }

        if (referenced == null)
            return new ReplyRender(BuildReplyFallbackEmbed(jumpLink), null);

        var description = BuildReplyDescription(referenced, jumpLink);
        var pingUserId = await ResolvePingTarget(ctx, replyTarget.MessageId, referenced);

        var embed = new Embed
        {
            Author = new Embed.EmbedAuthor($"{DisplayName(referenced.Author)}\u2004\u21a9\ufe0f", IconUrl: BuildUserAvatarUrl(referenced.Author)),
            Description = description,
        };

        return new ReplyRender(embed, pingUserId);
    }

    // Proxied messages are sent by a webhook, so the message author is the webhook rather than the
    // real person. Resolve the original sender from PluralKit's message store so the reply ping
    // notifies an actual user; fall back to the message author for normal (non-proxied) messages.
    private static async Task<ulong?> ResolvePingTarget(InteractionContext ctx, ulong messageId, Message referenced)
    {
        var pkMessage = await ctx.Repository.GetMessage(messageId);
        if (pkMessage != null)
            return pkMessage.Sender;

        if (referenced.WebhookId == null && referenced.Author?.Bot != true)
            return referenced.Author?.Id;

        // Best-effort for foreign webhook/proxy messages (not in our database): match the shown
        // display name against server members and only ping when exactly one member matches, to
        // avoid pinging the wrong person.
        var displayName = referenced.Author?.Username;
        if (referenced.WebhookId != null && !string.IsNullOrWhiteSpace(displayName))
            return await TryMatchMemberByName(ctx, displayName);

        return null;
    }

    private static async Task<ulong?> TryMatchMemberByName(InteractionContext ctx, string displayName)
    {
        try
        {
            var matches = await ctx.Rest.SearchGuildMembers(ctx.GuildId, displayName, 10);
            if (matches == null)
                return null;

            var unique = matches
                .Where(m => m.User != null && m.User.Bot != true)
                .Where(m => string.Equals(m.Nick, displayName, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(m.User.GlobalName, displayName, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(m.User.Username, displayName, StringComparison.OrdinalIgnoreCase))
                .Select(m => m.User.Id)
                .Distinct()
                .ToList();

            return unique.Count == 1 ? unique[0] : null;
        }
        catch
        {
            // Member search can fail (missing intent/permission); skip the ping in that case.
            return null;
        }
    }

    private static Embed BuildReplyFallbackEmbed(string jumpLink)
    {
        return new Embed
        {
            Description = $"**[Reply to:]({jumpLink})**"
        };
    }

    private static string BuildReplyDescription(Message repliedTo, string jumpLink)
    {
        if (string.IsNullOrWhiteSpace(repliedTo.Content))
            return $"*[(click to see attachment)]({jumpLink})*";

        var msg = Regex.Replace(repliedTo.Content, @"(?:(?:([_\*]) \1)?\n){2,}", "\n");
        if (msg.Length > 100)
        {
            msg = SafeTruncate(msg, 100);

            if (UrlRegex.IsMatch(msg))
            {
                msg += repliedTo.Content.Substring(Math.Min(100, repliedTo.Content.Length)).Split(" ")[0];
                if (msg.Length > 300)
                    msg = Regex.Replace(msg, UrlRegex.ToString(), $"*[(very long link removed, click to see original message)]({jumpLink})*");
            }

            if (msg != repliedTo.Content)
                msg += "…";
        }

        var content = $"**[Reply to:]({jumpLink})** {msg}";
        if (repliedTo.Attachments.Length > 0 || repliedTo.Embeds?.Length > 0)
            content += $" {Emojis.Paperclip}";

        return content;
    }

    private static string DisplayName(User user)
    {
        return user.GlobalName ?? user.Username;
    }

    // Truncate at a safe Unicode boundary so we never split a surrogate pair,
    // a Discord custom emoji token <:name:id>, or a ZWJ/variation-selector sequence.
    private static string SafeTruncate(string s, int maxChars)
    {
        if (s.Length <= maxChars) return s;
        int cut = maxChars;
        if (cut < s.Length && char.IsLowSurrogate(s[cut])) cut--;
        int lastAngle = s.LastIndexOf('<', cut - 1);
        if (lastAngle >= 0)
        {
            var tok = Regex.Match(s[lastAngle..], @"^<a?:[^:>]+:\d+>");
            if (tok.Success && lastAngle + tok.Length > cut)
                cut = lastAngle;
        }
        while (cut > 0 && cut < s.Length &&
               (s[cut] == '\uFE0F' || s[cut] == '\u200D' || char.IsLowSurrogate(s[cut])))
            cut--;
        return s[..cut];
    }

    // Replace <@id> and <@!id> Discord mention syntax with the user's display name
    // so the TTS generator speaks a name instead of a number string.
    private static async Task<string> ResolveMentionsForTts(InteractionContext ctx, string text)
    {
        var mentionRegex = new Regex(@"<@!?(\d+)>");
        var matches = mentionRegex.Matches(text);
        if (matches.Count == 0)
            return text;

        var resolved = ctx.Event.Data?.Resolved?.Users;
        var replacements = new Dictionary<string, string>();

        foreach (Match m in matches)
        {
            if (replacements.ContainsKey(m.Value)) continue;
            if (!ulong.TryParse(m.Groups[1].Value, out var userId)) continue;

            // Default to empty string: if we can't resolve the user, strip the
            // mention rather than passing the raw "<@id>" token to the TTS engine
            // (which would be spoken as "at 123456789").
            string name = "";

            // Check interaction resolved data first (no API call needed).
            if (resolved != null && resolved.TryGetValue(userId, out var resolvedUser))
            {
                name = resolvedUser.GlobalName ?? resolvedUser.Username;
            }
            else
            {
                // Fall back to fetching the guild member for their server nick/display name.
                try
                {
                    var member = await ctx.Rest.GetGuildMember(ctx.GuildId, userId);
                    if (member != null)
                        name = member.Nick ?? member.User.GlobalName ?? member.User.Username;
                }
                catch { /* ignore, keep the empty default so the mention is stripped */ }
            }

            replacements[m.Value] = name;
        }

        return mentionRegex.Replace(text, m =>
            replacements.TryGetValue(m.Value, out var r) ? r : m.Value);
    }

    private static string? BuildAvatarUrl(InteractionContext ctx)
    {
        if (!string.IsNullOrWhiteSpace(ctx.Member?.Avatar))
            return $"https://cdn.discordapp.com/guilds/{ctx.GuildId}/users/{ctx.User.Id}/avatars/{ctx.Member.Avatar}.png?size=4096";

        if (!string.IsNullOrWhiteSpace(ctx.User.Avatar))
            return $"https://cdn.discordapp.com/avatars/{ctx.User.Id}/{ctx.User.Avatar}.png?size=4096";

        return null;
    }

    private static string? BuildUserAvatarUrl(User user)
    {
        if (string.IsNullOrWhiteSpace(user.Avatar))
            return null;

        return $"https://cdn.discordapp.com/avatars/{user.Id}/{user.Avatar}.png";
    }

    private record ReplyTarget(ulong GuildId, ulong ChannelId, ulong MessageId);
    private record ReplyRender(Embed Embed, ulong? PingUserId);
    internal readonly record struct VoiceDefinition(string Id, string Name);
}
