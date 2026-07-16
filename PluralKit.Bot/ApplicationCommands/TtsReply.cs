using Myriad.Types;

using PluralKit.Core;

namespace PluralKit.Bot;

// "🔊 Reply as me (TTS)" message context-menu command.
//
// Flow (fully stateless — all routing state is encoded in the modal custom_id, since Discord
// components carry no server-side state):
//   1. Right-click a message → Apps → "🔊 Reply as me (TTS)"  →  ShowModal opens a single modal
//      containing a voice String Select, a paragraph text input, and an optional File Upload, with
//      custom_id "ttsreply-modal:{targetMsgId}".
//   2. Submitting the modal fires HandleModalSubmit, which reads the chosen voice, text and any
//      uploaded file, generates the clip and sends it as a webhook reply to the target message
//      (reusing ApplicationCommandTts.SendTtsReply).
public class ApplicationCommandTtsReply
{
    public const string ModalPrefix = "ttsreply-modal:";

    // Discord select menus allow at most 25 options.
    private const int MaxVoiceOptions = 25;

    private readonly TtsVoiceService _voices;
    private readonly ApplicationCommandTts _ttsCommand;

    public ApplicationCommandTtsReply(TtsVoiceService voices, ApplicationCommandTts ttsCommand)
    {
        _voices = voices;
        _ttsCommand = ttsCommand;
    }

    // Message context-menu handler. Opens the modal directly: voice select + text + optional file.
    public async Task ShowModal(InteractionContext ctx)
    {
        if (ctx.GuildId == 0)
            throw new PKError("Reply as me (TTS) only works in servers.");

        var targetId = ctx.Event.Data?.TargetId
            ?? throw new PKError("Could not determine which message to reply to.");

        // Lead with this user's own most-used voices, then back-fill with the rest of the voice
        // catalog (in catalog order) so even a brand-new user gets a useful picker. Distinct
        // preserves the most-used-first order.
        IEnumerable<string> mostUsed;
        try
        {
            mostUsed = await ctx.Repository.GetMostUsedVoices(ctx.User.Id, MaxVoiceOptions);
        }
        catch
        {
            mostUsed = Array.Empty<string>();
        }

        var options = mostUsed
            .Concat(_voices.Catalog.Select(v => v.Id))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(id => _voices.Catalog.FirstOrDefault(v =>
                string.Equals(v.Id, id, StringComparison.OrdinalIgnoreCase)))
            .Where(v => !string.IsNullOrEmpty(v.Id) && _voices.IsVoiceAvailable(v.Id))
            .Take(MaxVoiceOptions)
            .Select(v => new SelectOption
            {
                Label = v.Name.Length > 100 ? v.Name[..100] : v.Name,
                Value = v.Id,
            })
            .ToArray();

        if (options.Length == 0)
            throw new PKError("No TTS voices are currently available.");

        // Newer Discord modals wrap each input in a Label (type 18): a required voice String Select,
        // a required paragraph text input, and an optional File Upload (type 19, modal-only).
        await ctx.RespondModal(
            $"{ModalPrefix}{targetId}",
            "Reply as me (TTS)",
            new[]
            {
                new MessageComponent
                {
                    Type = ComponentType.Label,
                    Label = "Voice",
                    Component = new MessageComponent
                    {
                        Type = ComponentType.StringSelect,
                        CustomId = "voice",
                        Placeholder = "Choose a voice…",
                        Required = true,
                        Options = options,
                    },
                },
                new MessageComponent
                {
                    Type = ComponentType.Label,
                    Label = "Message",
                    Component = new MessageComponent
                    {
                        Type = ComponentType.TextInput,
                        CustomId = "text",
                        Style = ButtonStyle.Secondary, // 2 = Paragraph
                        Required = true,
                        MaxLength = 2000,
                        Placeholder = "What should the voice say?",
                    },
                },
                new MessageComponent
                {
                    Type = ComponentType.Label,
                    Label = "Attach a file (optional)",
                    Required = false,
                    Component = new MessageComponent
                    {
                        Type = ComponentType.FileUpload,
                        CustomId = "file",
                        Required = false,
                        MinValues = 0,
                        MaxValues = 1,
                    },
                },
            });
    }

    // Modal-submit handler. Reads the chosen voice, text and optional file; generates the clip and
    // sends it as a reply to the target message.
    public async Task HandleModalSubmit(InteractionContext ctx)
    {
        var customId = ctx.CustomId ?? string.Empty;
        var targetIdRaw = customId.Length > ModalPrefix.Length ? customId[ModalPrefix.Length..] : string.Empty;
        if (!ulong.TryParse(targetIdRaw, out var targetId))
            throw new PKError("Could not determine which message to reply to.");

        var voiceId = ApplicationCommandSay.FlattenModalComponents(ctx.Event.Data?.Components)
            .FirstOrDefault(c => string.Equals(c.CustomId, "voice", StringComparison.Ordinal))
            ?.Values?.FirstOrDefault();
        if (string.IsNullOrEmpty(voiceId))
            throw new PKError("No voice was selected.");

        var text = ApplicationCommandSay.GetModalText(ctx.Event.Data?.Components, "text");
        if (string.IsNullOrWhiteSpace(text))
            throw new PKError("Provide text to speak.");

        var attachment = ApplicationCommandSay.GetModalAttachment(ctx.Event.Data, "file");

        // TTS generation can exceed Discord's 3-second interaction deadline, so defer (ephemeral)
        // before doing the work. Errors after this point edit the deferred reply.
        await ctx.Defer();

        await _ttsCommand.SendTtsReply(ctx, voiceId, text, targetId, attachment);

        // Remove the deferred ephemeral "thinking" indicator now that the reply was sent.
        try
        {
            await ctx.DeleteReply();
        }
        catch
        {
            // Keep successful send behavior even if ephemeral cleanup fails.
        }
    }
}