namespace Services;

public class ConsoleMessageLogger() : IMessageLogger, IDisposable
{
    private bool closed = false;

    public Task LogJSONMessageAsync(string jsonMessage)
    {
        if (closed)
        {
            throw new InvalidOperationException("The logger is already closed.");
        }
        return Console.Out.WriteLineAsync(jsonMessage);
    }

    public Task CloseAsync()
    {
        closed = true;
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        // Do nothing
    }
}