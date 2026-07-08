namespace Myriad.Types;

public record ApplicationCommandInteractionDataOption
{
    public string Name { get; init; }
    public object? Value { get; init; }
    public bool? Focused { get; init; }
    public ApplicationCommandInteractionDataOption[]? Options { get; init; }
}