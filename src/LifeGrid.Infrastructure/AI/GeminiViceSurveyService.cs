using LifeGrid.Application.Vice;
using LifeGrid.Domain.Common;
using Microsoft.Extensions.AI;
using System.Net;
using System.Reflection;
using System.Text.Json;

namespace LifeGrid.Infrastructure.AI;

internal sealed class GeminiViceSurveyService(IChatClient chatClient)
    : IGeminiViceSurveyService
{
    private static readonly string Prompt31Template = LoadEmbeddedPrompt("prompt3.1.txt");
    private static readonly string Prompt32Template = LoadEmbeddedPrompt("prompt3.2.txt");

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true
    };

    // ── Loop 1: Question Generation ────────────────────────────────────────

    public async Task<Result<IReadOnlyList<SurveyQuestionDto>>> GenerateQuestionsAsync(
        string            goalsContextJson,
        CancellationToken ct = default)
    {
        var today  = DateTime.UtcNow.ToString("MMMM d, yyyy");
        var prompt = $"[Current date: {today}]\n\n{Prompt31Template}\n\nUser's Active Goals:\n{goalsContextJson}";

        string responseText;
        try
        {
            var response = await chatClient.GetResponseAsync(
                new List<ChatMessage> { new(ChatRole.User, prompt) },
                cancellationToken: ct);
            responseText = response.Text ?? string.Empty;
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.TooManyRequests)
        {
            return Result<IReadOnlyList<SurveyQuestionDto>>.Failure(ex.Message);
        }
        catch (Exception ex)
        {
            return Result<IReadOnlyList<SurveyQuestionDto>>.Failure(
                $"Gemini request failed (question generation): {ex.Message}");
        }

        return ParseQuestions(StripCodeFences(responseText));
    }

    // ── Loop 2: Vice Analysis ──────────────────────────────────────────────

    public async Task<Result<IReadOnlyList<DetectedViceDto>>> AnalyzeAnswersAsync(
        string            answersJson,
        string            goalsJson,
        CancellationToken ct = default)
    {
        var prompt = Prompt32Template
            .Replace("${USER_SURVEY_ANSWERS_JSON}", answersJson)
            .Replace("${USER_GOALS_JSON}",          goalsJson);

        string responseText;
        try
        {
            var response = await chatClient.GetResponseAsync(
                new List<ChatMessage> { new(ChatRole.User, prompt) },
                cancellationToken: ct);
            responseText = response.Text ?? string.Empty;
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.TooManyRequests)
        {
            return Result<IReadOnlyList<DetectedViceDto>>.Failure(ex.Message);
        }
        catch (Exception ex)
        {
            return Result<IReadOnlyList<DetectedViceDto>>.Failure(
                $"Gemini request failed (vice analysis): {ex.Message}");
        }

        return ParseVices(StripCodeFences(responseText));
    }

    // ── Parsing ────────────────────────────────────────────────────────────

    private static Result<IReadOnlyList<SurveyQuestionDto>> ParseQuestions(string json)
    {
        try
        {
            using var doc  = JsonDocument.Parse(json);
            var       root = doc.RootElement;

            if (!root.TryGetProperty("questions", out var questionsEl))
                return Result<IReadOnlyList<SurveyQuestionDto>>.Failure(
                    "Gemini questions response missing 'questions' field.");

            var questions = new List<SurveyQuestionDto>();
            foreach (var q in questionsEl.EnumerateArray())
            {
                var id           = q.GetProperty("id").GetString()            ?? string.Empty;
                var type         = q.GetProperty("type").GetString()           ?? string.Empty;
                var questionText = q.GetProperty("question_text").GetString()  ?? string.Empty;

                List<string>? options = null;
                if (q.TryGetProperty("options", out var optionsProp) &&
                    optionsProp.ValueKind == JsonValueKind.Array)
                {
                    options = new List<string>();
                    foreach (var opt in optionsProp.EnumerateArray())
                    {
                        var optText = opt.GetString();
                        if (optText is not null) options.Add(optText);
                    }
                }

                questions.Add(new SurveyQuestionDto(id, type, questionText, options));
            }

            return Result<IReadOnlyList<SurveyQuestionDto>>.Success(questions);
        }
        catch (JsonException ex)
        {
            return Result<IReadOnlyList<SurveyQuestionDto>>.Failure(
                $"Gemini returned malformed JSON for question generation: {ex.Message}");
        }
    }

    private static Result<IReadOnlyList<DetectedViceDto>> ParseVices(string json)
    {
        try
        {
            using var doc  = JsonDocument.Parse(json);
            var       root = doc.RootElement;

            var vices = new List<DetectedViceDto>();
            foreach (var goalEl in root.EnumerateArray())
            {
                if (!goalEl.TryGetProperty("data", out var dataEl)) continue;

                var goalDescription = dataEl.TryGetProperty("description", out var descProp)
                    ? descProp.GetString() ?? string.Empty
                    : string.Empty;

                if (!goalEl.TryGetProperty("bad_habits", out var habitsEl)) continue;
                if (habitsEl.ValueKind != JsonValueKind.Array) continue;

                foreach (var habitEl in habitsEl.EnumerateArray())
                {
                    var description = habitEl.GetProperty("description").GetString() ?? string.Empty;
                    var danger      = habitEl.GetProperty("danger").GetInt32();
                    vices.Add(new DetectedViceDto(description, danger, goalDescription));
                }
            }

            return Result<IReadOnlyList<DetectedViceDto>>.Success(vices);
        }
        catch (JsonException ex)
        {
            return Result<IReadOnlyList<DetectedViceDto>>.Failure(
                $"Gemini returned malformed JSON for vice analysis: {ex.Message}");
        }
    }

    // ── Helpers ────────────────────────────────────────────────────────────

    private static string LoadEmbeddedPrompt(string fileName)
    {
        var assembly     = Assembly.GetExecutingAssembly();
        var resourceName = $"LifeGrid.Infrastructure.AI.Prompts.{fileName}";
        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Embedded resource '{resourceName}' not found.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    private static string StripCodeFences(string text)
    {
        var trimmed = text.Trim();
        if (trimmed.StartsWith("```"))
        {
            var firstNewline = trimmed.IndexOf('\n');
            if (firstNewline >= 0)
                trimmed = trimmed[(firstNewline + 1)..];
            if (trimmed.EndsWith("```"))
                trimmed = trimmed[..^3].TrimEnd();
        }
        return trimmed;
    }
}
