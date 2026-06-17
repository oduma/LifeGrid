using FluentAssertions;
using LifeGrid.Application.Onboarding;
using LifeGrid.Application.Onboarding.Commands;
using LifeGrid.Domain.Onboarding;
using NSubstitute;

namespace LifeGrid.Application.Tests.Onboarding;

public sealed class CompleteStep1CommandHandlerTests
{
    private readonly IOnboardingRepository _repository = Substitute.For<IOnboardingRepository>();
    private readonly CompleteStep1CommandHandler _handler;

    public CompleteStep1CommandHandlerTests()
        => _handler = new CompleteStep1CommandHandler(_repository);

    [Fact]
    public async Task NoSession_ReturnsFailure()
    {
        _repository.GetActiveSessionAsync(Arg.Any<CancellationToken>())
                   .Returns((OnboardingSession?)null);

        var result = await _handler.Handle(new CompleteStep1Command(), default);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().NotBeNullOrEmpty();
        await _repository.DidNotReceive().UpsertAsync(Arg.Any<OnboardingSession>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExistingSession_AdvancesStepAndReturnsSession()
    {
        var session = OnboardingSession.Create();
        _repository.GetActiveSessionAsync(Arg.Any<CancellationToken>())
                   .Returns(session);
        _repository.UpsertAsync(Arg.Any<OnboardingSession>(), Arg.Any<CancellationToken>())
                   .Returns(x => x.Arg<OnboardingSession>());

        var result = await _handler.Handle(new CompleteStep1Command(), default);

        result.IsSuccess.Should().BeTrue();
        result.Value!.CurrentStep.Should().Be(OnboardingStep.Step1_GoalDraftCaptured);
        result.Value.IsComplete.Should().BeFalse();
        await _repository.Received(1).UpsertAsync(session, Arg.Any<CancellationToken>());
    }
}
