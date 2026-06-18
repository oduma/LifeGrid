using FluentAssertions;
using LifeGrid.Application.Goal;
using LifeGrid.Infrastructure.AI;
using Microsoft.Extensions.AI;
using NSubstitute;

namespace LifeGrid.Infrastructure.Tests.AI;

public sealed class GeminiGoalValidationServiceTests
{
    private readonly IChatClient _chatClient = Substitute.For<IChatClient>();
    private readonly GeminiGoalValidationService _service;

    public GeminiGoalValidationServiceTests()
        => _service = new GeminiGoalValidationService(_chatClient);

    private void ArrangeResponse(string text)
    {
        _chatClient
            .GetResponseAsync(
                Arg.Any<IEnumerable<ChatMessage>>(),
                Arg.Any<ChatOptions?>(),
                Arg.Any<CancellationToken>())
            .Returns(new ChatResponse(new ChatMessage(ChatRole.Assistant, text)));
    }

    // ── ValidateGoalAsync ─────────────────────────────────────────────────

    [Fact]
    public async Task ValidateGoal_ValidJson_ReturnsValidResult()
    {
        var json = """
            {
              "isValid": true,
              "goal": {
                "description": "Run a marathon",
                "duration": "6 months",
                "deadline_date": "2026-12-10 00:00:00",
                "ambient_tag": "Physical"
              }
            }
            """;
        ArrangeResponse(json);

        var result = await _service.ValidateGoalAsync("Run a marathon in 6 months");

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeOfType<GeminiValidationResult.Valid>();
        var valid = (GeminiValidationResult.Valid)result.Value!;
        valid.Data.Description.Should().Be("Run a marathon");
        valid.Data.AmbientTag.Should().Be("Physical");
    }

    [Fact]
    public async Task ValidateGoal_InvalidGoal_ReturnsInvalidWithRetryPrompt()
    {
        var json = """
            {
              "isValid": false,
              "retry_prompt": "Please provide a specific deadline."
            }
            """;
        ArrangeResponse(json);

        var result = await _service.ValidateGoalAsync("I want to be fit");

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeOfType<GeminiValidationResult.Invalid>();
        var invalid = (GeminiValidationResult.Invalid)result.Value!;
        invalid.RetryPrompt.Should().Be("Please provide a specific deadline.");
    }

    [Fact]
    public async Task ValidateGoal_MalformedJson_ReturnsFailureWithoutThrowing()
    {
        ArrangeResponse("not valid json {{{");

        var result = await _service.ValidateGoalAsync("some goal");

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task ValidateGoal_MarkdownWrappedJson_StripsFencesAndParses()
    {
        var json = """
            ```json
            {
              "isValid": false,
              "retry_prompt": "Add a timeline."
            }
            ```
            """;
        ArrangeResponse(json);

        var result = await _service.ValidateGoalAsync("Learn something");

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeOfType<GeminiValidationResult.Invalid>();
    }

    [Fact]
    public async Task ValidateGoal_MissingIsValidField_ReturnsFailure()
    {
        ArrangeResponse("""{ "goal": {} }""");

        var result = await _service.ValidateGoalAsync("some goal");

        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task ValidateGoal_HttpException_ReturnsFailure()
    {
        _chatClient
            .GetResponseAsync(
                Arg.Any<IEnumerable<ChatMessage>>(),
                Arg.Any<ChatOptions?>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromException<ChatResponse>(new HttpRequestException("Network error")));

        var result = await _service.ValidateGoalAsync("Run a marathon in 6 months");

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Gemini request failed");
    }

    [Fact]
    public async Task ValidateGoal_TooManyRequests_ReturnsMessageFromException()
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

        var result = await _service.ValidateGoalAsync("Run a marathon in 6 months");

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("rate limit");
    }

    [Fact]
    public async Task GenerateRefinementQuestions_TooManyRequests_ReturnsMessageFromException()
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

        var result = await _service.GenerateRefinementQuestionsAsync("""{"description":"test"}""");

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("rate limit");
    }

    // ── GenerateRefinementQuestionsAsync ──────────────────────────────────

    [Fact]
    public async Task GenerateRefinementQuestions_ValidJson_ReturnsQuestions()
    {
        var json = """
            [
              { "RankOrder": 1, "Question": "What is your age and gender?" },
              { "RankOrder": 2, "Question": "What is your current baseline?" },
              { "RankOrder": 3, "Question": "Any injuries?" }
            ]
            """;
        ArrangeResponse(json);

        var result = await _service.GenerateRefinementQuestionsAsync("""{"description":"test"}""");

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(3);
        result.Value![0].RankOrder.Should().Be(1);
        result.Value[0].Question.Should().Be("What is your age and gender?");
    }

    [Fact]
    public async Task GenerateRefinementQuestions_MalformedJson_ReturnsFailure()
    {
        ArrangeResponse("not a json array");

        var result = await _service.GenerateRefinementQuestionsAsync("""{"description":"test"}""");

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task GenerateRefinementQuestions_QuestionsOrderedByRankOrder()
    {
        var json = """
            [
              { "RankOrder": 3, "Question": "Third" },
              { "RankOrder": 1, "Question": "First" },
              { "RankOrder": 2, "Question": "Second" }
            ]
            """;
        ArrangeResponse(json);

        var result = await _service.GenerateRefinementQuestionsAsync("{}");

        result.IsSuccess.Should().BeTrue();
        result.Value!.Select(q => q.RankOrder).Should().BeInAscendingOrder();
    }
}
