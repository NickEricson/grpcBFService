namespace GrpcBFService.Services;

using Grpc.Core;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Schema;
using System.Threading.Channels;
using System.Threading.Tasks;

public class BotApiService : BotApi.BotApiBase
{
    private readonly ILogger<BotApiService> _logger;
    private readonly IBot _bot;

    public BotApiService(IBot bot, ILogger<BotApiService> logger) =>
        (_bot, _logger) = (
            bot ?? throw new ArgumentNullException(nameof(bot)),
            logger ?? throw new ArgumentNullException(nameof(logger)));

    public override async Task Connect(IAsyncStreamReader<BFPayload> requestStream, IServerStreamWriter<BFPayload> responseStream, ServerCallContext context)
    {
        // Channel to handle activities the bot sends.
        var botSendActivityChannel = Channel.CreateUnbounded<Activity>();

        // Channel to make stream write operations thread safe.
        var respondToChannelChannel = Channel.CreateUnbounded<BFPayload>();

        // Wire response stream to channel.
        var responseToChannelTask = await Task.Factory.StartNew(async () =>
        {
            while (await respondToChannelChannel.Reader.WaitToReadAsync())
            {
                await foreach (var payload in respondToChannelChannel.Reader.ReadAllAsync(context.CancellationToken))
                {
                    await responseStream.WriteAsync(payload, context.CancellationToken);
                }
            }
        });

        // Wire bot activity stream to response stream.
        var processBotActivityTask = await Task.Factory.StartNew(async () =>
        {
            while (await botSendActivityChannel.Reader.WaitToReadAsync())
            {
                await foreach (var activity in botSendActivityChannel.Reader.ReadAllAsync(context.CancellationToken))
                {
                    BFPayload payload = new()
                    {
                        Kind = "Activity",
                        Body = Newtonsoft.Json.JsonConvert.SerializeObject(activity) // Newtonsoft for adaptive cards.
                    };
                    await respondToChannelChannel.Writer.WriteAsync(payload, context.CancellationToken);
                }
            }
        }, context.CancellationToken);

        // Wire channel message stream to bot.
        var processChannelMessageTask = await Task.Factory.StartNew(async () =>
        {
            GrpcBotAdapter botAdapter = new(botSendActivityChannel);
            while (!context.CancellationToken.IsCancellationRequested && await requestStream.MoveNext())
            {
                var request = requestStream.Current;

                try
                {
                    switch (request.Kind)
                    {
                        case "Activity":
                            var activity = Newtonsoft.Json.JsonConvert.DeserializeObject<Activity>(request.Body);
                            await botAdapter.ProcessActivityAsync(_bot, activity, context.CancellationToken);
                            break;
                        case "RequestResponse":
                            var resourceResponse = Newtonsoft.Json.JsonConvert.DeserializeObject<ResourceResponse>(request.Body);
                            // The channel responds with Ids for sent activities and we will need to route those to the bot (which is on a different thread).
                            _logger.LogInformation($"Got back id {resourceResponse.Id}. Should route this to the bot, but aren't.");
                            break;
                        default:
                            _logger.LogInformation($"Don't know how to handle {request.Kind}. Ignoring.");
                            break;
                    }
                }
                catch (Exception ex)
                {
                    BFPayload payload = new()
                    {
                        Kind = "Error",
                        Body = ex.ToString()
                    };
                    await respondToChannelChannel.Writer.WriteAsync(payload, context.CancellationToken);
                }
            }

            // Channel closed input, disconnect.
            botSendActivityChannel.Writer.Complete();
            respondToChannelChannel.Writer.Complete();
        }, context.CancellationToken);

        try
        {
            await Task.WhenAll(processChannelMessageTask, responseToChannelTask, processBotActivityTask);
        }
        catch (Exception ex)
        {
            _logger.LogError("Error! " + ex.ToString());
        }
    }
}