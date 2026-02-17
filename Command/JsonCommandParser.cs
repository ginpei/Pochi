using System.Text.Json;

namespace PowerPochi.Command;

public sealed class JsonCommandParser : ICommandParser
{
    private readonly JsonSerializerOptions _serializerOptions;

    public JsonCommandParser()
    {
        _serializerOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };
    }

    public CommandParseResult Parse(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return CommandParseResult.Failure("empty message");
        }

        try
        {
            var envelope = JsonSerializer.Deserialize<CommandEnvelope>(message, _serializerOptions);
            if (envelope is null)
            {
                return CommandParseResult.Failure("invalid message");
            }

            if (!string.Equals(envelope.Type, "command", StringComparison.OrdinalIgnoreCase))
            {
                return CommandParseResult.Failure("unsupported type");
            }

            if (string.IsNullOrWhiteSpace(envelope.Command) || !Enum.TryParse<CommandType>(envelope.Command, true, out var parsed))
            {
                return CommandParseResult.Failure("unknown command");
            }

            return CommandParseResult.Success(new CommandRequest(parsed, envelope.ClientId));
        }
        catch (JsonException ex)
        {
            return CommandParseResult.Failure($"invalid json: {ex.Message}");
        }
    }
}
