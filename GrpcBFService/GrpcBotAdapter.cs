namespace GrpcBFService;

using Microsoft.Bot.Builder;
using Microsoft.Bot.Schema;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

public class GrpcBotAdapter : BotAdapter
{
    private readonly Channel<Activity> _sender;

    public GrpcBotAdapter(Channel<Activity> sender) : base()
    {
        _sender = sender ?? throw new ArgumentNullException(nameof(sender));
    }

    public async Task ProcessActivityAsync(IBot bot, Activity activity, CancellationToken cancellationToken)
    {
        TurnContext turnContext = new(this, activity);
        await RunPipelineAsync(turnContext, bot.OnTurnAsync, cancellationToken);
    }

    public override Task DeleteActivityAsync(ITurnContext turnContext, ConversationReference reference, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public override async Task<ResourceResponse[]> SendActivitiesAsync(ITurnContext turnContext, Activity[] activities, CancellationToken cancellationToken)
    {
        foreach (var activity in activities)
        {
            await _sender.Writer.WriteAsync(activity, cancellationToken);
        }
        // Need to get back responses to here.
        return activities.Select(x => new ResourceResponse()).ToArray();
    }

    public override Task<ResourceResponse> UpdateActivityAsync(ITurnContext turnContext, Activity activity, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}
