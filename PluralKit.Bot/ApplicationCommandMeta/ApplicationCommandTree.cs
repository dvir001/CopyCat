using ApplicationCommandType = Myriad.Types.ApplicationCommand.ApplicationCommandType;
using InteractionType = Myriad.Types.Interaction.InteractionType;

namespace PluralKit.Bot;

public partial class ApplicationCommandTree
{
    public Task TryHandleCommand(InteractionContext ctx)
    {
        if (ctx.Event.Data!.Name == Say.Name)
            return ctx.Execute<ApplicationCommandSay>(Say, m => m.SendAsInvoker(ctx));
        else if (ctx.Event.Data!.Name == Tts.Name)
            return ctx.Execute<ApplicationCommandTts>(Tts, m => m.SendAsInvoker(ctx));
        else if (ctx.Event.Data!.Name == ProxiedMessageDelete.Name)
            return ctx.Execute<ApplicationCommandProxiedMessage>(ProxiedMessageDelete, m => m.DeleteMessage(ctx));
        else if (ctx.Event.Data!.Name == SayContextMenu.Name)
            return ctx.Execute<ApplicationCommandSay>(SayContextMenu, m => m.ShowSayReplyModal(ctx));

        return null;
    }

    public Task TryHandleModalSubmit(InteractionContext ctx)
    {
        var customId = ctx.Event.Data?.CustomId ?? "";
        if (customId.StartsWith("say-reply:"))
            return ctx.Execute<ApplicationCommandSay>(null, m => m.HandleSayReplyModal(ctx));

        return null;
    }

    public Task TryHandleAutocomplete(InteractionContext ctx)
    {
        if (ctx.Event.Data!.Name == Tts.Name)
            return ctx.ExecuteAutocomplete<ApplicationCommandTts>(Tts, m => m.Autocomplete(ctx));

        return null;
    }
}