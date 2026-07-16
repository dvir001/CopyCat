using ApplicationCommandType = Myriad.Types.ApplicationCommand.ApplicationCommandType;
using InteractionType = Myriad.Types.Interaction.InteractionType;
using Myriad.Rest;
using Myriad.Rest.Types;

namespace PluralKit.Bot;

public partial class ApplicationCommandTree
{
    public static async Task RegisterGlobalCommands(DiscordApiClient rest, ulong applicationId)
    {
        var commands = new[] { Say, Tts, TtsGroup, ProxiedMessageDelete, SayContextMenu, TtsReply }
            .Select(command => new ApplicationCommandRequest
            {
                Type = command.Type,
                Name = command.Name,
                Description = command.Description ?? "",
                Options = command.Options?.ToList()
            })
            .ToList();

        await rest.ReplaceGlobalApplicationCommands(applicationId, commands);
    }

    public Task TryHandleCommand(InteractionContext ctx)
    {
        if (ctx.Event.Data!.Name == Say.Name)
            return ctx.Execute<ApplicationCommandSay>(Say, m => m.SendAsInvoker(ctx));
        else if (ctx.Event.Data!.Name == Tts.Name)
            return ctx.Execute<ApplicationCommandTts>(Tts, m => m.SendAsInvoker(ctx));
        else if (ctx.Event.Data!.Name == TtsGroup.Name)
            return ctx.Execute<ApplicationCommandTts>(TtsGroup, m => m.SendGroupAsInvoker(ctx));
        else if (ctx.Event.Data!.Name == ProxiedMessageDelete.Name)
            return ctx.Execute<ApplicationCommandProxiedMessage>(ProxiedMessageDelete, m => m.DeleteMessage(ctx));
        else if (ctx.Event.Data!.Name == SayContextMenu.Name)
            return ctx.Execute<ApplicationCommandSay>(SayContextMenu, m => m.ShowSayReplyModal(ctx));
        else if (ctx.Event.Data!.Name == TtsReply.Name)
            return ctx.Execute<ApplicationCommandTtsReply>(TtsReply, m => m.ShowModal(ctx));

        return null;
    }

    public Task TryHandleModalSubmit(InteractionContext ctx)
    {
        var customId = ctx.Event.Data?.CustomId ?? "";
        if (customId.StartsWith("say-reply:"))
            return ctx.Execute<ApplicationCommandSay>(null, m => m.HandleSayReplyModal(ctx));
        if (customId.StartsWith("ttsreply-modal:"))
            return ctx.Execute<ApplicationCommandTtsReply>(null, m => m.HandleModalSubmit(ctx));

        return null;
    }

    public Task TryHandleAutocomplete(InteractionContext ctx)
    {
        if (ctx.Event.Data!.Name == Tts.Name)
            return ctx.ExecuteAutocomplete<ApplicationCommandTts>(Tts, m => m.Autocomplete(ctx));
        if (ctx.Event.Data!.Name == TtsGroup.Name)
            return ctx.ExecuteAutocomplete<ApplicationCommandTts>(TtsGroup, m => m.AutocompleteGroup(ctx));

        return null;
    }
}