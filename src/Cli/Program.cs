using System.IO.Ports;
using System.Text.Json;
using System.Text.RegularExpressions;
using Spectre.Console;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Services;

DotNetEnv.Env.TraversePath().Load();

var builder = Host.CreateApplicationBuilder(args);

if (DotNetEnv.Env.GetString("AZURE_IOT_CONNECTION_STRING", "") != "")
{
    builder.Services.AddScoped<IMessageLogger, IoTDeviceMessageLogger>((_) =>
    {
        return new IoTDeviceMessageLogger(DotNetEnv.Env.GetString("AZURE_IOT_CONNECTION_STRING"));
    });
}
else
{
    builder.Services.AddScoped<IMessageLogger, ConsoleMessageLogger>();
}

var host = builder.Build();

// Wait 用の TaskCompletionSource
// var tcs = new TaskCompletionSource();
using var cts = new CancellationTokenSource();

var port = AnsiConsole.Prompt(
    new SelectionPrompt<string>()
        .Title("Select device port")
        .PageSize(10)
        .AddChoices(SerialPort.GetPortNames())
);

var serial = new SerialPort(port)
{
    BaudRate = 115200,
    DataBits = 8,
    Parity = Parity.None,
    StopBits = StopBits.One,
    Handshake = Handshake.None
};

using var deviceClient = host.Services.GetRequiredService<IMessageLogger>();

Console.CancelKeyPress += async (_, e) =>
{
    Console.WriteLine("Ctrl+C を受信しました。");
    e.Cancel = true;

    try
    {
        serial.Write("STP\r\n");
        serial.Close();
    }
    catch { }
    try
    {
        await deviceClient.CloseAsync();
    }
    catch { }
    cts.Cancel();
};

try
{
    serial.Open();
}
catch (Exception e)
{
    throw new Exception("`SerialPort.GetPortNames()` で当該の Device が存在するか確認してください。", e);
}

// StartCommand の送信
serial.Write("STA\r\n");

MyRoomData? latestData = null;

serial.DataReceived += (sender, _) =>
{
    var serial = (SerialPort)sender;
    var received = serial.ReadLine();
    if (MyRegex().IsMatch(received))
    {
        var matched = MyRegex().Match(received);
        var co2 = matched.Groups["CO2"];
        var hum = matched.Groups["HUM"];
        var tmp = matched.Groups["TMP"];
        Console.WriteLine($"CO2: {co2}, HUM: {hum}, TMP: {tmp}");

        Interlocked.Exchange(ref latestData, new MyRoomData
        (
            tmp.Value,
            hum.Value,
            co2.Value,
            DateTime.UtcNow
        ));
    }
};

var timer = new PeriodicTimer(TimeSpan.FromSeconds(15));

try
{
    while (await timer.WaitForNextTickAsync(cts.Token))
    {
        var myRoomData = Interlocked.CompareExchange(ref latestData, null, null);
        if (myRoomData is null)
        {
            continue;
        }
        var myRoomDataJson = JsonSerializer.Serialize(myRoomData, new JsonSerializerOptions { PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase });

        try
        {
            await deviceClient.LogJSONMessageAsync(myRoomDataJson);
        }
        catch (Exception e)
        {
            Console.Error.WriteLine(e.Message);
        }
    }
}
catch (OperationCanceledException) { }

// await tcs.Task;

partial class Program
{
    [GeneratedRegex("CO2=(?<CO2>[0-9]*),HUM=(?<HUM>[0-9]*\\.[0-9]+),TMP=(?<TMP>[0-9]*\\.[0-9]+)", RegexOptions.IgnoreCase, "ja-JP")]
    private static partial Regex MyRegex();
}

record MyRoomData(string temperature, string humidity, string co2, DateTime timestamp);
