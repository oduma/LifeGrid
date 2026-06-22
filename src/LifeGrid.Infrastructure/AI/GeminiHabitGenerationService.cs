using LifeGrid.Application.Week;
using LifeGrid.Domain.Common;
using Microsoft.Extensions.AI;
using System.Net;
using System.Reflection;
using System.Text.Json;

namespace LifeGrid.Infrastructure.AI;

internal sealed class GeminiHabitGenerationService(IChatClient chatClient)
    : IGeminiHabitGenerationService
{
    private static readonly string Prompt21Template = LoadEmbeddedPrompt("prompt2.1.txt");
    private static readonly string Prompt22Template = LoadEmbeddedPrompt("prompt2.2.txt");

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task<Result<HabitSchedulingResult>> GenerateScheduleAsync(
        string            goalAsStated,
        string            deadlineAsStated,
        string            baselineAnswersJson,
        DateTime          startDate,
        CancellationToken ct = default)
    {
        // ── Call 1: Blueprint (prompt2.1) ─────────────────────────────────
        var startDateStr = startDate.ToString("MMMM d, yyyy");
        var prompt1 = Prompt21Template
                          .Replace("${USER_GOAL}",                  goalAsStated)
                          .Replace("${USER_DEADLINE}",              deadlineAsStated)
                          .Replace("${USER_BASELINE_ANSWERS_JSON}", baselineAnswersJson)
                          .Replace("${START_DATE}",                 startDateStr);

        string call1Raw;
        try
        {
            var response1 = await chatClient.GetResponseAsync(
                new List<ChatMessage> { new(ChatRole.User, prompt1) },
                cancellationToken: ct);
            call1Raw = response1.Text ?? string.Empty;
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.TooManyRequests)
        {
            return Result<HabitSchedulingResult>.Failure(ex.Message);
        }
        catch (Exception ex)
        {
            return Result<HabitSchedulingResult>.Failure($"Gemini request failed (blueprint call): {ex.Message}");
        }

        // Parse Call 1 to check feasibility
        var feasibilityResult = ParseFeasibility(StripCodeFences(call1Raw));
        if (!feasibilityResult.IsSuccess)
            return Result<HabitSchedulingResult>.Failure(feasibilityResult.Error!);

        if (feasibilityResult.Value is HabitSchedulingResult.Infeasible)
            return Result<HabitSchedulingResult>.Success(feasibilityResult.Value);

        // ── Call 2: Schedule (prompt2.2) ──────────────────────────────────
        // Pass the raw Call 1 JSON directly — no re-serialization
        var prompt2 = Prompt22Template
            .Replace("${COACH_SPECIALIST_PARAMETERS_JSON}", call1Raw)
            .Replace("${START_DATE}", startDateStr);

        string call2Raw;
        try
        {
            var response2 = await chatClient.GetResponseAsync(
                new List<ChatMessage> { new(ChatRole.User, prompt2) },
                cancellationToken: ct);
            call2Raw = response2.Text ?? string.Empty;
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.TooManyRequests)
        {
            return Result<HabitSchedulingResult>.Failure(ex.Message);
        }
        catch (Exception ex)
        {
            return Result<HabitSchedulingResult>.Failure($"Gemini request failed (schedule call): {ex.Message}");
        }

        return ParseSchedule(StripCodeFences(call2Raw));
    }

    // ── Parsing ────────────────────────────────────────────────────────────

    private static Result<HabitSchedulingResult> ParseFeasibility(string json)
    {
        try
        {
            using var doc  = JsonDocument.Parse(json);
            var       root = doc.RootElement;

            if (!root.TryGetProperty("isFeasible", out var feasibleProp))
                return Result<HabitSchedulingResult>.Failure(
                    "Gemini blueprint response missing 'isFeasible' field.");

            if (!feasibleProp.GetBoolean())
            {
                var reason    = root.TryGetProperty("recalibration_reason", out var rp)
                    ? rp.GetString() ?? "Goal flagged as unsafe by the AI."
                    : "Goal flagged as unsafe by the AI.";

                string? suggestedDeadline     = null;
                string? suggestedScope        = null;

                if (root.TryGetProperty("recommended_recalibration", out var rec))
                {
                    if (rec.TryGetProperty("suggested_deadline", out var sd))
                        suggestedDeadline = sd.GetString();
                    if (rec.TryGetProperty("suggested_alternative_scope", out var ss))
                        suggestedScope = ss.GetString();
                }

                return Result<HabitSchedulingResult>.Success(
                    new HabitSchedulingResult.Infeasible(reason, suggestedDeadline, suggestedScope));
            }

            // Feasible — return a sentinel; the raw JSON travels on to Call 2
            return Result<HabitSchedulingResult>.Success(
                new HabitSchedulingResult.Feasible(Array.Empty<WeekScheduleDto>()));
        }
        catch (JsonException)
        {
            return Result<HabitSchedulingResult>.Failure(
                "Gemini returned malformed JSON for the blueprint call.");
        }
    }

    private static Result<HabitSchedulingResult> ParseSchedule(string json)
    {
        try
        {
            using var doc  = JsonDocument.Parse(json);
            var       root = doc.RootElement;

            if (!root.TryGetProperty("weeks", out var weeksProp))
                return Result<HabitSchedulingResult>.Failure(
                    "Gemini schedule response missing 'weeks' field.");

            var weeks = new List<WeekScheduleDto>();

            foreach (var weekEl in weeksProp.EnumerateArray())
            {
                var weekNumber = weekEl.GetProperty("week_number").GetInt32();
                var startDate  = DateTime.Parse(weekEl.GetProperty("start_date").GetString()!);

                var habits = new List<HabitScheduleItemDto>();
                if (weekEl.TryGetProperty("habits", out var habitsProp))
                {
                    foreach (var habitEl in habitsProp.EnumerateArray())
                    {
                        var description = habitEl.GetProperty("description").GetString() ?? string.Empty;
                        var measurement = habitEl.GetProperty("measurement");
                        var value       = measurement.GetProperty("value").GetDouble();
                        var unit        = measurement.GetProperty("unit").GetString() ?? string.Empty;

                        habits.Add(new HabitScheduleItemDto(description, value, unit));
                    }
                }

                weeks.Add(new WeekScheduleDto(weekNumber, startDate, habits));
            }

            return Result<HabitSchedulingResult>.Success(new HabitSchedulingResult.Feasible(weeks));
        }
        catch (JsonException ex)
        {
            return Result<HabitSchedulingResult>.Failure(
                $"Gemini returned malformed JSON for the schedule call: {ex.Message}");
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
