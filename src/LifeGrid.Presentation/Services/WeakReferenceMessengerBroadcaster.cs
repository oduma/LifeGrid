using CommunityToolkit.Mvvm.Messaging;
using LifeGrid.Application.Gamification;

namespace LifeGrid.Presentation.Services;

internal sealed class WeakReferenceMessengerBroadcaster : IEconomyStateBroadcaster
{
    public void Broadcast()
        => WeakReferenceMessenger.Default.Send(new EconomyStateMutatedMessage());
}
