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
    /// When true, shows AI debug info (prompt sent and raw response) in job details.
    /// </summary>
    public bool ShowAiDebug { get; set; } = false;

    /// <summary>
    /// Minimum rating threshold for auto-discarding jobs (1-10). Jobs below this rating will be auto-discarded.
    /// </summary>
    public int MinimumRating { get; set; } = 4;

    public const string DefaultPrompt = @"Analyze this job description and respond with a JSON object.

IMPORTANT: ALL text output must be in Croatian language only.

Your task:
1. Create a SHORT summary (5-10 words max) in Croatian describing the core role, e.g., ""Backend API razvoj s Azure cloudom"" or ""Full-stack .NET s React frontendом""

2. Summarize the job in 3-5 bullet points in Croatian, focusing on:
   - Key responsibilities
   - Required skills/experience
   - Benefits if mentioned

3. Rate the job from 1-10 based on:
   - Clear job requirements (not vague)
   - Reasonable experience expectations
   - Good benefits/compensation mentioned (note: ""competitive rate"" is acceptable for freelance roles)
   - Remote/flexible work options (highly valued)
   - Technology stack relevance for .NET developers
   - Freelance/contract opportunities are POSITIVE (not negative) - rate them higher
   - Long-term contracts (6+ months) are a plus

4. Decide if the job should be discarded. DISCARD if ANY of these apply:
   - It's not a real job (recruiter spam, generic posting). Note: freelance/contract postings with clear requirements are REAL jobs, not spam.
   - Requires 10+ years for mid-level position
   - Location requirements don't match remote work
   - Job is clearly unrelated to software development
   - **CRITICAL**: Job does NOT require .NET, C#, ASP.NET Core, Blazor, WPF, MAUI, or Azure as PRIMARY technologies for software development.
   - DISCARD these roles unless they specifically require C#/.NET coding:
     * DevOps/SRE/Infrastructure Engineer roles (even if using Azure) - DISCARD unless writing C#/.NET code
     * Cloud Engineer/Architect roles focused on AWS, GCP, or infrastructure-only Azure work - DISCARD
     * Frontend-only roles (Angular, React, Vue, JavaScript/TypeScript without .NET backend) - DISCARD
     * Python-only, Java-only, Node.js-only, Ruby-only, PHP-only, Go-only, Rust-only roles - DISCARD
     * Data Engineer/ML Engineer roles without C#/.NET - DISCARD
     * Terraform/Kubernetes/Docker-focused roles without C#/.NET development - DISCARD
   - The job MUST involve writing C#/.NET code as a primary responsibility
   - Posting is in a language other than English without translation

Respond ONLY with this JSON format (no other text):
```json
{
  ""shortSummary"": ""Kratki opis posla na hrvatskom (5-10 riječi)"",
  ""summary"": ""• Točka 1 na hrvatskom\n• Točka 2\n• Točka 3"",
  ""rating"": 7,
  ""shouldDiscard"": false,
  ""discardReason"": null
}
```

If discarding, set shouldDiscard to true and provide a brief reason in discardReason.

Job description:
{description}";
}
