using FluentAssertions;
using LifeGrid.Application.Onboarding;
using LifeGrid.Application.Onboarding.Queries;
using LifeGrid.Domain.Onboarding;
using NSubstitute;

namespace LifeGrid.Application.Tests.Onboarding;

public sealed class GetOrCreateOnboardingSessionQueryHandlerTests
{
    private readonly IOnboardingRepository _repository = Substitute.For<IOnboardingRepository>();
    private readonly GetOrCreateOnboardingSessionQueryHandler _handler;

    public GetOrCreateOnboardingSessionQueryHandlerTests()
        => _handler = new GetOrCreateOnboardingSessionQueryHandler(_repository);

    [Fact]
    public async Task NoExistingSession_CreatesAndReturnsNew()
    {
        _repository.GetActiveSessionAsync(Arg.Any<CancellationToken>())
                   .Returns((OnboardingSession?)null);
        _repository.UpsertAsync(Arg.Any<OnboardingSession>(), Arg.Any<CancellationToken>())
                   .Returns(x => x.Arg<OnboardingSession>());

        var result = await _handler.Handle(new GetOrCreateOnboardingSessionQuery(), default);

        result.IsSuccess.Should().BeTrue();
        result.Value!.CurrentStep.Should().Be(OnboardingStep.Unstarted);
        await _repository.Received(1).UpsertAsync(Arg.Any<OnboardingSession>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExistingSession_ReturnsExistingWithoutUpsert()
    {
        var existing = OnboardingSession.Create();
        _repository.GetActiveSessionAsync(Arg.Any<CancellationToken>())
                   .Returns(existing);

        var result = await _handler.Handle(new GetOrCreateOnboardingSessionQuery(), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeSameAs(existing);
        await _repository.DidNotReceive().UpsertAsync(Arg.Any<OnboardingSession>(), Arg.Any<CancellationToken>());
    }
}
