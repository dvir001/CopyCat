using ApplicationCommandType = Myriad.Types.ApplicationCommand.ApplicationCommandType;
using Myriad.Types;

namespace PluralKit.Bot;

public class ApplicationCommand
{
    public ApplicationCommand(ApplicationCommandType type, string name, string? description = null,
                              ApplicationCommandOption[]? options = null)
    {
        Type = type;
        Name = name;
        Description = description;
        Options = options;
    }

    public ApplicationCommandType Type { get; }
    public string Name { get; }
    public string Description { get; }
    public ApplicationCommandOption[]? Options { get; }
}