using FluentAssertions;
using LifeGrid.Application.Goal;
using LifeGrid.Application.UserProfile;
using LifeGrid.Application.Week;
using NSubstitute;
using GoalAggregate     = LifeGrid.Domain.Goal.Goal;
using UserProfileEntity = LifeGrid.Domain.UserProfile.UserProfile;

namespace LifeGrid.Application.Tests.Goal;

public sealed class GetGoalsQueryHandlerTests
{
    private readonly IUserProfileRepository _profileRepo = Substitute.For<IUserProfileRepository>();
    private readonly IGoalRepository        _goalRepo    = Substitute.For<IGoalRepository>();
    private readonly IWeekRepository        _weekRepo    = Substitute.For<IWeekRepository>();
    private readonly GetGoalsQueryHandler   _handler;

    public GetGoalsQueryHandlerTests()
        => _handler = new GetGoalsQueryHandler(_profileRepo, _goalRepo, _weekRepo);

    [Fact]
    public async Task NoProfile_ReturnsEmptyList()
    {
        _profileRepo.GetSingleAsync(Arg.Any<CancellationToken>()).Returns((UserProfileEntity?)null);

        var result = await _handler.Handle(new GetGoalsQuery(), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
        await _goalRepo.DidNotReceive().GetAllByUserIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task NoGoals_ReturnsEmptyList()
    {
        var profile = UserProfileEntity.Create();
        _profileRepo.GetSingleAsync(Arg.Any<CancellationToken>()).Returns(profile);
        _goalRepo.GetAllByUserIdAsync(profile.UserId, Arg.Any<CancellationToken>())
            .Returns(Array.Empty<GoalAggregate>());

        var result = await _handler.Handle(new GetGoalsQuery(), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }

    [Fact]
    public async Task WithGoals_MapsTotalWeeks()
    {
        var profile = UserProfileEntity.Create();
        var goal    = GoalAggregate.Create(profile.UserId, "Run a marathon", "#Fitness", "6 months",
            new DateTime(2027, 1, 1), GoalAggregate.CalculateStartDate(DateTime.Now), DateTime.Now);

        _profileRepo.GetSingleAsync(Arg.Any<CancellationToken>()).Returns(profile);
        _goalRepo.GetAllByUserIdAsync(profile.UserId, Arg.Any<CancellationToken>())
            .Returns(new[] { goal });
        _weekRepo.GetWeekGoalCountByGoalIdAsync(goal.GoalId, Arg.Any<CancellationToken>())
            .Returns(12);

        var result = await _handler.Handle(new GetGoalsQuery(), default);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Should().HaveCount(1);
        result.Value![0].TotalWeeks.Should().Be(12);
    }

    [Fact]
    public async Task WithGoals_MapsAllDtoFields()
    {
        var profile  = UserProfileEntity.Create();
        var deadline = new DateTime(2027, 6, 1);
        var goal     = GoalAggregate.Create(profile.UserId, "Learn Spanish", "#Language", "12 months",
            deadline, GoalAggregate.CalculateStartDate(DateTime.Now), DateTime.Now);

        _profileRepo.GetSingleAsync(Arg.Any<CancellationToken>()).Returns(profile);
        _goalRepo.GetAllByUserIdAsync(profile.UserId, Arg.Any<CancellationToken>())
            .Returns(new[] { goal });
        _weekRepo.GetWeekGoalCountByGoalIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(0);

        var result = await _handler.Handle(new GetGoalsQuery(), default);

        var dto = result.Value![0];
        dto.Description.Should().Be("Learn Spanish");
        dto.AmbientTag.Should().Be("#Language");
        dto.Duration.Should().Be("12 months");
        dto.DeadlineDate.Should().Be(deadline);
        dto.Status.Should().Be("Active");
    }
}
