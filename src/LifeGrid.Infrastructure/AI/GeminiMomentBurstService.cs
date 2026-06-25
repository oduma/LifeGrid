using LifeGrid.Application.MomentBurst;
using LifeGrid.Domain.Common;
using Microsoft.Extensions.AI;
using System.Net;
using System.Reflection;
using System.Text.Json;

namespace LifeGrid.Infrastructure.AI;

internal sealed class GeminiMomentBurstService(IChatClient chatClient)
    : IGeminiMomentBurstService
{
    private static readonly string PromptTemplate = LoadEmbeddedPrompt("prompt5.txt");

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task<Result<MomentBurstResult>> GenerateAsync(
        string            userFreeText,
        string            weeklyHabitsJson,
        DateTime          currentDate,
        CancellationToken ct = default)
    {
        var prompt = PromptTemplate
            .Replace("${CURRENT_DATE}",      currentDate.ToString("MMMM d, yyyy"))
            .Replace("${USER_FREE_TEXT}",     userFreeText)
            .Replace("${WEEKLY_HABITS_JSON}", weeklyHabitsJson);

        string raw;
        try
        {
            var response = await chatClient.GetResponseAsync(
                new List<ChatMessage> { new(ChatRole.User, prompt) },
                cancellationToken: ct);
            raw = response.Text ?? string.Empty;
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.TooManyRequests)
        {
            return Result<MomentBurstResult>.Failure(ex.Message);
        }
        catch (Exception ex)
        {
            return Result<MomentBurstResult>.Failure($"Gemini request failed: {ex.Message}");
        }

        return ParseResponse(StripCodeFences(raw));
    }

    private static Result<MomentBurstResult> ParseResponse(string json)
    {
        try
        {
            using var doc  = JsonDocument.Parse(json);
            var       root = doc.RootElement;

            var status = root.TryGetProperty("status", out var sp)
                ? sp.GetString() ?? string.Empty
                : string.Empty;

            if (status == "denied")
            {
                var msg = root.TryGetProperty("habit_description", out var dp)
                    ? dp.GetString() ?? "Stay focused on your current habits."
                    : "Stay focused on your current habits.";
                return Result<MomentBurstResult>.Success(new MomentBurstResult.Denied(msg));
            }

            var name  = root.TryGetProperty("momentum_burst_quest_name", out var np)
                ? np.GetString() ?? string.Empty
                : string.Empty;
            var desc  = root.TryGetProperty("habit_description", out var dp2)
                ? dp2.GetString() ?? string.Empty
                : string.Empty;

            if (!root.TryGetProperty("measure", out var measureProp))
                return Result<MomentBurstResult>.Failure(
                    "Gemini response missing 'measure' field.");

            var value = measureProp.TryGetProperty("value", out var vp)
                ? vp.GetDouble()
                : 1.0;
            var unit = measureProp.TryGetProperty("unit", out var up)
                ? up.GetString() ?? string.Empty
                : string.Empty;

            return Result<MomentBurstResult>.Success(
                new MomentBurstResult.Accepted(name, desc, value, unit));
        }
        catch (JsonException ex)
        {
            return Result<MomentBurstResult>.Failure(
                $"Gemini returned malformed JSON: {ex.Message}");
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
