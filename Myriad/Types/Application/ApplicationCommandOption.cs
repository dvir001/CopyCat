namespace Myriad.Types;

public record ApplicationCommandOption(ApplicationCommandOption.OptionType Type, string Name, string Description)
{
    public enum OptionType
    {
        Subcommand = 1,
        SubcommandGroup = 2,
        String = 3,
        Integer = 4,
        Boolean = 5,
        User = 6,
        Channel = 7,
        Role = 8,
        Mentionable = 9,
        Number = 10,
        Attachment = 11
    }

    public bool Default { get; init; }
    public bool Required { get; init; }
    public bool Autocomplete { get; init; }
    public Choice[]? Choices { get; init; }
    public ApplicationCommandOption[]? Options { get; init; }

    public record Choice(string Name, object Value);
}