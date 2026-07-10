using FluentAssertions;
using LifeGrid.Application.FlashQuest;
using LifeGrid.Infrastructure.AI;
using Microsoft.Extensions.AI;
using NSubstitute;

namespace LifeGrid.Infrastructure.Tests.AI;

public sealed class GeminiFlashQuestServiceTests
{
    private readonly IChatClient _chatClient = Substitute.For<IChatClient>();
    private readonly GeminiFlashQuestService _service;

    public GeminiFlashQuestServiceTests()
        => _service = new GeminiFlashQuestService(_chatClient);

    private void ArrangeResponse(string text)
        => _chatClient
            .GetResponseAsync(
                Arg.Any<IEnumerable<ChatMessage>>(),
                Arg.Any<ChatOptions?>(),
                Arg.Any<CancellationToken>())
            .Returns(new ChatResponse(new ChatMessage(ChatRole.Assistant, text)));

    [Fact]
    public async Task ParsesAcceptedResponse_WithSourceHabitIds()
    {
        var habitId = Guid.NewGuid();
        var json = $$"""
            {
              "flash-quests": [
                {
                  "source_habit_id": "{{habitId}}",
                  "falsh_quest_name": "Quick Sprint",
                  "habit_description": "Sprint for 20 minutes",
                  "measure": { "value": 20, "unit": "minutes" }
                }
              ]
            }
            """;
        ArrangeResponse(json);

        var result = await _service.GenerateAsync("[]", new DateTime(2026, 6, 25));

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeOfType<FlashQuestGenerationResult.Accepted>();
        var accepted = (FlashQuestGenerationResult.Accepted)result.Value!;
        accepted.Quests.Should().HaveCount(1);
        accepted.Quests[0].SourceHabitId.Should().Be(habitId);
        accepted.Quests[0].QuestName.Should().Be("Quick Sprint");
        accepted.Quests[0].Description.Should().Be("Sprint for 20 minutes");
        accepted.Quests[0].MeasureValue.Should().Be(20);
        accepted.Quests[0].MeasureUnit.Should().Be("minutes");
    }

    [Fact]
    public async Task ParsesNotEligible_OnLiteralNA()
    {
        ArrangeResponse("N/A");

        var result = await _service.GenerateAsync("[]", new DateTime(2026, 6, 25));

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeOfType<FlashQuestGenerationResult.NotEligible>();
    }

    [Fact]
    public async Task MalformedJson_ReturnsFailure()
    {
        ArrangeResponse("not valid json {{{");

        var result = await _service.GenerateAsync("[]", new DateTime(2026, 6, 25));

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task RateLimit_ReturnsFailureWithExceptionMessage()
    {
        var ex = new HttpRequestException(
            "Gemini rate limit reached. Please wait 30 seconds and try again.",
            null,
            System.Net.HttpStatusCode.TooManyRequests);

        _chatClient
            .GetResponseAsync(
                Arg.Any<IEnumerable<ChatMessage>>(),
                Arg.Any<ChatOptions?>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromException<ChatResponse>(ex));

        var result = await _service.GenerateAsync("[]", new DateTime(2026, 6, 25));

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("rate limit");
    }
}
