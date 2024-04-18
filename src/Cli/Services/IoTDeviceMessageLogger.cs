using System.Text;
using Microsoft.Azure.Devices.Client;

namespace Services;

public class IoTDeviceMessageLogger(string connectionString) : IMessageLogger, IDisposable
{
    private readonly DeviceClient deviceClient = DeviceClient.CreateFromConnectionString(connectionString);
    private bool disposed = false;

    public Task LogJSONMessageAsync(string jsonMessage)
    {
        using var message = new Message(Encoding.UTF8.GetBytes(jsonMessage))
        {
            ContentEncoding = "utf-8",
            ContentType = "application/json",
        };

        return deviceClient.SendEventAsync(message);
    }

    public Task CloseAsync()
    {
        return deviceClient.CloseAsync();
    }

    public void Dispose()
    {
        if (!disposed)
        {
            deviceClient?.Dispose();
            disposed = true;
        }
    }

}