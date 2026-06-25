using CommunityToolkit.Mvvm.Messaging;
using LifeGrid.Application.Gamification;

namespace LifeGrid.Presentation.Services;

internal sealed class WeakReferenceMessengerBroadcaster : IEconomyStateBroadcaster
{
    public void Broadcast()
        => WeakReferenceMessenger.Default.Send(new EconomyStateMutatedMessage());

    public void BroadcastEconomy(int currentSp, int shieldsAvailable)
        => WeakReferenceMessenger.Default.Send(new EconomyStateMutatedMessage(currentSp, shieldsAvailable));
}
