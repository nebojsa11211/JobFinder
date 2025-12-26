using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using JobFinder.Models;

namespace JobFinder.Services;

public class KimiService : IKimiService
{
    private readonly ISettingsService _settingsService;
    private readonly HttpClient _httpClient;
    private readonly string _logDirectory;

    public bool IsConfigured => !string.IsNullOrWhiteSpace(_settingsService.Settings.KimiApiKey);

    public KimiService(ISettingsService settingsService)
    {
        _settingsService = settingsService;
        _httpClient = new HttpClient();

        // Setup log directory for AI responses
        _logDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "JobFinder", "ai-logs");
        Directory.CreateDirectory(_logDirectory);
    }

    public async Task<JobSummaryResult?> GetSummaryAsync(string jobDescription, CancellationToken cancellationToken = default)
    {
        if (!IsConfigured || string.IsNullOrWhiteSpace(jobDescription))
            return null;

        var settings = _settingsService.Settings;
        var prompt = settings.SummaryPrompt.Replace("{description}", jobDescription);

        try
        {
            // System message strongly enforces JSON output format
            var systemMessage = """
                You are a job analysis assistant. You MUST respond with valid JSON only.
                Never include any text before or after the JSON object.
                Never use markdown code blocks. Just output raw JSON.
                The JSON must have these exact fields: shortSummary, summary, rating, shouldDiscard, discardReason
                """;

            var requestBody = new
            {
                model = settings.KimiModel,
                messages = new object[]
                {
                    new { role = "system", content = systemMessage },
                    new { role = "user", content = prompt }
                },
                temperature = 0.3, // Lower temperature for more consistent structured output
                response_format = new { type = "json_object" } // Force JSON mode (OpenAI compatible)
            };

            var json = JsonSerializer.Serialize(requestBody);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", settings.KimiApiKey);

            var response = await _httpClient.PostAsync(
                $"{settings.KimiApiBaseUrl.TrimEnd('/')}/chat/completions",
                content,
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync(cancellationToken);
                return null;
            }

            var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
            using var doc = JsonDocument.Parse(responseJson);

            var message = doc.RootElement
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString();

            if (string.IsNullOrEmpty(message))
                return null;

            var result = ParseStructuredResponse(message);
            if (result != null)
                result.RawResponse = message;

            // Log the raw response for debugging
            await LogAiResponseAsync(jobDescription, message, result);

            return result;
        }
        catch (Exception ex)
        {
            // Log the exception
            await LogAiResponseAsync(jobDescription, $"EXCEPTION: {ex.Message}", null);
            return null;
        }
    }

    public async Task<(bool Success, string? ErrorMessage)> ValidateApiKeyAsync()
    {
        if (!IsConfigured)
        {
            return (false, "API key is not configured. Please add your Kimi API key in Settings.");
        }

        var settings = _settingsService.Settings;

        try
        {
            // Make a minimal API call to test the key
            var requestBody = new
            {
                model = settings.KimiModel,
                messages = new object[]
                {
                    new { role = "user", content = "Hi" }
                },
                max_tokens = 5
            };

            var json = JsonSerializer.Serialize(requestBody);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", settings.KimiApiKey);

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            var response = await _httpClient.PostAsync(
                $"{settings.KimiApiBaseUrl.TrimEnd('/')}/chat/completions",
                content,
                cts.Token);

            if (response.IsSuccessStatusCode)
            {
                return (true, null);
            }

            var errorContent = await response.Content.ReadAsStringAsync();

            return response.StatusCode switch
            {
                System.Net.HttpStatusCode.Unauthorized => (false, "Invalid API key. Please check your Kimi API key in Settings."),
                System.Net.HttpStatusCode.Forbidden => (false, "API key does not have permission. Please check your Kimi API key."),
                System.Net.HttpStatusCode.TooManyRequests => (false, "API rate limit exceeded. Please try again later."),
                System.Net.HttpStatusCode.NotFound => (false, $"API endpoint not found. Please check the API base URL in Settings.\n\nURL: {settings.KimiApiBaseUrl}"),
                System.Net.HttpStatusCode.BadRequest => (false, $"Invalid request to API. Model '{settings.KimiModel}' may not be available."),
                _ => (false, $"API error ({response.StatusCode}): {errorContent}")
            };
        }
        catch (TaskCanceledException)
        {
            return (false, "Connection timed out. Please check your internet connection and API base URL.");
        }
        catch (HttpRequestException ex)
        {
            return (false, $"Connection failed: {ex.Message}\n\nPlease check your internet connection and API base URL.");
        }
        catch (Exception ex)
        {
            return (false, $"Unexpected error: {ex.Message}");
        }
    }

    private async Task LogAiResponseAsync(string jobDescription, string response, JobSummaryResult? result)
    {
        try
        {
            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss_fff");
            var parseStatus = result?.ParseFailed == true ? "PARSE_FAILED" : "OK";
            var logFile = Path.Combine(_logDirectory, $"{timestamp}_{parseStatus}.log");

            var logContent = $"""
                === AI RESPONSE LOG ===
                Timestamp: {DateTime.Now:yyyy-MM-dd HH:mm:ss}
                Parse Status: {parseStatus}
                Rating: {result?.Rating ?? 0}
                ShouldDiscard: {result?.ShouldDiscard}
                DiscardReason: {result?.DiscardReason ?? "N/A"}

                === JOB DESCRIPTION (first 500 chars) ===
                {jobDescription[..Math.Min(500, jobDescription.Length)]}...

                === RAW AI RESPONSE ===
                {response}

                === PARSED RESULT ===
                ShortSummary: {result?.ShortSummary ?? "N/A"}
                Summary: {result?.Summary ?? "N/A"}
                """;

            await File.WriteAllTextAsync(logFile, logContent);
        }
        catch
        {
            // Ignore logging errors
        }
    }

    private static int ParseRating(JsonElement element)
    {
        // Handle integer values
        if (element.ValueKind == JsonValueKind.Number && element.TryGetInt32(out var intValue))
            return intValue;

        // Handle string values like "7" or "7/10"
        if (element.ValueKind == JsonValueKind.String)
        {
            var str = element.GetString() ?? "";
            // Extract first number from strings like "7", "7/10", "Rating: 7"
            var match = Regex.Match(str, @"\d+");
            if (match.Success && int.TryParse(match.Value, out var parsed))
                return parsed;
        }

        return 0; // Will be clamped to 1 later
    }

    private static JobSummaryResult? ParseStructuredResponse(string response)
    {
        try
        {
            var jsonString = ExtractJsonFromResponse(response);
            if (string.IsNullOrEmpty(jsonString))
            {
                // JSON extraction failed - try fallback text parsing
                return ParsePlainTextResponse(response);
            }

            using var doc = JsonDocument.Parse(jsonString);
            var root = doc.RootElement;

            var result = new JobSummaryResult();

            // Parse short summary
            if (root.TryGetProperty("shortSummary", out var shortSummaryProp))
                result.ShortSummary = shortSummaryProp.GetString() ?? "";
            else if (root.TryGetProperty("ShortSummary", out shortSummaryProp))
                result.ShortSummary = shortSummaryProp.GetString() ?? "";
            else if (root.TryGetProperty("short_summary", out shortSummaryProp))
                result.ShortSummary = shortSummaryProp.GetString() ?? "";

            // Parse summary (try multiple possible property names)
            if (root.TryGetProperty("summary", out var summaryProp))
                result.Summary = summaryProp.GetString() ?? "";
            else if (root.TryGetProperty("Summary", out summaryProp))
                result.Summary = summaryProp.GetString() ?? "";
            else if (root.TryGetProperty("sazetak", out summaryProp))
                result.Summary = summaryProp.GetString() ?? "";

            // Parse rating (handle both int and string formats)
            if (root.TryGetProperty("rating", out var ratingProp))
                result.Rating = ParseRating(ratingProp);
            else if (root.TryGetProperty("Rating", out ratingProp))
                result.Rating = ParseRating(ratingProp);
            else if (root.TryGetProperty("ocjena", out ratingProp))
                result.Rating = ParseRating(ratingProp);

            // Clamp rating to 1-10
            result.Rating = Math.Clamp(result.Rating, 1, 10);

            // Parse shouldDiscard
            if (root.TryGetProperty("shouldDiscard", out var discardProp))
                result.ShouldDiscard = discardProp.ValueKind == JsonValueKind.True;
            else if (root.TryGetProperty("ShouldDiscard", out discardProp))
                result.ShouldDiscard = discardProp.ValueKind == JsonValueKind.True;
            else if (root.TryGetProperty("discard", out discardProp))
                result.ShouldDiscard = discardProp.ValueKind == JsonValueKind.True;
            else if (root.TryGetProperty("odbaci", out discardProp))
                result.ShouldDiscard = discardProp.ValueKind == JsonValueKind.True;

            // Parse discard reason
            if (root.TryGetProperty("discardReason", out var reasonProp))
                result.DiscardReason = reasonProp.GetString();
            else if (root.TryGetProperty("DiscardReason", out reasonProp))
                result.DiscardReason = reasonProp.GetString();
            else if (root.TryGetProperty("reason", out reasonProp))
                result.DiscardReason = reasonProp.GetString();
            else if (root.TryGetProperty("razlog", out reasonProp))
                result.DiscardReason = reasonProp.GetString();

            return result;
        }
        catch (JsonException ex)
        {
            return CreateParseFailureResult(response, $"JSON parse error: {ex.Message}");
        }
        catch (Exception ex)
        {
            return CreateParseFailureResult(response, $"Unexpected error: {ex.Message}");
        }
    }

    /// <summary>
    /// Extracts JSON from various response formats (code blocks, raw JSON, etc.)
    /// </summary>
    private static string? ExtractJsonFromResponse(string response)
    {
        if (string.IsNullOrWhiteSpace(response))
            return null;

        // Strategy 1: Try to extract JSON from markdown code block (```json ... ``` or ``` ... ```)
        var codeBlockMatch = Regex.Match(response, @"```(?:json)?\s*(\{[\s\S]*?\})\s*```", RegexOptions.IgnoreCase);
        if (codeBlockMatch.Success)
        {
            return codeBlockMatch.Groups[1].Value.Trim();
        }

        // Strategy 2: Try to find JSON object with balanced braces
        var braceStart = response.IndexOf('{');
        var braceEnd = response.LastIndexOf('}');
        if (braceStart >= 0 && braceEnd > braceStart)
        {
            var potentialJson = response.Substring(braceStart, braceEnd - braceStart + 1);
            // Validate it's actually parseable JSON before returning
            try
            {
                using var doc = JsonDocument.Parse(potentialJson);
                return potentialJson;
            }
            catch
            {
                // Not valid JSON, continue to next strategy
            }
        }

        // Strategy 3: Check if the entire response is JSON
        try
        {
            var trimmed = response.Trim();
            if (trimmed.StartsWith('{') && trimmed.EndsWith('}'))
            {
                using var doc = JsonDocument.Parse(trimmed);
                return trimmed;
            }
        }
        catch
        {
            // Not valid JSON
        }

        return null;
    }

    /// <summary>
    /// Creates a result indicating parse failure - these jobs should NOT be auto-discarded
    /// </summary>
    private static JobSummaryResult CreateParseFailureResult(string response, string reason)
    {
        return new JobSummaryResult
        {
            Summary = response.Length > 500 ? response[..500] + "..." : response,
            ShortSummary = "⚠️ Parse failed - needs review",
            Rating = 5, // Neutral rating - don't auto-discard
            ShouldDiscard = false,
            ParseFailed = true,
            DiscardReason = $"AI response could not be parsed: {reason}",
            RawResponse = response
        };
    }

    /// <summary>
    /// Fallback parser for when AI returns plain text instead of JSON.
    /// Extracts rating, summary, and discard recommendation from unstructured text.
    /// </summary>
    private static JobSummaryResult ParsePlainTextResponse(string response)
    {
        var result = new JobSummaryResult
        {
            ParseFailed = false, // We're successfully parsing, just not from JSON
            RawResponse = response
        };

        // Extract rating from various patterns
        result.Rating = ExtractRatingFromText(response);

        // Extract bullet points as summary
        result.Summary = ExtractBulletPointsAsSummary(response);

        // Generate short summary from first meaningful line or bullet
        result.ShortSummary = ExtractShortSummary(response);

        // Determine if should discard based on content analysis
        var (shouldDiscard, discardReason) = AnalyzeForDiscard(response);
        result.ShouldDiscard = shouldDiscard;
        result.DiscardReason = discardReason;

        return result;
    }

    /// <summary>
    /// Extracts rating from patterns like "Rating: 7", "7/10", "(7/10)", "Ocjena: 7"
    /// </summary>
    private static int ExtractRatingFromText(string text)
    {
        // Pattern: "Rating: 7" or "rating: 7/10" or "Ocjena: 7"
        var ratingPatterns = new[]
        {
            @"[Rr]ating[:\s]+(\d+)(?:/10)?",
            @"[Oo]cjena[:\s]+(\d+)(?:/10)?",
            @"\((\d+)/10\)",
            @"(\d+)/10",
            @"[Ss]core[:\s]+(\d+)"
        };

        foreach (var pattern in ratingPatterns)
        {
            var match = Regex.Match(text, pattern);
            if (match.Success && int.TryParse(match.Groups[1].Value, out var rating))
            {
                return Math.Clamp(rating, 1, 10);
            }
        }

        // Default to 5 if no rating found
        return 5;
    }

    /// <summary>
    /// Extracts bullet points and formats them as a summary
    /// </summary>
    private static string ExtractBulletPointsAsSummary(string text)
    {
        var lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        var bulletPoints = new List<string>();

        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            // Match various bullet point formats: •, -, *, ●, ▪, numbers
            if (Regex.IsMatch(trimmed, @"^[•\-\*●▪◦‣⁃]\s*") ||
                Regex.IsMatch(trimmed, @"^\d+[\.\)]\s*"))
            {
                // Clean up the bullet point
                var cleaned = Regex.Replace(trimmed, @"^[•\-\*●▪◦‣⁃\d\.\)]+\s*", "• ");
                if (cleaned.Length > 10) // Skip very short lines
                {
                    bulletPoints.Add(cleaned);
                }
            }
        }

        if (bulletPoints.Count > 0)
        {
            return string.Join("\n", bulletPoints.Take(6)); // Max 6 bullet points
        }

        // Fallback: use first 500 chars of response
        return text.Length > 500 ? text[..500] + "..." : text;
    }

    /// <summary>
    /// Extracts a short summary (first meaningful content)
    /// </summary>
    private static string ExtractShortSummary(string text)
    {
        var lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            // Skip headers and very short lines
            if (trimmed.Length < 15) continue;
            if (trimmed.StartsWith("Key Points", StringComparison.OrdinalIgnoreCase)) continue;
            if (trimmed.StartsWith("Summary", StringComparison.OrdinalIgnoreCase)) continue;
            if (trimmed.StartsWith("Croatian", StringComparison.OrdinalIgnoreCase)) continue;

            // Clean bullet point prefix
            var cleaned = Regex.Replace(trimmed, @"^[•\-\*●▪◦‣⁃\d\.\)]+\s*", "").Trim();
            if (cleaned.Length >= 15)
            {
                // Truncate to ~60 chars at word boundary
                if (cleaned.Length > 60)
                {
                    var cutoff = cleaned.LastIndexOf(' ', 60);
                    if (cutoff > 30) cleaned = cleaned[..cutoff] + "...";
                    else cleaned = cleaned[..57] + "...";
                }
                return cleaned;
            }
        }

        return "AI analysis complete";
    }

    /// <summary>
    /// Analyzes text content to determine if job should be discarded
    /// </summary>
    private static (bool ShouldDiscard, string? Reason) AnalyzeForDiscard(string text)
    {
        var textLower = text.ToLowerInvariant();

        // Check for .NET/C# technologies - if none found, likely should discard
        var hasDotNet = Regex.IsMatch(textLower, @"\.net|c#|csharp|asp\.net|blazor|wpf|maui|azure");

        // Check for discard indicators
        var discardIndicators = new Dictionary<string, string>
        {
            { @"\bnot\s+(a\s+)?real\s+job", "Not a real job posting" },
            { @"\brecruiter\s+spam", "Recruiter spam" },
            { @"\bshould\s+(be\s+)?discard", "AI recommended discard" },
            { @"\bdiscard[:\s]+true", "AI recommended discard" },
            { @"\bodbaci[:\s]+true", "AI recommended discard" }
        };

        foreach (var (pattern, reason) in discardIndicators)
        {
            if (Regex.IsMatch(textLower, pattern))
            {
                return (true, reason);
            }
        }

        // Check for non-.NET only jobs
        var frontendOnly = Regex.IsMatch(textLower, @"\b(react|angular|vue|javascript|typescript)\b") &&
                          !hasDotNet;
        var otherBackend = Regex.IsMatch(textLower, @"\b(python|java|ruby|php|golang|go\s+developer|node\.?js)\b") &&
                          !Regex.IsMatch(textLower, @"\b(java\s*script|\.net\s+and\s+java)\b") && // Not JavaScript, not ".NET and Java"
                          !hasDotNet;

        if (frontendOnly)
        {
            return (true, "Frontend-only role (no .NET)");
        }

        if (otherBackend)
        {
            return (true, "Non-.NET backend role");
        }

        return (false, null);
    }
}
