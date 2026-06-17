using FluentAssertions;
using LifeGrid.Application.Onboarding;
using LifeGrid.Application.Onboarding.Commands;
using LifeGrid.Domain.Onboarding;
using NSubstitute;

namespace LifeGrid.Application.Tests.Onboarding;

public sealed class UpdateGoalDraftCommandHandlerTests
{
    private readonly IOnboardingRepository _repository = Substitute.For<IOnboardingRepository>();
    private readonly UpdateGoalDraftCommandHandler _handler;

    public UpdateGoalDraftCommandHandlerTests()
        => _handler = new UpdateGoalDraftCommandHandler(_repository);

    [Fact]
    public async Task NoSession_ReturnsFailure()
    {
        _repository.GetActiveSessionAsync(Arg.Any<CancellationToken>())
                   .Returns((OnboardingSession?)null);

        var result = await _handler.Handle(new UpdateGoalDraftCommand("test"), default);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().NotBeNullOrEmpty();
        await _repository.DidNotReceive().UpsertAsync(Arg.Any<OnboardingSession>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExistingSession_SavesDraftAndReturnsSuccess()
    {
        var session = OnboardingSession.Create();
        _repository.GetActiveSessionAsync(Arg.Any<CancellationToken>())
                   .Returns(session);
        _repository.UpsertAsync(Arg.Any<OnboardingSession>(), Arg.Any<CancellationToken>())
                   .Returns(x => x.Arg<OnboardingSession>());

        var result = await _handler.Handle(new UpdateGoalDraftCommand("run a marathon"), default);

        result.IsSuccess.Should().BeTrue();
        session.RawGoalDraft.Should().Be("run a marathon");
        await _repository.Received(1).UpsertAsync(session, Arg.Any<CancellationToken>());
    }
}
