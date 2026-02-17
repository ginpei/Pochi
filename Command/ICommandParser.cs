namespace PowerPochi.Command;

public interface ICommandParser
{
    CommandParseResult Parse(string message);
}
