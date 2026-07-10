using FluentAssertions;
using LifeGrid.Application.Common;
using LifeGrid.Application.FlashQuest;
using LifeGrid.Application.Week;
using MediatR;
using NSubstitute;
using WeekEntity = LifeGrid.Domain.Week.Week;

namespace LifeGrid.Application.Tests.FlashQuest;

public sealed class FlashQuestTriggerServiceTests
{
    private readonly IWeekRepository   _weekRepo = Substitute.For<IWeekRepository>();
    private readonly ISender           _sender   = Substitute.For<ISender>();
    private readonly IDateTimeProvider _clock    = Substitute.For<IDateTimeProvider>();

    private FlashQuestTriggerService BuildService() => new(_weekRepo, _sender, _clock);

    [Fact]
    public async Task NotThursday_NoOp()
    {
        _clock.UtcNow.Returns(new DateTime(2026, 6, 23, 12, 0, 0, DateTimeKind.Utc)); // Tuesday

        await BuildService().EvaluateAsync();

        await _sender.DidNotReceive().Send(Arg.Any<GenerateFlashQuestCommand>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Thursday_ActiveWeekExists_SendsGenerateFlashQuestCommand()
    {
        _clock.UtcNow.Returns(new DateTime(2026, 6, 25, 12, 1, 0, DateTimeKind.Utc)); // Thursday 12:01 PM

        var currentMonday = new DateTime(2026, 6, 22, 0, 0, 0, DateTimeKind.Utc);
        var week           = WeekEntity.Create(1, currentMonday);
        _weekRepo.GetByStartDateAsync(currentMonday, Arg.Any<CancellationToken>()).Returns(week);

        await BuildService().EvaluateAsync();

        await _sender.Received(1).Send(
            Arg.Is<GenerateFlashQuestCommand>(c => c.WeekId == week.WeekId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Thursday_NoActiveWeek_NoOp()
    {
        _clock.UtcNow.Returns(new DateTime(2026, 6, 25, 12, 1, 0, DateTimeKind.Utc));
        _weekRepo.GetByStartDateAsync(Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
                 .Returns((WeekEntity?)null);

        await BuildService().EvaluateAsync();

        await _sender.DidNotReceive().Send(Arg.Any<GenerateFlashQuestCommand>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Thursday_ClosedWeek_NoOp()
    {
        _clock.UtcNow.Returns(new DateTime(2026, 6, 25, 12, 1, 0, DateTimeKind.Utc));
        var currentMonday = new DateTime(2026, 6, 22, 0, 0, 0, DateTimeKind.Utc);
        var week           = WeekEntity.Create(1, currentMonday);
        week.Close();
        _weekRepo.GetByStartDateAsync(currentMonday, Arg.Any<CancellationToken>()).Returns(week);

        await BuildService().EvaluateAsync();

        await _sender.DidNotReceive().Send(Arg.Any<GenerateFlashQuestCommand>(), Arg.Any<CancellationToken>());
    }
}
