using FluentAssertions;
using LifeGrid.Infrastructure.AI;
using Microsoft.Extensions.AI;
using NSubstitute;
using System.Net;

namespace LifeGrid.Infrastructure.Tests.AI;

public sealed class GeminiViceSurveyServiceTests
{
    private readonly IChatClient              _chatClient = Substitute.For<IChatClient>();
    private readonly GeminiViceSurveyService  _service;

    public GeminiViceSurveyServiceTests()
        => _service = new GeminiViceSurveyService(_chatClient);

    // ── helpers ───────────────────────────────────────────────────────────────

    private void ArrangeResponse(string text)
        => _chatClient
            .GetResponseAsync(
                Arg.Any<IEnumerable<ChatMessage>>(),
                Arg.Any<ChatOptions?>(),
                Arg.Any<CancellationToken>())
            .Returns(new ChatResponse(new ChatMessage(ChatRole.Assistant, text)));

    private void ArrangeThrows(Exception ex)
        => _chatClient
            .GetResponseAsync(
                Arg.Any<IEnumerable<ChatMessage>>(),
                Arg.Any<ChatOptions?>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromException<ChatResponse>(ex));

    private static readonly string ValidQuestionsJson = """
        {
          "survey_title": "The Lifestyle Diagnostic Audit",
          "survey_purpose": "To identify hidden friction points.",
          "questions": [
            {
              "id": "q1",
              "type": "multiple_choice",
              "question_text": "What is your stress response?",
              "options": ["Scroll social media", "Eat snacks", "Other"]
            },
            {
              "id": "q2",
              "type": "open_ended",
              "question_text": "Describe your bedtime routine.",
              "options": null
            }
          ]
        }
        """;

    private static readonly string ValidVicesJson = """
        [
          {
            "isValid": true,
            "data": {
              "description": "Run a marathon",
              "duration": "6 months",
              "deadline_date": "2026-12-10",
              "ambient_tag": "Fitness"
            },
            "bad_habits": [
              { "description": "Late-night scrolling", "danger": 3 },
              { "description": "Sugar cravings",       "danger": 5 }
            ]
          }
        ]
        """;

    // ── GenerateQuestions ─────────────────────────────────────────────────────

    [Fact]
    public async Task GenerateQuestions_ValidJson_ReturnsCorrectCount()
    {
        ArrangeResponse(ValidQuestionsJson);

        var result = await _service.GenerateQuestionsAsync("[]");

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(2);
    }

    [Fact]
    public async Task GenerateQuestions_MultipleChoiceQuestion_HasOptions()
    {
        ArrangeResponse(ValidQuestionsJson);

        var result = await _service.GenerateQuestionsAsync("[]");

        var q1 = result.Value!.First(q => q.Id == "q1");
        q1.Type.Should().Be("multiple_choice");
        q1.Options.Should().HaveCount(3);
    }

    [Fact]
    public async Task GenerateQuestions_OpenEndedQuestion_HasNullOptions()
    {
        ArrangeResponse(ValidQuestionsJson);

        var result = await _service.GenerateQuestionsAsync("[]");

        var q2 = result.Value!.First(q => q.Id == "q2");
        q2.Type.Should().Be("open_ended");
        q2.Options.Should().BeNullOrEmpty();
    }

    [Fact]
    public async Task GenerateQuestions_AllGoalsInPrompt()
    {
        string? capturedPrompt = null;
        _chatClient
            .GetResponseAsync(
                Arg.Do<IEnumerable<ChatMessage>>(msgs => capturedPrompt = msgs.First().Text),
                Arg.Any<ChatOptions?>(),
                Arg.Any<CancellationToken>())
            .Returns(new ChatResponse(new ChatMessage(ChatRole.Assistant, ValidQuestionsJson)));

        var goalsJson = """[{"description":"Run a marathon"},{"description":"Learn Spanish"}]""";
        await _service.GenerateQuestionsAsync(goalsJson);

        capturedPrompt.Should().Contain("Run a marathon").And.Contain("Learn Spanish");
    }

    [Fact]
    public async Task GenerateQuestions_MalformedJson_ReturnsFailure()
    {
        ArrangeResponse("not valid json {{{{");

        var result = await _service.GenerateQuestionsAsync("[]");

        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task GenerateQuestions_MissingQuestionsField_ReturnsFailure()
    {
        ArrangeResponse("""{"survey_title": "Audit"}""");

        var result = await _service.GenerateQuestionsAsync("[]");

        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task GenerateQuestions_RateLimit_ReturnsFailure()
    {
        ArrangeThrows(new HttpRequestException("Too Many Requests", null, HttpStatusCode.TooManyRequests));

        var result = await _service.GenerateQuestionsAsync("[]");

        result.IsSuccess.Should().BeFalse();
    }

    // ── AnalyzeAnswers ────────────────────────────────────────────────────────

    [Fact]
    public async Task AnalyzeAnswers_ValidJson_ReturnsFlatViceList()
    {
        ArrangeResponse(ValidVicesJson);

        var result = await _service.AnalyzeAnswersAsync("[]", "[]");

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(2);
        result.Value!.Select(v => v.Description)
              .Should().Contain("Late-night scrolling").And.Contain("Sugar cravings");
    }

    [Fact]
    public async Task AnalyzeAnswers_GoalDescriptionPopulatedOnVices()
    {
        ArrangeResponse(ValidVicesJson);

        var result = await _service.AnalyzeAnswersAsync("[]", "[]");

        result.Value!.All(v => v.GoalDescription == "Run a marathon").Should().BeTrue();
    }

    [Fact]
    public async Task AnalyzeAnswers_PlaceholdersSubstituted()
    {
        string? capturedPrompt = null;
        _chatClient
            .GetResponseAsync(
                Arg.Do<IEnumerable<ChatMessage>>(msgs => capturedPrompt = msgs.First().Text),
                Arg.Any<ChatOptions?>(),
                Arg.Any<CancellationToken>())
            .Returns(new ChatResponse(new ChatMessage(ChatRole.Assistant, ValidVicesJson)));

        await _service.AnalyzeAnswersAsync("""[{"answer":"yes"}]""", """[{"description":"Run"}]""");

        capturedPrompt.Should().NotContain("${USER_SURVEY_ANSWERS_JSON}");
        capturedPrompt.Should().NotContain("${USER_GOALS_JSON}");
        capturedPrompt.Should().Contain("answer");
    }

    [Fact]
    public async Task AnalyzeAnswers_MalformedJson_ReturnsFailure()
    {
        ArrangeResponse("this is not json at all");

        var result = await _service.AnalyzeAnswersAsync("[]", "[]");

        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task AnalyzeAnswers_RateLimit_ReturnsFailure()
    {
        ArrangeThrows(new HttpRequestException("Too Many Requests", null, HttpStatusCode.TooManyRequests));

        var result = await _service.AnalyzeAnswersAsync("[]", "[]");

        result.IsSuccess.Should().BeFalse();
    }
}
