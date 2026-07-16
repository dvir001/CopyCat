using PluralKit.Bot;

using Xunit;

namespace PluralKit.Tests;

public class TtsDialogueParserTests
{
    [Fact]
    public void ParsesNumberedSpeakerLines()
    {
        var lines = TtsDialogueParser.Parse("1: Hello there.\n2: General Kenobi.", 2);

        Assert.Collection(lines,
            line =>
            {
                Assert.Equal(1, line.Speaker);
                Assert.Equal(new TtsDialogueText("Hello there."), Assert.Single(line.Fragments));
            },
            line =>
            {
                Assert.Equal(2, line.Speaker);
                Assert.Equal(new TtsDialogueText("General Kenobi."), Assert.Single(line.Fragments));
            });
    }

    [Fact]
    public void ParsesInlineSpeakerMarkersAndOverlapCue()
    {
        var lines = TtsDialogueParser.Parse(
            "1: First speaker 2: Second speaker continues (1) after cue 1: First speaker overlaps 2: Final line",
            2);

        Assert.Equal(new[] { 1, 2, 1, 2 }, lines.Select(line => line.Speaker));
        Assert.Equal(new TtsDialogueFragment[]
        {
            new TtsDialogueText("Second speaker continues"),
            new TtsDialogueOverlapCue(1),
            new TtsDialogueText("after cue")
        }, lines[1].Fragments);
    }

    [Fact]
    public void ParsesOverlapCueWithoutSpeakingIt()
    {
        var line = Assert.Single(TtsDialogueParser.Parse("1: Start (2) keep talking", 2));

        Assert.Equal(new TtsDialogueFragment[]
        {
            new TtsDialogueText("Start"),
            new TtsDialogueOverlapCue(2),
            new TtsDialogueText("keep talking")
        }, line.Fragments);
        Assert.Equal("Start keep talking", line.SpokenText);
        Assert.Equal("Start", Assert.Single(line.CuePrefixes));
    }

    [Fact]
    public void RemovesInlineCueWithoutSplittingSpokenSentence()
    {
        var line = Assert.Single(TtsDialogueParser.Parse("1: they (2)are evil.", 2));

        Assert.Equal("they are evil.", line.SpokenText);
    }

    [Fact]
    public void RemovesCueWithoutAddingWhitespace()
    {
        var line = Assert.Single(TtsDialogueParser.Parse("1: word(2)word", 2));

        Assert.Equal("wordword", line.SpokenText);
    }

    [Fact]
    public void RejectsSpeakerWithoutConfiguredVoice()
    {
        var exception = Assert.Throws<TtsDialogueParseException>(() =>
            TtsDialogueParser.Parse("1: Hello\n3: Surprise", 2));

        Assert.Equal("Speaker 3 does not have a configured voice.", exception.Message);
    }

    [Theory]
    [InlineData("This line has no speaker")]
    [InlineData("1:")]
    public void RejectsMalformedLines(string input)
    {
        var exception = Assert.Throws<TtsDialogueParseException>(() => TtsDialogueParser.Parse(input, 2));

        Assert.IsAssignableFrom<PKError>(exception);
    }
}