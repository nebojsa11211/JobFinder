namespace JobFinder.Models;

public class AppSettings
{
    public string KimiApiKey { get; set; } = "";
    public string KimiApiBaseUrl { get; set; } = "https://api.moonshot.ai/v1";
    public string KimiModel { get; set; } = "kimi-k2-0711-preview";
    public string SummaryPrompt { get; set; } = DefaultPrompt;
    public bool ShowDiscardedJobs { get; set; } = false;

    /// <summary>
    /// When true, the browser window starts minimized instead of maximized.
    /// </summary>
    public bool StartBrowserMinimized { get; set; } = false;

    /// <summary>
    /// Minimum rating threshold for auto-discarding jobs (1-10). Jobs below this rating will be auto-discarded.
    /// </summary>
    public int MinimumRating { get; set; } = 4;

    public const string DefaultPrompt = @"Analyze this job description and respond with a JSON object.

Your task:
1. Create a SHORT summary (5-10 words max) describing the core role, e.g., ""Backend API development with Azure cloud"" or ""Full-stack .NET with React frontend""

2. Summarize the job in 3-5 bullet points in Croatian, focusing on:
   - Key responsibilities
   - Required skills/experience
   - Benefits if mentioned

3. Rate the job from 1-10 based on:
   - Clear job requirements (not vague)
   - Reasonable experience expectations
   - Good benefits/compensation mentioned
   - Remote/flexible work options
   - Technology stack relevance for .NET developers

4. Decide if the job should be discarded. Discard if:
   - It's not a real job (recruiter spam, generic posting)
   - Requires 10+ years for mid-level position
   - Location requirements don't match remote work
   - Job is clearly unrelated to software development
   - Job does NOT require .NET, C#, ASP.NET Core, Blazor, WPF, MAUI, or Azure as primary technologies. Discard frontend-only roles (Angular, React, Vue, JavaScript/TypeScript without .NET backend), Python-only, Java-only, Node.js-only, Ruby-only, PHP-only, Go-only roles. The job MUST involve C#/.NET development.
   - Posting is in a language other than English without translation

Respond ONLY with this JSON format (no other text):
```json
{
  ""shortSummary"": ""Brief 5-10 word description of the role"",
  ""summary"": ""• Bullet point 1 in Croatian\n• Bullet point 2\n• Bullet point 3"",
  ""rating"": 7,
  ""shouldDiscard"": false,
  ""discardReason"": null
}
```

If discarding, set shouldDiscard to true and provide a brief reason in discardReason.

Job description:
{description}";
}
