using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Grpc.Net.Client;
using GrpcBFService;
using Microsoft.Bot.Schema;
using System.Threading.Channels;

// Channel to make writes to bot thread safe.
Channel<BFPayload> writeToBotChannel = Channel.CreateUnbounded<BFPayload>();

using CancellationTokenSource cancellationTokenSource = new();

using var channel = GrpcChannel.ForAddress("https://localhost:7183", new GrpcChannelOptions
{
    HttpHandler = new SocketsHttpHandler
    {
        // .net 5 only, see: https://docs.microsoft.com/en-us/aspnet/core/grpc/performance?view=aspnetcore-5.0
        EnableMultipleHttp2Connections = true,

        PooledConnectionIdleTimeout = Timeout.InfiniteTimeSpan,
        KeepAlivePingDelay = TimeSpan.FromSeconds(60),
        KeepAlivePingTimeout = TimeSpan.FromSeconds(30)
    }
});

var botApiClient = new BotApi.BotApiClient(channel);

// Establish 2-way connection to bot.
using var botConnection = botApiClient.Connect(cancellationToken: cancellationTokenSource.Token);

// Wire bot responses to console.
var processBotMessageTask = await Task.Factory.StartNew(async () =>
{
    try
    {
        await foreach (var response in botConnection.ResponseStream.ReadAllAsync(cancellationTokenSource.Token))
        {
            switch (response.Kind)
            {
                case "Activity":
                    // Respond to the bot with the activity id.
                    BFPayload payload = new()
                    {
                        Kind = "RequestResponse",
                        Body = Newtonsoft.Json.JsonConvert.SerializeObject(new ResourceResponse("5"))
                    };
                    await writeToBotChannel.Writer.WriteAsync(payload);

                    // Handle the activity (push to client).
                    var activity = Newtonsoft.Json.JsonConvert.DeserializeObject<Activity>(response.Body);
                    Console.WriteLine($"{activity.Type}: {activity.Text}");
                    break;
                default:
                    Console.WriteLine($"Unknown response kind {response.Kind}. Ignoring");
                    break;
            }
        }
    }
    catch (RpcException rpcException) when (rpcException.StatusCode == StatusCode.Cancelled)
    {
        // Handle shutting down everything.
    }
});

// Wire channel writer channel to bot.
var writeToBotTask = await Task.Factory.StartNew(async () =>
{
    await foreach (var message in writeToBotChannel.Reader.ReadAllAsync())
    {
        await botConnection.RequestStream.WriteAsync(message);
    }
});

// Process messages from console.
Console.WriteLine("Type something (blank to quit)");
while (true)
{
    var input = Console.ReadLine();

    // If signalled, close everything down.
    if (string.IsNullOrEmpty(input))
    {
        writeToBotChannel.Writer.Complete();
        await botConnection.RequestStream.CompleteAsync();
        cancellationTokenSource.Cancel();
        break;
    }

    // Push the activity to the writer channel.
    var activity = Activity.CreateMessageActivity();
    activity.Text = input;
    activity.TextFormat = TextFormatTypes.Plain;
    BFPayload payload = new()
    {
        Kind = "Activity",
        Body = Newtonsoft.Json.JsonConvert.SerializeObject(activity)
    };
    await writeToBotChannel.Writer.WriteAsync(payload);
}

// Why not be friendly.
await Task.WhenAll(processBotMessageTask, writeToBotTask);
