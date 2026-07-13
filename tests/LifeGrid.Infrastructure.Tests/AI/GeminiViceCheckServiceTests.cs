using FluentAssertions;
using LifeGrid.Infrastructure.AI;
using Microsoft.Extensions.AI;
using NSubstitute;

namespace LifeGrid.Infrastructure.Tests.AI;

public sealed class GeminiViceCheckServiceTests
{
    private readonly IChatClient _chatClient = Substitute.For<IChatClient>();
    private readonly GeminiViceCheckService _service;

    public GeminiViceCheckServiceTests()
        => _service = new GeminiViceCheckService(_chatClient);

    private void ArrangeResponse(string text)
        => _chatClient
            .GetResponseAsync(
                Arg.Any<IEnumerable<ChatMessage>>(),
                Arg.Any<ChatOptions?>(),
                Arg.Any<CancellationToken>())
            .Returns(new ChatResponse(new ChatMessage(ChatRole.Assistant, text)));

    // ── GenerateQuestionAsync ──────────────────────────────────────────────────

    [Fact]
    public async Task GenerateQuestion_ExtractsAmbientQuestion()
    {
        var json = """
            {
              "selected_goal_description": "Get fit",
              "selected_bad_habit": "Late-night snacking",
              "danger_level": 5,
              "ambient_question": "How did your evening routine go?"
            }
            """;
        ArrangeResponse(json);

        var result = await _service.GenerateQuestionAsync("[]");

        result.IsSuccess.Should().BeTrue();
        result.Value!.Question.Should().Be("How did your evening routine go?");
    }

    [Fact]
    public async Task GenerateQuestion_MalformedJson_ReturnsFailure()
    {
        ArrangeResponse("not valid json {{{");

        var result = await _service.GenerateQuestionAsync("[]");

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().NotBeNullOrEmpty();
    }

    // ── EvaluateAnswerAsync ────────────────────────────────────────────────────

    [Fact]
    public async Task EvaluateAnswer_ExtractsPersistsTrue()
    {
        ArrangeResponse("""{ "persists": true, "analysis_reasoning": "Partial admission." }""");

        var result = await _service.EvaluateAnswerAsync("{}");

        result.IsSuccess.Should().BeTrue();
        result.Value!.Persists.Should().BeTrue();
    }

    [Fact]
    public async Task EvaluateAnswer_ExtractsPersistsFalse()
    {
        ArrangeResponse("""{ "persists": false, "analysis_reasoning": "Clear avoidance." }""");

        var result = await _service.EvaluateAnswerAsync("{}");

        result.IsSuccess.Should().BeTrue();
        result.Value!.Persists.Should().BeFalse();
    }

    [Fact]
    public async Task EvaluateAnswer_MalformedJson_ReturnsFailure()
    {
        ArrangeResponse("not valid json {{{");

        var result = await _service.EvaluateAnswerAsync("{}");

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().NotBeNullOrEmpty();
    }
}
