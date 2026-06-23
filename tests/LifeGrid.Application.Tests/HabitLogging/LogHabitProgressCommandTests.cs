using FluentAssertions;
using LifeGrid.Application.Common;
using LifeGrid.Application.Habit;
using LifeGrid.Application.HabitLogging;
using NSubstitute;
using CompletedValueLog = LifeGrid.Domain.Habit.CompletedValueLog;
using HabitEntity = LifeGrid.Domain.Habit.Habit;

namespace LifeGrid.Application.Tests.HabitLogging;

public sealed class LogHabitProgressCommandTests
{
    private readonly IHabitRepository  _habitRepo  = Substitute.For<IHabitRepository>();
    private readonly IDateTimeProvider _clock      = Substitute.For<IDateTimeProvider>();
    private readonly IUnitOfWork       _uow        = Substitute.For<IUnitOfWork>();
    private readonly LogHabitProgressCommandHandler _handler;

    private static readonly DateTime FixedNow =
        new(2026, 6, 23, 10, 30, 0, DateTimeKind.Utc);

    public LogHabitProgressCommandTests()
    {
        _clock.UtcNow.Returns(FixedNow);
        _handler = new LogHabitProgressCommandHandler(_habitRepo, _clock, _uow);
    }

    // ── validation ────────────────────────────────────────────────────────────

    [Fact]
    public async Task ValueZero_ReturnsFailure()
    {
        var result = await _handler.Handle(
            new LogHabitProgressCommand(Guid.NewGuid(), 0, "km", null, null), default);

        result.IsSuccess.Should().BeFalse();
        await _uow.DidNotReceive().CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task NegativeValue_ReturnsFailure()
    {
        var result = await _handler.Handle(
            new LogHabitProgressCommand(Guid.NewGuid(), -1.5, "km", null, null), default);

        result.IsSuccess.Should().BeFalse();
        await _uow.DidNotReceive().CommitAsync(Arg.Any<CancellationToken>());
    }

    // ── habit not found ───────────────────────────────────────────────────────

    [Fact]
    public async Task HabitNotFound_ReturnsFailure()
    {
        _habitRepo.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
                  .Returns((HabitEntity?)null);

        var result = await _handler.Handle(
            new LogHabitProgressCommand(Guid.NewGuid(), 5.0, "km", null, null), default);

        result.IsSuccess.Should().BeFalse();
        await _uow.DidNotReceive().CommitAsync(Arg.Any<CancellationToken>());
    }

    // ── happy path ────────────────────────────────────────────────────────────

    [Fact]
    public async Task HappyPath_AddsLogAndCommits()
    {
        var habitId = Guid.NewGuid();
        var habit   = HabitEntity.Create(
            Guid.NewGuid(),
            LifeGrid.Domain.Habit.HabitType.Planned,
            "Run 5k", "Run five kilometres",
            5.0, "km",
            new DateTime(2026, 6, 27, 0, 0, 0, DateTimeKind.Utc));

        _habitRepo.GetByIdAsync(habitId, Arg.Any<CancellationToken>()).Returns(habit);

        var result = await _handler.Handle(
            new LogHabitProgressCommand(habitId, 5.0, "km", "Felt great!", null), default);

        result.IsSuccess.Should().BeTrue();
        await _habitRepo.Received(1)
            .AddCompletionLogAsync(
                Arg.Is<CompletedValueLog>(l => l.HabitId == habitId),
                Arg.Any<CancellationToken>());
        await _uow.Received(1).CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HappyPath_UsesDateTimeProviderTimestamp()
    {
        var habitId = Guid.NewGuid();
        var habit   = HabitEntity.Create(
            Guid.NewGuid(),
            LifeGrid.Domain.Habit.HabitType.Planned,
            "Meditate", "10 minutes of meditation",
            10.0, "min",
            new DateTime(2026, 6, 27, 0, 0, 0, DateTimeKind.Utc));

        _habitRepo.GetByIdAsync(habitId, Arg.Any<CancellationToken>()).Returns(habit);

        await _handler.Handle(
            new LogHabitProgressCommand(habitId, 10.0, "min", null, null), default);

        await _habitRepo.Received(1)
            .AddCompletionLogAsync(
                Arg.Is<CompletedValueLog>(l => l.Timestamp == FixedNow),
                Arg.Any<CancellationToken>());
    }
}
