using AutoMediaRenamer.Services;
using Xunit;

namespace AutoMediaRenamer.Tests;

public class TitleCaseHelperTests
{
    [Theory]
    [InlineData("slow.horses", "Slow Horses")]
    [InlineData("the_lord_of_the_rings", "The Lord of the Rings")]
    [InlineData("cats.in.the.bag", "Cats in the Bag")]
    [InlineData("gladiator.ii", "Gladiator II")]
    [InlineData("doctor.who.2005", "Doctor Who 2005")]
    [InlineData("can't.buy.me.love", "Can't Buy Me Love")]
    [InlineData("hello-ms-cobel", "Hello Ms Cobel")]
    public void ToTitleCase_FormatsProperly(string input, string expected)
    {
        string actual = TitleCaseHelper.ToTitleCase(input);
        Assert.Equal(expected, actual);
    }
}
