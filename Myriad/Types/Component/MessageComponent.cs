using System.Text.Json.Serialization;

namespace Myriad.Types;

public record MessageComponent
{
    public ComponentType Type { get; init; }
    public ButtonStyle? Style { get; set; }
    public string? Label { get; init; }

    // Label (type 18) description text, shown under the label above the wrapped component.
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Description { get; init; }

    public string? Content { get; init; }
    public Emoji? Emoji { get; init; }
    public string? CustomId { get; init; }
    public string? Url { get; init; }
    public bool? Disabled { get; init; }
    public uint? AccentColor { get; init; }
    public int? Spacing { get; init; }
    public ComponentMedia? Media { get; init; }
    public ComponentMediaItem[]? Items { get; init; }

    // Text input fields
    public string? Value { get; init; }
    public string? Placeholder { get; init; }
    public bool? Required { get; init; }
    public int? MinLength { get; init; }
    public int? MaxLength { get; init; }

    // Select-menu fields
    public SelectOption[]? Options { get; init; }
    public int? MinValues { get; init; }
    public int? MaxValues { get; init; }

    // Values submitted for a String Select or File Upload component inside a modal.
    // For a File Upload these are attachment snowflake IDs that key into
    // ApplicationCommandInteractionData.Resolved.Attachments.
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string[]? Values { get; init; }

    public MessageComponent? Accessory { get; init; }

    // A Label (type 18) wraps a single interactive component via this field ("component",
    // singular). Used both when building a modal and when reading a submitted one.
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public MessageComponent? Component { get; init; }

    public MessageComponent[]? Components { get; init; }
}