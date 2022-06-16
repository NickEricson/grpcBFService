namespace GrpcBFService.Bots;

using Microsoft.Bot.Builder;
using Microsoft.Bot.Schema;

public class EchoBot : ActivityHandler
{
    protected override async Task OnMessageActivityAsync(ITurnContext<IMessageActivity> turnContext, CancellationToken cancellationToken)
    {
        await turnContext.SendActivityAsync(MessageFactory.Text($"Echo: {turnContext.Activity.Text}"), cancellationToken);
    }

    protected override async Task OnEventActivityAsync(ITurnContext<IEventActivity> turnContext, CancellationToken cancellationToken)
    {
        await turnContext.SendActivityAsync($"Event received. Name: {turnContext.Activity.Name}. Value: {turnContext.Activity.Value}", cancellationToken: cancellationToken);
    }

    protected override async Task OnMembersAddedAsync(IList<ChannelAccount> membersAdded, ITurnContext<IConversationUpdateActivity> turnContext, CancellationToken cancellationToken)
    {
        foreach (var member in membersAdded)
        {
            if (member.Id != turnContext.Activity.Recipient.Id)
            {
                await turnContext.SendActivityAsync(MessageFactory.Text($"Hello and welcome!"), cancellationToken);
            }
        }
    }
}
