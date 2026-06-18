using LifeGrid.Application.Goal;
using LifeGrid.Domain.Common;
using Microsoft.Extensions.AI;
using System.Net;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace LifeGrid.Infrastructure.AI;

internal sealed class GeminiGoalValidationService(IChatClient chatClient)
    : IGeminiGoalValidationService
{
    private static readonly string Prompt1Template = LoadEmbeddedPrompt("prompt1.txt");
    private static readonly string Prompt2Template = LoadEmbeddedPrompt("prompt2.txt");

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task<Result<GeminiValidationResult>> ValidateGoalAsync(
        string            rawDraft,
        CancellationToken ct = default)
    {
        var today  = DateTime.UtcNow.ToString("MMMM d, yyyy");
        var prompt = $"[Current date: {today}]\n\n" +
                     Prompt1Template.Replace("${USER_INPUT_TEXT}", rawDraft);

        string responseText;
        try
        {
            var response = await chatClient.GetResponseAsync(
                new List<ChatMessage> { new(ChatRole.User, prompt) }, cancellationToken: ct);
            responseText = response.Text ?? string.Empty;
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.TooManyRequests)
        {
            return Result<GeminiValidationResult>.Failure(ex.Message);
        }
        catch (Exception ex)
        {
            return Result<GeminiValidationResult>.Failure($"Gemini request failed: {ex.Message}");
        }

        return ParseValidationResponse(StripCodeFences(responseText));
    }

    public async Task<Result<IReadOnlyList<RefinementQuestionDto>>> GenerateRefinementQuestionsAsync(
        string            validatedGoalJson,
        CancellationToken ct = default)
    {
        var prompt = Prompt2Template.Replace("${VALIDATED_GOAL_JSON}", validatedGoalJson);

        string responseText;
        try
        {
            var response = await chatClient.GetResponseAsync(
                new List<ChatMessage> { new(ChatRole.User, prompt) }, cancellationToken: ct);
            responseText = response.Text ?? string.Empty;
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.TooManyRequests)
        {
            return Result<IReadOnlyList<RefinementQuestionDto>>.Failure(ex.Message);
        }
        catch (Exception ex)
        {
            return Result<IReadOnlyList<RefinementQuestionDto>>.Failure($"Gemini request failed: {ex.Message}");
        }

        return ParseRefinementQuestionsResponse(StripCodeFences(responseText));
    }

    // ── Parsing ────────────────────────────────────────────────────────────

    private static Result<GeminiValidationResult> ParseValidationResponse(string json)
    {
        try
        {
            using var doc  = JsonDocument.Parse(json);
            var       root = doc.RootElement;

            if (!root.TryGetProperty("isValid", out var isValidProp))
                return Result<GeminiValidationResult>.Failure("Gemini response missing 'isValid' field.");

            if (isValidProp.GetBoolean())
            {
                if (!root.TryGetProperty("goal", out var goalProp))
                    return Result<GeminiValidationResult>.Failure("Gemini valid response missing 'goal' field.");

                var goalJson = goalProp.GetRawText();
                var raw      = JsonSerializer.Deserialize<RawGoalPayload>(goalJson, JsonOpts);
                if (raw is null)
                    return Result<GeminiValidationResult>.Failure("Could not deserialize goal payload.");

                if (!DateTime.TryParse(raw.DeadlineDate, out var deadline))
                    return Result<GeminiValidationResult>.Failure("Could not parse deadline_date.");

                var dto = new ValidatedGoalDto(
                    raw.Description  ?? string.Empty,
                    raw.Duration     ?? string.Empty,
                    deadline,
                    raw.AmbientTag   ?? string.Empty);

                return Result<GeminiValidationResult>.Success(new GeminiValidationResult.Valid(dto));
            }
            else
            {
                var retryPrompt = root.TryGetProperty("retry_prompt", out var rp)
                    ? rp.GetString() ?? "Please provide more details about your goal."
                    : "Please provide more details about your goal.";

                return Result<GeminiValidationResult>.Success(new GeminiValidationResult.Invalid(retryPrompt));
            }
        }
        catch (JsonException)
        {
            return Result<GeminiValidationResult>.Failure("Gemini returned malformed JSON for validation.");
        }
    }

    private static Result<IReadOnlyList<RefinementQuestionDto>> ParseRefinementQuestionsResponse(string json)
    {
        try
        {
            var items = JsonSerializer.Deserialize<List<RawQuestionPayload>>(json, JsonOpts);
            if (items is null)
                return Result<IReadOnlyList<RefinementQuestionDto>>.Failure("Could not deserialize refinement questions.");

            var dtos = items
                .Select(q => new RefinementQuestionDto(q.RankOrder, q.Question ?? string.Empty))
                .OrderBy(q => q.RankOrder)
                .ToList();

            return Result<IReadOnlyList<RefinementQuestionDto>>.Success(dtos);
        }
        catch (JsonException)
        {
            return Result<IReadOnlyList<RefinementQuestionDto>>.Failure("Gemini returned malformed JSON for refinement questions.");
        }
    }

    // ── Prompt loading ─────────────────────────────────────────────────────

    private static string LoadEmbeddedPrompt(string fileName)
    {
        var assembly       = Assembly.GetExecutingAssembly();
        var resourceName   = $"LifeGrid.Infrastructure.AI.Prompts.{fileName}";
        using var stream   = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Embedded resource '{resourceName}' not found.");
        using var reader   = new StreamReader(stream);
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

    // ── Private DTOs for JSON deserialization ──────────────────────────────

    private sealed class RawGoalPayload
    {
        [JsonPropertyName("description")]   public string? Description  { get; set; }
        [JsonPropertyName("duration")]      public string? Duration     { get; set; }
        [JsonPropertyName("deadline_date")] public string? DeadlineDate { get; set; }
        [JsonPropertyName("ambient_tag")]   public string? AmbientTag   { get; set; }
    }

    private sealed class RawQuestionPayload
    {
        [JsonPropertyName("RankOrder")] public int     RankOrder { get; set; }
        [JsonPropertyName("Question")]  public string? Question  { get; set; }
    }
}
