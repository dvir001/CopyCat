using System.Text.Json.Serialization;

namespace Myriad.Types;

public record SelectOption
{
    public string Label { get; init; }
    public string Value { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Description { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? Default { get; init; }
}
