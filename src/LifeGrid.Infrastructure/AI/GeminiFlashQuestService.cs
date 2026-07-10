using LifeGrid.Application.FlashQuest;
using LifeGrid.Domain.Common;
using Microsoft.Extensions.AI;
using System.Net;
using System.Reflection;
using System.Text.Json;

namespace LifeGrid.Infrastructure.AI;

internal sealed class GeminiFlashQuestService(IChatClient chatClient)
    : IGeminiFlashQuestService
{
    private static readonly string PromptTemplate = LoadEmbeddedPrompt("prompt8.txt");

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task<Result<FlashQuestGenerationResult>> GenerateAsync(
        string            weeklyHabitsJson,
        DateTime          currentDate,
        CancellationToken ct = default)
    {
        var prompt = PromptTemplate
            .Replace("${CURRENT_DATE}",      currentDate.ToString("MMMM d, yyyy"))
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
            return Result<FlashQuestGenerationResult>.Failure(ex.Message);
        }
        catch (Exception ex)
        {
            return Result<FlashQuestGenerationResult>.Failure($"Gemini request failed: {ex.Message}");
        }

        return ParseResponse(StripCodeFences(raw));
    }

    private static Result<FlashQuestGenerationResult> ParseResponse(string text)
    {
        if (text.Trim().Trim('"') == "N/A")
            return Result<FlashQuestGenerationResult>.Success(new FlashQuestGenerationResult.NotEligible());

        try
        {
            using var doc  = JsonDocument.Parse(text);
            var       root = doc.RootElement;

            if (!root.TryGetProperty("flash-quests", out var questsProp) ||
                questsProp.ValueKind != JsonValueKind.Array)
                return Result<FlashQuestGenerationResult>.Failure(
                    "Gemini response missing 'flash-quests' array.");

            var items = new List<FlashQuestItem>();
            foreach (var item in questsProp.EnumerateArray())
            {
                var sourceHabitId = item.TryGetProperty("source_habit_id", out var sp)
                    && Guid.TryParse(sp.GetString(), out var g) ? g : Guid.Empty;

                var name = item.TryGetProperty("falsh_quest_name", out var np)
                    ? np.GetString() ?? string.Empty
                    : string.Empty;

                var desc = item.TryGetProperty("habit_description", out var dp)
                    ? dp.GetString() ?? string.Empty
                    : string.Empty;

                double value = 1.0;
                string unit  = string.Empty;
                if (item.TryGetProperty("measure", out var measureProp))
                {
                    value = measureProp.TryGetProperty("value", out var vp) ? vp.GetDouble() : 1.0;
                    unit  = measureProp.TryGetProperty("unit", out var up) ? up.GetString() ?? string.Empty : string.Empty;
                }

                items.Add(new FlashQuestItem(sourceHabitId, name, desc, value, unit));
            }

            return Result<FlashQuestGenerationResult>.Success(
                new FlashQuestGenerationResult.Accepted(items));
        }
        catch (JsonException ex)
        {
            return Result<FlashQuestGenerationResult>.Failure(
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
