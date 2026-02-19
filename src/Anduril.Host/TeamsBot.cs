using Anduril.Communication;
using Microsoft.Bot.Builder;

namespace Anduril.Host;

/// <summary>
/// Bot Framework bot implementation that bridges incoming Teams activities
/// to the <see cref="TeamsAdapter"/> for processing.
/// </summary>
public sealed class TeamsBot(TeamsAdapter teamsAdapter) : IBot
{
    public async Task OnTurnAsync(ITurnContext turnContext, CancellationToken cancellationToken = default)
    {
        await teamsAdapter.ProcessActivityAsync(turnContext, cancellationToken);
    }
}

