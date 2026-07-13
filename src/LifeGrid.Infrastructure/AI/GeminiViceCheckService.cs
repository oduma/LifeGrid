using LifeGrid.Application.ViceCheck;
using LifeGrid.Domain.Common;
using Microsoft.Extensions.AI;
using System.Net;
using System.Reflection;
using System.Text.Json;

namespace LifeGrid.Infrastructure.AI;

internal sealed class GeminiViceCheckService(IChatClient chatClient)
    : IGeminiViceCheckService
{
    private static readonly string Prompt6Template = LoadEmbeddedPrompt("prompt6.txt");
    private static readonly string Prompt7Template = LoadEmbeddedPrompt("prompt7.txt");

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task<Result<GenerateQuestionResult>> GenerateQuestionAsync(
        string goalAndHabitJson, CancellationToken ct = default)
    {
        var prompt = Prompt6Template.Replace("${GOALS_AND_HABITS_JSON}", goalAndHabitJson);

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
            return Result<GenerateQuestionResult>.Failure(ex.Message);
        }
        catch (Exception ex)
        {
            return Result<GenerateQuestionResult>.Failure(
                $"Gemini request failed (question generation): {ex.Message}");
        }

        return ParseQuestion(StripCodeFences(responseText));
    }

    public async Task<Result<EvaluateAnswerResult>> EvaluateAnswerAsync(
        string userResponseJson, CancellationToken ct = default)
    {
        var prompt = Prompt7Template.Replace("${USER_RESPONSE_JSON}", userResponseJson);

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
            return Result<EvaluateAnswerResult>.Failure(ex.Message);
        }
        catch (Exception ex)
        {
            return Result<EvaluateAnswerResult>.Failure(
                $"Gemini request failed (answer evaluation): {ex.Message}");
        }

        return ParseAnswer(StripCodeFences(responseText));
    }

    private static Result<GenerateQuestionResult> ParseQuestion(string json)
    {
        try
        {
            using var doc  = JsonDocument.Parse(json);
            var       root = doc.RootElement;

            if (!root.TryGetProperty("ambient_question", out var qp))
                return Result<GenerateQuestionResult>.Failure(
                    "Gemini response missing 'ambient_question' field.");

            return Result<GenerateQuestionResult>.Success(
                new GenerateQuestionResult(qp.GetString() ?? string.Empty));
        }
        catch (JsonException ex)
        {
            return Result<GenerateQuestionResult>.Failure(
                $"Gemini returned malformed JSON for question generation: {ex.Message}");
        }
    }

    private static Result<EvaluateAnswerResult> ParseAnswer(string json)
    {
        try
        {
            using var doc  = JsonDocument.Parse(json);
            var       root = doc.RootElement;

            if (!root.TryGetProperty("persists", out var pp))
                return Result<EvaluateAnswerResult>.Failure(
                    "Gemini response missing 'persists' field.");

            return Result<EvaluateAnswerResult>.Success(new EvaluateAnswerResult(pp.GetBoolean()));
        }
        catch (JsonException ex)
        {
            return Result<EvaluateAnswerResult>.Failure(
                $"Gemini returned malformed JSON for answer evaluation: {ex.Message}");
        }
    }

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
