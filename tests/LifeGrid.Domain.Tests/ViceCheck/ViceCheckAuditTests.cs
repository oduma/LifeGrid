using FluentAssertions;
using LifeGrid.Domain.ViceCheck;

namespace LifeGrid.Domain.Tests.ViceCheck;

public sealed class ViceCheckAuditTests
{
    private static readonly DateTime CreatedAt = new(2026, 6, 25, 12, 0, 0, DateTimeKind.Utc);

    private static ViceCheckAudit BuildAudit() => ViceCheckAudit.Create(
        Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
        "Get fit", "Late-night snacking", 5,
        "How did your evening routine go?", CreatedAt);

    [Fact]
    public void Create_SetsStatusToPending()
    {
        var audit = BuildAudit();

        audit.Status.Should().Be(ViceCheckStatus.Pending);
        audit.CreatedAt.Should().Be(CreatedAt);
        audit.Answer.Should().BeNull();
        audit.ResolvedAt.Should().BeNull();
        audit.PenaltyPercentApplied.Should().BeNull();
    }

    [Fact]
    public void MarkPassed_SetsStatusAndAnswer()
    {
        var audit      = BuildAudit();
        var resolvedAt = CreatedAt.AddHours(1);

        audit.MarkPassed("I went straight to bed instead.", resolvedAt);

        audit.Status.Should().Be(ViceCheckStatus.Passed);
        audit.Answer.Should().Be("I went straight to bed instead.");
        audit.ResolvedAt.Should().Be(resolvedAt);
        audit.PenaltyPercentApplied.Should().BeNull();
    }

    [Fact]
    public void MarkFailed_SetsStatusAnswerAndPenaltyPercent()
    {
        var audit      = BuildAudit();
        var resolvedAt = CreatedAt.AddHours(1);

        audit.MarkFailed("I only did it for a little bit.", 5.0, resolvedAt);

        audit.Status.Should().Be(ViceCheckStatus.Failed);
        audit.Answer.Should().Be("I only did it for a little bit.");
        audit.ResolvedAt.Should().Be(resolvedAt);
        audit.PenaltyPercentApplied.Should().Be(5.0);
    }
}
