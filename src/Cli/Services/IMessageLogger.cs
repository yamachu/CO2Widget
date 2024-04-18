namespace Services;

public interface IMessageLogger : IDisposable
{
    Task LogJSONMessageAsync(string jsonMessage);

    Task CloseAsync();
}
