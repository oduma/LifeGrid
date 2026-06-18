using LifeGrid.Infrastructure.Security;
using Microsoft.Extensions.AI;
using System.Net;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace LifeGrid.Infrastructure.AI;

internal sealed class GeminiHttpChatClient(
    HttpClient            http,
    ISecureStorageService secureStorage) : IChatClient
{
    private const string PrimaryUrl  =
        "https://generativelanguage.googleapis.com/v1beta/models/gemini-2.5-pro:generateContent";

    private const string FallbackUrl =
        "https://generativelanguage.googleapis.com/v1beta/models/gemini-2.5-flash:generateContent";

    public async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions?             options = null,
        CancellationToken        ct      = default)
    {
        var apiKey = await secureStorage.GetAsync("Gemini_Provider_Token") ?? string.Empty;

        if (string.IsNullOrWhiteSpace(apiKey))
            throw new InvalidOperationException(
                "Gemini API key is not configured. Please add it in Settings before using AI features.");

        var promptText = string.Concat(messages.Select(m => m.Text ?? string.Empty));

        var body = new GeminiRequest(new[]
        {
            new GeminiContent(new[] { new GeminiPart(promptText) })
        });

        var response = await http.PostAsJsonAsync($"{PrimaryUrl}?key={apiKey}", body, ct);

        // Transparent fallback: 503 from the primary model → retry once with the flash model.
        if (response.StatusCode == HttpStatusCode.ServiceUnavailable)
            response = await http.PostAsJsonAsync($"{FallbackUrl}?key={apiKey}", body, ct);

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(ct);

            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                var waitSeconds = ParseRetryDelaySeconds(errorBody);
                var waitHint    = waitSeconds > 0
                    ? $" Please wait {waitSeconds} seconds and try again."
                    : " Please wait about 30 seconds and try again.";
                throw new HttpRequestException(
                    "Gemini rate limit reached." + waitHint,
                    inner: null,
                    statusCode: HttpStatusCode.TooManyRequests);
            }

            throw new HttpRequestException(
                $"Gemini API error {(int)response.StatusCode}: {errorBody}",
                inner: null,
                statusCode: response.StatusCode);
        }

        var geminiResponse = await response.Content.ReadFromJsonAsync<GeminiResponse>(cancellationToken: ct);
        var text = geminiResponse?.Candidates?.FirstOrDefault()?.Content?.Parts?.FirstOrDefault()?.Text;

        if (string.IsNullOrEmpty(text))
            throw new InvalidOperationException(
                "Gemini returned a successful response but with no text content. " +
                $"Candidates count: {geminiResponse?.Candidates?.Length ?? 0}.");

        return new ChatResponse(new ChatMessage(ChatRole.Assistant, text));
    }

    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage>              messages,
        ChatOptions?                          options = null,
        [EnumeratorCancellation] CancellationToken ct  = default)
    {
        var full = await GetResponseAsync(messages, options, ct);
        yield return new ChatResponseUpdate(ChatRole.Assistant, full.Text ?? string.Empty);
    }

    public object? GetService(Type serviceType, object? serviceKey = null) => null;

    public void Dispose() { }

    // Gemini puts the retry delay in the response body, not the Retry-After header.
    // Body shape: {"error":{"details":[{"@type":"...RetryInfo","retryDelay":"30s"}]}}
    private static int ParseRetryDelaySeconds(string errorBody)
    {
        try
        {
            using var doc = JsonDocument.Parse(errorBody);
            var details = doc.RootElement
                .GetProperty("error")
                .GetProperty("details");

            foreach (var detail in details.EnumerateArray())
            {
                if (detail.TryGetProperty("retryDelay", out var delayProp))
                {
                    var raw = delayProp.GetString() ?? string.Empty; // e.g. "30s"
                    if (raw.EndsWith('s') && int.TryParse(raw[..^1], out var secs))
                        return secs;
                }
            }
        }
        catch (Exception) { /* malformed body — fall through to default */ }

        return 0;
    }

    // ── Private DTOs ──────────────────────────────────────────────────────

    private sealed record GeminiRequest(
        [property: JsonPropertyName("contents")] GeminiContent[] Contents);

    private sealed record GeminiContent(
        [property: JsonPropertyName("parts")] GeminiPart[] Parts);

    private sealed record GeminiPart(
        [property: JsonPropertyName("text")] string Text);

    private sealed class GeminiResponse
    {
        [JsonPropertyName("candidates")]
        public GeminiCandidate[]? Candidates { get; set; }
    }

    private sealed class GeminiCandidate
    {
        [JsonPropertyName("content")]
        public GeminiContent? Content { get; set; }
    }
}
