namespace JobFinder.Models;

public class AppSettings
{
    // === AI Configuration ===
    public string KimiApiKey { get; set; } = "";
    public string KimiApiBaseUrl { get; set; } = "https://api.moonshot.ai/v1";
    public string KimiModel { get; set; } = "kimi-k2-0711-preview";
    public string SummaryPrompt { get; set; } = DefaultPrompt;

    // === Display Options ===
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

    // === Auto-Apply Configuration ===

    /// <summary>
    /// User's professional profile (CV content, skills, experience).
    /// Used by AI to generate personalized application messages and answer questions.
    /// </summary>
    public string UserProfessionalProfile { get; set; } = "";

    /// <summary>
    /// Cover letter template with placeholders.
    /// Placeholders: [JobTitle], [Company], [Skills], [Experience]
    /// </summary>
    public string CoverLetterTemplate { get; set; } = DefaultCoverLetterTemplate;

    /// <summary>
    /// Prompt template for generating application messages.
    /// Placeholders: {profile}, {jobDescription}, {jobTitle}, {company}
    /// </summary>
    public string ApplicationMessagePrompt { get; set; } = DefaultApplicationMessagePrompt;

    /// <summary>
    /// Minimum delay between automated actions in milliseconds.
    /// </summary>
    public int MinActionDelayMs { get; set; } = 1500;

    /// <summary>
    /// Maximum delay between automated actions in milliseconds.
    /// </summary>
    public int MaxActionDelayMs { get; set; } = 4000;

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

    public const string DefaultCoverLetterTemplate = @"Dear Hiring Team,

I am excited to apply for the [JobTitle] position at [Company]. With my experience in [Skills], I am confident I can contribute effectively to your team.

[Experience]

I am particularly drawn to this opportunity because of [Company]'s reputation and the chance to work on challenging projects. I look forward to discussing how my background aligns with your needs.

Best regards";

    public const string DefaultApplicationMessagePrompt = @"You are a professional job application assistant. Generate a personalized, concise application message.

CANDIDATE PROFILE:
{profile}

JOB DETAILS:
- Title: {jobTitle}
- Company: {company}
- Description: {jobDescription}

INSTRUCTIONS:
1. Write a professional, concise message (150-200 words maximum)
2. Address 2-3 specific requirements mentioned in the job description
3. Highlight relevant experience from the candidate's profile that matches
4. Be genuine and enthusiastic, avoid generic clichés
5. Do NOT include greetings like 'Dear Hiring Manager' - start directly with content
6. Do NOT include sign-offs - end with the last substantive sentence

Respond with JSON only:
```json
{
  ""message"": ""The application message text..."",
  ""addressedRequirements"": [""requirement 1 from job"", ""requirement 2 from job""],
  ""matchingSkills"": [""skill1"", ""skill2""],
  ""confidenceScore"": 85
}
```";

    public const string DefaultQuestionAnswerPrompt = @"You are helping a job candidate answer application questions based on their profile.

CANDIDATE PROFILE:
{profile}

JOB CONTEXT:
{jobDescription}

QUESTIONS TO ANSWER:
{questions}

INSTRUCTIONS:
1. Answer each question concisely and professionally
2. Use information from the candidate's profile when available
3. For experience-related questions, extract years/details from the profile
4. For yes/no questions, answer definitively based on profile
5. If information is not in the profile, provide a reasonable professional answer
6. Keep answers brief but complete

Respond with JSON only:
```json
{
  ""answers"": {
    ""Question text 1"": ""Answer 1"",
    ""Question text 2"": ""Answer 2""
  }
}
```";
}
