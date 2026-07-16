using System.Text.RegularExpressions;
using System.Text;

namespace PluralKit.Bot;

public static partial class TtsDialogueParser
{
    public static IReadOnlyList<TtsDialogueLine> Parse(string input, int speakerCount)
    {
        if (speakerCount is < 2 or > 3)
            throw new ArgumentOutOfRangeException(nameof(speakerCount));

        var matches = SpeakerMarkerRegex().Matches(input);
        if (matches.Count == 0 || !string.IsNullOrWhiteSpace(input[..matches[0].Index]))
            throw new TtsDialogueParseException("Dialogue must start with 1:, 2:, or 3:.");

        var lines = new List<TtsDialogueLine>(matches.Count);
        for (var index = 0; index < matches.Count; index++)
        {
            var match = matches[index];
            var speaker = int.Parse(match.Groups[1].Value);
            if (speaker > speakerCount)
                throw new TtsDialogueParseException($"Speaker {speaker} does not have a configured voice.");

            var textStart = match.Index + match.Length;
            var textEnd = index + 1 < matches.Count ? matches[index + 1].Index : input.Length;
            var text = input[textStart..textEnd].Trim();
            if (text.Length == 0)
                throw new TtsDialogueParseException($"Speaker {speaker} has an empty line.");

            var fragments = new List<TtsDialogueFragment>();
            var cuePrefixes = new List<string>();
            var position = 0;
            foreach (Match cue in OverlapCueRegex().Matches(text))
            {
                AddTextFragment(fragments, text[position..cue.Index]);
                cuePrefixes.Add(RemoveOverlapCues(text[..cue.Index]).Trim());

                var targetSpeaker = int.Parse(cue.Groups[1].Value);
                if (targetSpeaker > speakerCount)
                    throw new TtsDialogueParseException($"Speaker {targetSpeaker} does not have a configured voice.");
                if (targetSpeaker == speaker)
                    throw new TtsDialogueParseException("An overlap cue must target a different speaker.");

                fragments.Add(new TtsDialogueOverlapCue(targetSpeaker));
                position = cue.Index + cue.Length;
            }

            AddTextFragment(fragments, text[position..]);
            var spokenText = RemoveOverlapCues(text).Trim();
            lines.Add(new TtsDialogueLine(speaker, fragments, spokenText, cuePrefixes));
        }

        if (lines.Count == 0)
            throw new TtsDialogueParseException("Provide at least one speaker line.");

        return lines;
    }

    private static void AddTextFragment(List<TtsDialogueFragment> fragments, string text)
    {
        var trimmed = text.Trim();
        if (trimmed.Length > 0)
            fragments.Add(new TtsDialogueText(trimmed));
    }

    private static string RemoveOverlapCues(string text)
    {
        var result = new StringBuilder(text.Length);
        var position = 0;
        foreach (Match cue in OverlapCueRegex().Matches(text))
        {
            result.Append(text, position, cue.Index - position);

            var whitespaceBefore = cue.Index > 0 && char.IsWhiteSpace(text[cue.Index - 1]);
            var positionAfterCue = cue.Index + cue.Length;
            var whitespaceAfter = positionAfterCue < text.Length && char.IsWhiteSpace(text[positionAfterCue]);
            if (whitespaceBefore || whitespaceAfter)
            {
                while (result.Length > 0 && char.IsWhiteSpace(result[^1]))
                    result.Length--;
                result.Append(' ');
                while (positionAfterCue < text.Length && char.IsWhiteSpace(text[positionAfterCue]))
                    positionAfterCue++;
            }

            position = positionAfterCue;
        }

        result.Append(text, position, text.Length - position);
        return result.ToString();
    }

    [GeneratedRegex(@"(?:^|\s+)([1-3])\s*:\s*", RegexOptions.CultureInvariant)]
    private static partial Regex SpeakerMarkerRegex();

    [GeneratedRegex(@"\(([1-3])\)", RegexOptions.CultureInvariant)]
    private static partial Regex OverlapCueRegex();
}

public sealed record TtsDialogueLine(int Speaker, IReadOnlyList<TtsDialogueFragment> Fragments, string SpokenText,
    IReadOnlyList<string> CuePrefixes);

public abstract record TtsDialogueFragment;

public sealed record TtsDialogueText(string Text): TtsDialogueFragment;

public sealed record TtsDialogueOverlapCue(int Speaker): TtsDialogueFragment;

public sealed class TtsDialogueParseException(string message): PKError(message);