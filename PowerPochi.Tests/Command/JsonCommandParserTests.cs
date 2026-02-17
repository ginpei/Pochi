using PowerPochi.Command;
using Xunit;

namespace PowerPochi.Tests.Command;

public class JsonCommandParserTests
{
    [Fact]
    public void Parse_ShouldReturnCommand_WhenValid()
    {
        var parser = new JsonCommandParser();
        var json = "{\"type\":\"command\",\"command\":\"Next\",\"clientId\":\"test\"}";

        var result = parser.Parse(json);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Command);
        Assert.Equal(CommandType.Next, result.Command!.Command);
        Assert.Equal("test", result.Command.ClientId);
    }

    [Fact]
    public void Parse_ShouldFail_WhenUnknownCommand()
    {
        var parser = new JsonCommandParser();
        var json = "{\"type\":\"command\",\"command\":\"Unknown\"}";

        var result = parser.Parse(json);

        Assert.False(result.IsSuccess);
        Assert.Equal("unknown command", result.Error);
    }
}
