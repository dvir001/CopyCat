using ApplicationCommandType = Myriad.Types.ApplicationCommand.ApplicationCommandType;
using Myriad.Types;

namespace PluralKit.Bot;

public partial class ApplicationCommandTree
{
    public static ApplicationCommand Say = new(
        ApplicationCommandType.ChatInput,
        "s",
        "Speak through CopyCat as yourself",
        new[]
        {
            new ApplicationCommandOption(ApplicationCommandOption.OptionType.String, "text",
                "Message text to send")
            {
                Required = true
            },
            new ApplicationCommandOption(ApplicationCommandOption.OptionType.Attachment, "attachment",
                "Optional file to include")
            {
                Required = false
            },
            new ApplicationCommandOption(ApplicationCommandOption.OptionType.String, "reply_to",
                "Optional message link to reply to")
            {
                Required = false
            },
        });

    public static ApplicationCommand Tts = new(
        ApplicationCommandType.ChatInput,
        "tts",
        "Generate a voice clip from text",
        new[]
        {
            new ApplicationCommandOption(ApplicationCommandOption.OptionType.String, "text",
                "Message text to speak")
            {
                Required = true
            },
            new ApplicationCommandOption(ApplicationCommandOption.OptionType.String, "voice",
                "Voice preset")
            {
                Required = true,
                Autocomplete = true
            },
            new ApplicationCommandOption(ApplicationCommandOption.OptionType.Attachment, "attachment",
                "Optional file to include")
            {
                Required = false
            },
            new ApplicationCommandOption(ApplicationCommandOption.OptionType.String, "reply_to",
                "Optional message link or id to reply to")
            {
                Required = false
            },
        });

    public static ApplicationCommand TtsGroup = new(
        ApplicationCommandType.ChatInput,
        "ttsg",
        "Generate a multi-speaker voice clip",
        new[]
        {
            new ApplicationCommandOption(ApplicationCommandOption.OptionType.String, "text",
                "Numbered dialogue using 1:, 2:, and optionally 3:")
            {
                Required = true
            },
            new ApplicationCommandOption(ApplicationCommandOption.OptionType.String, "voice1",
                "Voice for speaker 1")
            {
                Required = true,
                Autocomplete = true
            },
            new ApplicationCommandOption(ApplicationCommandOption.OptionType.String, "voice2",
                "Voice for speaker 2")
            {
                Required = true,
                Autocomplete = true
            },
            new ApplicationCommandOption(ApplicationCommandOption.OptionType.String, "voice3",
                "Optional voice for speaker 3")
            {
                Required = false,
                Autocomplete = true
            },
            new ApplicationCommandOption(ApplicationCommandOption.OptionType.Attachment, "attachment",
                "Optional file to include")
            {
                Required = false
            },
            new ApplicationCommandOption(ApplicationCommandOption.OptionType.String, "reply_to",
                "Optional message link or id to reply to")
            {
                Required = false
            },
        });

    public static ApplicationCommand ProxiedMessageQuery = new(ApplicationCommandType.Message, "\U00002753 Message info");
    public static ApplicationCommand ProxiedMessageDelete = new(ApplicationCommandType.Message, "\U0000274c Delete message");
    public static ApplicationCommand ProxiedMessagePing = new(ApplicationCommandType.Message, "\U0001f514 Ping author");
    public static ApplicationCommand SayContextMenu = new(ApplicationCommandType.Message, "\U0001f4ac Reply as me");
    public static ApplicationCommand TtsReply = new(ApplicationCommandType.Message, "\U0001f50a Reply as me (TTS)");
}