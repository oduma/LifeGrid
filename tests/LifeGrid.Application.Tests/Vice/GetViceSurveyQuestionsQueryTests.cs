using FluentAssertions;
using LifeGrid.Application.Goal;
using LifeGrid.Application.UserProfile;
using LifeGrid.Application.Vice;
using LifeGrid.Domain.Common;
using NSubstitute;
using GoalAggregate     = LifeGrid.Domain.Goal.Goal;
using UserProfileEntity = LifeGrid.Domain.UserProfile.UserProfile;

namespace LifeGrid.Application.Tests.Vice;

public sealed class GetViceSurveyQuestionsQueryTests
{
    private readonly IUserProfileRepository  _userProfiles    = Substitute.For<IUserProfileRepository>();
    private readonly IGoalRepository         _goals           = Substitute.For<IGoalRepository>();
    private readonly IGeminiViceSurveyService _viceSurveyService = Substitute.For<IGeminiViceSurveyService>();
    private readonly GetViceSurveyQuestionsQueryHandler _handler;

    public GetViceSurveyQuestionsQueryTests()
        => _handler = new GetViceSurveyQuestionsQueryHandler(
            _userProfiles, _goals, _viceSurveyService);

    private static readonly IReadOnlyList<SurveyQuestionDto> SampleQuestions =
        new List<SurveyQuestionDto>
        {
            new("q1", "multiple_choice", "What is your stress response?",
                new[] { "Scroll social media", "Eat snacks" })
        };

    [Fact]
    public async Task CallsGeminiWithAllGoalDescriptions()
    {
        var profile = UserProfileEntity.Create();
        var goal1   = GoalAggregate.Create(profile.UserId, "Run a marathon",  "Fitness",  "6 months",  DateTime.UtcNow.AddMonths(6),  DateTime.Now);
        var goal2   = GoalAggregate.Create(profile.UserId, "Learn Spanish",   "Language", "12 months", DateTime.UtcNow.AddMonths(12), DateTime.Now);

        _userProfiles.GetSingleAsync(Arg.Any<CancellationToken>()).Returns(profile);
        _goals.GetAllByUserIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
              .Returns(new List<GoalAggregate> { goal1, goal2 });
        _viceSurveyService.GenerateQuestionsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<SurveyQuestionDto>>.Success(SampleQuestions));

        await _handler.Handle(new GetViceSurveyQuestionsQuery(), default);

        await _viceSurveyService.Received(1).GenerateQuestionsAsync(
            Arg.Is<string>(json => json.Contains("Run a marathon") && json.Contains("Learn Spanish")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GeminiFailure_PropagatesFailure()
    {
        var profile = UserProfileEntity.Create();
        _userProfiles.GetSingleAsync(Arg.Any<CancellationToken>()).Returns(profile);
        _goals.GetAllByUserIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
              .Returns(new List<GoalAggregate>());
        _viceSurveyService.GenerateQuestionsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<SurveyQuestionDto>>.Failure("Gemini rate limit."));

        var result = await _handler.Handle(new GetViceSurveyQuestionsQuery(), default);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("rate limit");
    }

    [Fact]
    public async Task NoProfile_PassesEmptyGoalsArrayToGemini()
    {
        _userProfiles.GetSingleAsync(Arg.Any<CancellationToken>()).Returns((UserProfileEntity?)null);
        _viceSurveyService.GenerateQuestionsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<SurveyQuestionDto>>.Success(SampleQuestions));

        await _handler.Handle(new GetViceSurveyQuestionsQuery(), default);

        await _viceSurveyService.Received(1).GenerateQuestionsAsync(
            Arg.Is<string>(json => json == "[]"),
            Arg.Any<CancellationToken>());
    }
}
