using FluentAssertions;
using LifeGrid.Application.Onboarding;
using LifeGrid.Application.Onboarding.Commands;
using LifeGrid.Domain.Onboarding;
using NSubstitute;

namespace LifeGrid.Application.Tests.Onboarding;

public sealed class StartNewGoalSessionCommandTests
{
    private readonly IOnboardingRepository _repository = Substitute.For<IOnboardingRepository>();
    private readonly StartNewGoalSessionCommandHandler _handler;

    public StartNewGoalSessionCommandTests()
        => _handler = new StartNewGoalSessionCommandHandler(_repository);

    [Fact]
    public async Task Handle_CreatesAndUpsertsFreshSession()
    {
        OnboardingSession? saved = null;
        await _repository.UpsertAsync(
            Arg.Do<OnboardingSession>(s => saved = s),
            Arg.Any<CancellationToken>());

        var result = await _handler.Handle(new StartNewGoalSessionCommand(), default);

        result.IsSuccess.Should().BeTrue();
        saved.Should().NotBeNull();
        saved!.CurrentStep.Should().Be(OnboardingStep.Unstarted);
    }

    [Fact]
    public async Task Handle_UpsertCalledExactlyOnce()
    {
        await _handler.Handle(new StartNewGoalSessionCommand(), default);

        await _repository.Received(1)
            .UpsertAsync(Arg.Any<OnboardingSession>(), Arg.Any<CancellationToken>());
    }
}
