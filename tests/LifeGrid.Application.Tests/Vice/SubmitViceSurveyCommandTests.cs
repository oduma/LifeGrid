using FluentAssertions;
using LifeGrid.Application.Common;
using LifeGrid.Application.Goal;
using LifeGrid.Application.UserProfile;
using LifeGrid.Application.Vice;
using LifeGrid.Domain.Common;
using NSubstitute;
using GoalAggregate     = LifeGrid.Domain.Goal.Goal;
using UserProfileEntity = LifeGrid.Domain.UserProfile.UserProfile;

namespace LifeGrid.Application.Tests.Vice;

public sealed class SubmitViceSurveyCommandTests
{
    private readonly IUserProfileRepository   _userProfiles     = Substitute.For<IUserProfileRepository>();
    private readonly IGoalRepository          _goals            = Substitute.For<IGoalRepository>();
    private readonly IGeminiViceSurveyService _viceSurveyService = Substitute.For<IGeminiViceSurveyService>();
    private readonly IUnitOfWork              _uow              = Substitute.For<IUnitOfWork>();
    private readonly SubmitViceSurveyCommandHandler _handler;

    public SubmitViceSurveyCommandTests()
        => _handler = new SubmitViceSurveyCommandHandler(
            _userProfiles, _goals, _viceSurveyService, _uow);

    private static SubmitViceSurveyCommand SampleCommand() =>
        new(new List<SurveyAnswerDto>
        {
            new("q1", "Scroll social media"),
            new("q2", "Watch TV before bed")
        });

    private void ArrangeHappyPath(
        UserProfileEntity profile,
        GoalAggregate     goal,
        IReadOnlyList<DetectedViceDto> vices)
    {
        _userProfiles.GetSingleAsync(Arg.Any<CancellationToken>()).Returns(profile);
        _goals.GetAllByUserIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
              .Returns(new List<GoalAggregate> { goal });
        _viceSurveyService.AnalyzeAnswersAsync(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<DetectedViceDto>>.Success(vices));
    }

    // ── guard tests ────────────────────────────────────────────────────────

    [Fact]
    public async Task NoProfile_ReturnsFailure()
    {
        _userProfiles.GetSingleAsync(Arg.Any<CancellationToken>()).Returns((UserProfileEntity?)null);

        var result = await _handler.Handle(SampleCommand(), default);

        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task AlreadyCompleted_ReturnsFailure_NoGeminiCall()
    {
        var profile = UserProfileEntity.Create();
        profile.GrantSurveyBonusShield(); // sets IsViceSurveyCompleted = true
        _userProfiles.GetSingleAsync(Arg.Any<CancellationToken>()).Returns(profile);

        var result = await _handler.Handle(SampleCommand(), default);

        result.IsSuccess.Should().BeFalse();
        await _viceSurveyService.DidNotReceive().AnalyzeAnswersAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    // ── Gemini failure ─────────────────────────────────────────────────────

    [Fact]
    public async Task GeminiFailure_NoCommit()
    {
        var profile = UserProfileEntity.Create();
        var goal    = GoalAggregate.Create(profile.UserId, "Run a marathon", "Fitness", "6 months",
                          DateTime.UtcNow.AddMonths(6), DateTime.Now);

        _userProfiles.GetSingleAsync(Arg.Any<CancellationToken>()).Returns(profile);
        _goals.GetAllByUserIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
              .Returns(new List<GoalAggregate> { goal });
        _viceSurveyService.AnalyzeAnswersAsync(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<DetectedViceDto>>.Failure("Gemini error."));

        var result = await _handler.Handle(SampleCommand(), default);

        result.IsSuccess.Should().BeFalse();
        await _uow.DidNotReceive().CommitAsync(Arg.Any<CancellationToken>());
    }

    // ── happy path ─────────────────────────────────────────────────────────

    [Fact]
    public async Task HappyPath_SetsLinkedBadHabitsOnMatchingGoal()
    {
        var profile = UserProfileEntity.Create();
        var goal    = GoalAggregate.Create(profile.UserId, "Run a marathon", "Fitness", "6 months",
                          DateTime.UtcNow.AddMonths(6), DateTime.Now);
        var vices   = new List<DetectedViceDto>
        {
            new("Late-night scrolling", 3, "Run a marathon"),
            new("Sugar cravings",       5, "Run a marathon")
        };
        ArrangeHappyPath(profile, goal, vices);

        await _handler.Handle(SampleCommand(), default);

        goal.LinkedBadHabits.Should().HaveCount(2);
        goal.LinkedBadHabits.Select(h => h.Description)
            .Should().Contain("Late-night scrolling").And.Contain("Sugar cravings");
    }

    [Fact]
    public async Task HappyPath_GrantsBonusShieldAndMarksComplete()
    {
        var profile = UserProfileEntity.Create();
        var goal    = GoalAggregate.Create(profile.UserId, "Run a marathon", "Fitness", "6 months",
                          DateTime.UtcNow.AddMonths(6), DateTime.Now);
        ArrangeHappyPath(profile, goal, new List<DetectedViceDto>
        {
            new("Doomscrolling", 2, "Run a marathon")
        });

        await _handler.Handle(SampleCommand(), default);

        profile.IsViceSurveyCompleted.Should().BeTrue();
        profile.Economy.MaxShieldCap.Should().Be(3);
        profile.Economy.ShieldsAvailable.Should().Be(1);
    }

    [Fact]
    public async Task HappyPath_CommitsOnce()
    {
        var profile = UserProfileEntity.Create();
        var goal    = GoalAggregate.Create(profile.UserId, "Run a marathon", "Fitness", "6 months",
                          DateTime.UtcNow.AddMonths(6), DateTime.Now);
        ArrangeHappyPath(profile, goal, new List<DetectedViceDto>
        {
            new("Snacking", 1, "Run a marathon")
        });

        await _handler.Handle(SampleCommand(), default);

        await _uow.Received(1).CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HappyPath_ReturnsAllDetectedVices()
    {
        var profile = UserProfileEntity.Create();
        var goal    = GoalAggregate.Create(profile.UserId, "Run a marathon", "Fitness", "6 months",
                          DateTime.UtcNow.AddMonths(6), DateTime.Now);
        var vices   = new List<DetectedViceDto>
        {
            new("Vice A", 2, "Run a marathon"),
            new("Vice B", 4, "Run a marathon")
        };
        ArrangeHappyPath(profile, goal, vices);

        var result = await _handler.Handle(SampleCommand(), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(2);
    }

    [Fact]
    public async Task ViceGoalDescriptionMismatch_GoalReceivesNoHabits()
    {
        var profile = UserProfileEntity.Create();
        var goal    = GoalAggregate.Create(profile.UserId, "Run a marathon", "Fitness", "6 months",
                          DateTime.UtcNow.AddMonths(6), DateTime.Now);
        // AI returns vice for a different goal description — no match
        ArrangeHappyPath(profile, goal, new List<DetectedViceDto>
        {
            new("Sugar cravings", 3, "Learn Spanish")
        });

        await _handler.Handle(SampleCommand(), default);

        goal.LinkedBadHabits.Should().BeEmpty();
    }
}
