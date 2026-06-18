using FluentAssertions;
using LifeGrid.Application.Onboarding;
using LifeGrid.Application.Onboarding.Commands;
using LifeGrid.Domain.Onboarding;
using NSubstitute;

namespace LifeGrid.Application.Tests.Onboarding;

public sealed class SaveRefinementAnswersCommandTests
{
    private readonly IOnboardingRepository _repository = Substitute.For<IOnboardingRepository>();
    private readonly SaveRefinementAnswersCommandHandler _handler;

    public SaveRefinementAnswersCommandTests()
        => _handler = new SaveRefinementAnswersCommandHandler(_repository);

    // ── guard tests ───────────────────────────────────────────────────────────

    [Fact]
    public async Task NoActiveSession_ReturnsFailure()
    {
        _repository.GetActiveSessionAsync(Arg.Any<CancellationToken>()).Returns((OnboardingSession?)null);

        var result = await _handler.Handle(new SaveRefinementAnswersCommand("[]"), default);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task SessionNotAtRefinementQuestionsActive_ReturnsFailure()
    {
        var session = OnboardingSession.Create(); // Unstarted
        _repository.GetActiveSessionAsync(Arg.Any<CancellationToken>()).Returns(session);

        var result = await _handler.Handle(new SaveRefinementAnswersCommand("[]"), default);

        result.IsSuccess.Should().BeFalse();
        await _repository.DidNotReceive().UpsertAsync(
            Arg.Any<OnboardingSession>(), Arg.Any<CancellationToken>());
    }

    // ── happy path ────────────────────────────────────────────────────────────

    [Fact]
    public async Task HappyPath_CallsUpsertWithUpdatedAnswers()
    {
        var session = SessionAtRefinementActive();
        _repository.GetActiveSessionAsync(Arg.Any<CancellationToken>()).Returns(session);
        _repository.UpsertAsync(Arg.Any<OnboardingSession>(), Arg.Any<CancellationToken>())
                   .Returns(x => x.Arg<OnboardingSession>());

        var json = "[{\"rankOrder\":1,\"answer\":\"5k comfortable\"}]";
        await _handler.Handle(new SaveRefinementAnswersCommand(json), default);

        await _repository.Received(1).UpsertAsync(
            Arg.Is<OnboardingSession>(s => s.RefinementAnswersJson == json),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HappyPath_ReturnsSuccess()
    {
        var session = SessionAtRefinementActive();
        _repository.GetActiveSessionAsync(Arg.Any<CancellationToken>()).Returns(session);
        _repository.UpsertAsync(Arg.Any<OnboardingSession>(), Arg.Any<CancellationToken>())
                   .Returns(x => x.Arg<OnboardingSession>());

        var result = await _handler.Handle(new SaveRefinementAnswersCommand("[]"), default);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task HappyPath_DoesNotChangeCurrentStep()
    {
        var session = SessionAtRefinementActive();
        _repository.GetActiveSessionAsync(Arg.Any<CancellationToken>()).Returns(session);
        _repository.UpsertAsync(Arg.Any<OnboardingSession>(), Arg.Any<CancellationToken>())
                   .Returns(x => x.Arg<OnboardingSession>());

        await _handler.Handle(new SaveRefinementAnswersCommand("[]"), default);

        session.CurrentStep.Should().Be(OnboardingStep.Step1_RefinementQuestionsActive);
    }

    // ── helper ────────────────────────────────────────────────────────────────

    private static OnboardingSession SessionAtRefinementActive()
    {
        var s = OnboardingSession.Create();
        s.UpdateDraft("Run a marathon");
        s.AdvanceToStep1();
        s.AdvanceToRefinementQuestionsActive(
            "{\"description\":\"Run\"}",
            "[{\"rankOrder\":1,\"question\":\"Baseline?\"}]");
        return s;
    }
}
