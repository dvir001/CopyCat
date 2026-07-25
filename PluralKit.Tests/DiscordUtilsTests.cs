using Myriad.Types;

using PluralKit.Bot;

using Xunit;

namespace PluralKit.Tests;

public class DiscordUtilsTests
{
    [Fact]
    public void MentionsUserMatchesResolvedMention()
    {
        var message = new Message
        {
            Mentions = new[]
            {
                new User.Extra { Id = 123 },
            },
        };

        Assert.True(message.MentionsUser(123));
        Assert.False(message.MentionsUser(456));
    }

    [Fact]
    public void MentionsUserHandlesMissingMentions()
    {
        var message = new Message { Mentions = null! };

        Assert.False(message.MentionsUser(123));
    }
}
