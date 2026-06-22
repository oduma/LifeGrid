using FluentAssertions;
using LifeGrid.Application.Week;
using LifeGrid.Infrastructure.AI;
using Microsoft.Extensions.AI;
using NSubstitute;

namespace LifeGrid.Infrastructure.Tests.AI;

public sealed class GeminiHabitGenerationServiceTests
{
    private readonly IChatClient _chatClient = Substitute.For<IChatClient>();
    private readonly GeminiHabitGenerationService _service;

    public GeminiHabitGenerationServiceTests()
        => _service = new GeminiHabitGenerationService(_chatClient);

    // ── helpers ───────────────────────────────────────────────────────────────

    private void ArrangeSequentialResponses(string first, string second)
    {
        var call = 0;
        _chatClient
            .GetResponseAsync(
                Arg.Any<IEnumerable<ChatMessage>>(),
                Arg.Any<ChatOptions?>(),
                Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                var text = ++call == 1 ? first : second;
                return new ChatResponse(new ChatMessage(ChatRole.Assistant, text));
            });
    }

    private void ArrangeFirstCallThrows(Exception ex)
    {
        _chatClient
            .GetResponseAsync(
                Arg.Any<IEnumerable<ChatMessage>>(),
                Arg.Any<ChatOptions?>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromException<ChatResponse>(ex));
    }

    private static readonly string FeasibleBlueprint = """
        {
          "isFeasible": true,
          "total_weeks": 1,
          "goal_summary": "Run a marathon"
        }
        """;

    private static readonly string WeekScheduleJson = """
        {
          "weeks": [
            {
              "week_number": 1,
              "start_date": "2026-06-16",
              "habits": [
                {
                  "description": "Run 3 km",
                  "measurement": { "value": 3.0, "unit": "km" }
                }
              ]
            }
          ]
        }
        """;

    // ── happy path ────────────────────────────────────────────────────────────

    [Fact]
    public async Task HappyPath_ReturnsFeasibleWithWeeks()
    {
        ArrangeSequentialResponses(FeasibleBlueprint, WeekScheduleJson);

        var result = await _service.GenerateScheduleAsync(
            "Run a marathon", "2026-12-10", "[]", new DateTime(2026, 6, 22));

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeOfType<HabitSchedulingResult.Feasible>();
        var feasible = (HabitSchedulingResult.Feasible)result.Value!;
        feasible.Schedule.Should().HaveCount(1);
        feasible.Schedule[0].WeekNumber.Should().Be(1);
        feasible.Schedule[0].Habits.Should().HaveCount(1);
        feasible.Schedule[0].Habits[0].Description.Should().Be("Run 3 km");
        feasible.Schedule[0].Habits[0].Value.Should().Be(3.0);
        feasible.Schedule[0].Habits[0].Unit.Should().Be("km");
    }

    // ── infeasibility path ─────────────────────────────────────────────────────

    [Fact]
    public async Task InfeasibleBlueprint_ReturnsInfeasibleWithoutCall2()
    {
        var infeasibleBlueprint = """
            {
              "isFeasible": false,
              "recalibration_reason": "Timeline too aggressive for a beginner.",
              "recommended_recalibration": {
                "suggested_deadline": "2027-06-01",
                "suggested_alternative_scope": "Run a 10k instead"
              }
            }
            """;

        _chatClient
            .GetResponseAsync(
                Arg.Any<IEnumerable<ChatMessage>>(),
                Arg.Any<ChatOptions?>(),
                Arg.Any<CancellationToken>())
            .Returns(new ChatResponse(new ChatMessage(ChatRole.Assistant, infeasibleBlueprint)));

        var result = await _service.GenerateScheduleAsync(
            "Run a marathon", "2026-06-30", "[]", new DateTime(2026, 6, 22));

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeOfType<HabitSchedulingResult.Infeasible>();
        var infeasible = (HabitSchedulingResult.Infeasible)result.Value!;
        infeasible.RecalibrationReason.Should().ContainAny("Too aggressive", "aggressive", "Timeline");
        infeasible.SuggestedDeadline.Should().Be("2027-06-01");
        infeasible.SuggestedAlternativeScope.Should().Be("Run a 10k instead");

        // Call 2 must NOT have been made
        await _chatClient.Received(1).GetResponseAsync(
            Arg.Any<IEnumerable<ChatMessage>>(),
            Arg.Any<ChatOptions?>(),
            Arg.Any<CancellationToken>());
    }

    // ── chaining test ─────────────────────────────────────────────────────────

    [Fact]
    public async Task Call2ReceivesCall1RawJson_UnmodifiedSubstitution()
    {
        IEnumerable<ChatMessage>? secondCallMessages = null;

        var call = 0;
        _chatClient
            .GetResponseAsync(
                Arg.Any<IEnumerable<ChatMessage>>(),
                Arg.Any<ChatOptions?>(),
                Arg.Any<CancellationToken>())
            .Returns(ci =>
            {
                ++call;
                if (call == 2)
                    secondCallMessages = ci.Arg<IEnumerable<ChatMessage>>();
                var text = call == 1 ? FeasibleBlueprint : WeekScheduleJson;
                return new ChatResponse(new ChatMessage(ChatRole.Assistant, text));
            });

        await _service.GenerateScheduleAsync("goal", "2026-12-10", "[]", new DateTime(2026, 6, 22));

        secondCallMessages.Should().NotBeNull();
        var promptText = secondCallMessages!.Single().Text;
        // The raw Call 1 response must appear verbatim in Call 2's prompt
        promptText.Should().Contain(FeasibleBlueprint.Trim()[..20]);
    }

    // ── malformed JSON ────────────────────────────────────────────────────────

    [Fact]
    public async Task MalformedCall1Json_ReturnsFailure()
    {
        _chatClient
            .GetResponseAsync(
                Arg.Any<IEnumerable<ChatMessage>>(),
                Arg.Any<ChatOptions?>(),
                Arg.Any<CancellationToken>())
            .Returns(new ChatResponse(new ChatMessage(ChatRole.Assistant, "not valid json {{{}")));

        var result = await _service.GenerateScheduleAsync("goal", "2026-12-10", "[]", new DateTime(2026, 6, 22));

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task MalformedCall2Json_ReturnsFailure()
    {
        ArrangeSequentialResponses(FeasibleBlueprint, "not valid json {{{");

        var result = await _service.GenerateScheduleAsync("goal", "2026-12-10", "[]", new DateTime(2026, 6, 22));

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().NotBeNullOrEmpty();
    }

    // ── rate limit ────────────────────────────────────────────────────────────

    [Fact]
    public async Task RateLimit_ReturnsFailureWithExceptionMessage()
    {
        var ex = new HttpRequestException(
            "Gemini rate limit reached. Please wait 30 seconds and try again.",
            null,
            System.Net.HttpStatusCode.TooManyRequests);

        ArrangeFirstCallThrows(ex);

        var result = await _service.GenerateScheduleAsync("goal", "2026-12-10", "[]", new DateTime(2026, 6, 22));

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("rate limit");
    }
}
