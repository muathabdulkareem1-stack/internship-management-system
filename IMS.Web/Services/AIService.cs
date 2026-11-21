using System.Text;
using System.Text.Json;
using IMS.Web.Models;

namespace IMS.Web.Services;

public class AIService(IConfiguration config, IHttpClientFactory httpFactory)
{
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    private async Task<string> CallClaude(string prompt)
    {
        var apiKey = config["Anthropic:ApiKey"]!;
        var model  = config["Anthropic:Model"] ?? "claude-haiku-4-5";

        using var client = httpFactory.CreateClient();
        client.DefaultRequestHeaders.Add("x-api-key", apiKey);
        client.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");

        var body = new
        {
            model,
            max_tokens = 2048,
            messages = new[] { new { role = "user", content = prompt } }
        };

        var response = await client.PostAsync(
            "https://api.anthropic.com/v1/messages",
            new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json"));

        var raw = await response.Content.ReadAsStringAsync();

        using var doc = JsonDocument.Parse(raw);
        var root = doc.RootElement;

        if (root.TryGetProperty("error", out var errProp))
        {
            var msg = errProp.TryGetProperty("message", out var m) ? m.GetString() : raw;
            throw new InvalidOperationException($"Anthropic API error: {msg}");
        }

        return root.GetProperty("content")[0].GetProperty("text").GetString() ?? string.Empty;
    }

    private static string ExtractJson(string text)
    {
        var start = text.IndexOf('{');
        var end   = text.LastIndexOf('}');
        return start >= 0 && end > start ? text[start..(end + 1)] : "{}";
    }

    public async Task<StudentInsight> GetStudentInsight(
        string studentName, string company, string position,
        int hoursLogged, int hoursRequired,
        int totalTasks, int reviewedTasks, int rejectedTasks,
        int submittedTasks, int pendingTasks,
        List<(string Skill, double Rating)> skillRatings,
        int daysLeft,
        List<(string Label, int Hours)> hoursTimeline,
        int daysElapsed, int totalDays, double dailyHoursRate,
        int projectedHours, int projectedPercent,
        int cohortRank, int totalStudents,
        double cohortAvgHoursPercent, double cohortAvgSkill, double cohortAvgTaskRate,
        List<string> aboveAverageSkills, List<string> belowAverageSkills)
    {
        var skills   = skillRatings.Any()
            ? string.Join(", ", skillRatings.Select(s => $"{s.Skill}: {s.Rating:0.0}/5"))
            : "No evaluations yet";

        var timeline = hoursTimeline.Any()
            ? string.Join(", ", hoursTimeline.Select(h => $"{h.Label}: {h.Hours}h"))
            : "No hours logged yet";

        int hoursPercent   = hoursRequired > 0 ? Math.Min(100, hoursLogged * 100 / hoursRequired) : 0;
        int taskRate       = totalTasks > 0 ? reviewedTasks * 100 / totalTasks : 0;
        string avgSkillStr = skillRatings.Any()
            ? skillRatings.Average(s => s.Rating).ToString("0.00", System.Globalization.CultureInfo.InvariantCulture)
            : "null";
        string skillJson    = skillRatings.Any()
            ? string.Join(", ", skillRatings.Select(s => $"{{\"skill\": \"{s.Skill}\", \"rating\": {s.Rating:0.0}}}"))
            : "";
        string timelineJson = hoursTimeline.Any()
            ? string.Join(", ", hoursTimeline.Select(h => $"{{\"label\": \"{h.Label}\", \"hours\": {h.Hours}}}"))
            : "";
        string aboveAvg = aboveAverageSkills.Any() ? string.Join(", ", aboveAverageSkills) : "none";
        string belowAvg = belowAverageSkills.Any() ? string.Join(", ", belowAverageSkills) : "none";
        string hoursNeeded = daysLeft > 0
            ? ((hoursRequired - hoursLogged) / (double)daysLeft).ToString("0.1")
            : "0";

        var prompt = $$"""
            You are an internship management AI. Analyze this student's data and return ONLY a JSON object with no extra text or markdown.

            Student: {{studentName}}
            Company: {{company}}, Position: {{position}}
            Hours logged: {{hoursLogged}} / {{hoursRequired}} required ({{hoursPercent}}%)
            Tasks: {{totalTasks}} total — {{reviewedTasks}} reviewed, {{submittedTasks}} submitted, {{rejectedTasks}} rejected, {{pendingTasks}} pending
            Days remaining: {{daysLeft}}
            Skill ratings: {{skills}}
            Hours by month: {{timeline}}

            Prediction data:
            Days elapsed: {{daysElapsed}} of {{totalDays}} total
            Daily rate: {{dailyHoursRate:0.1}} hrs/day
            Projected total: {{projectedHours}} / {{hoursRequired}} ({{projectedPercent}}%)
            Needed to finish on time: {{hoursNeeded}} hrs/day

            Cohort:
            Rank {{cohortRank}} of {{totalStudents}} interns
            Cohort avg hours {{cohortAvgHoursPercent:0.0}}% vs this student {{hoursPercent}}%
            Cohort avg skill {{cohortAvgSkill:0.1}}/5 vs this student {{avgSkillStr}}/5
            Cohort avg tasks {{cohortAvgTaskRate:0.0}}% vs this student {{taskRate}}%
            Above average in: {{aboveAvg}}
            Below average in: {{belowAvg}}

            Return ONLY this JSON:
            {
              "summary": "2-3 sentence progress summary",
              "riskLevel": "low|medium|high",
              "riskReason": "one sentence why",
              "topSkill": "best skill name or null",
              "weakestSkill": "weakest skill name or null",
              "recommendation": "one actionable sentence for the student",
              "hoursPercent": {{hoursPercent}},
              "taskCompletionRate": {{taskRate}},
              "averageSkillRating": {{avgSkillStr}},
              "taskBreakdown": { "reviewed": {{reviewedTasks}}, "submitted": {{submittedTasks}}, "rejected": {{rejectedTasks}}, "pending": {{pendingTasks}} },
              "skillScores": [{{skillJson}}],
              "hoursTimeline": [{{timelineJson}}],
              "projectedHoursPercent": {{projectedPercent}},
              "projectedOutcome": "one sentence on where this student ends up at current pace",
              "trendDirection": "up|down|flat",
              "performanceTrend": "one sentence on whether performance is improving or declining",
              "cohortRank": {{cohortRank}},
              "totalStudents": {{totalStudents}},
              "percentile": "e.g. top 25%",
              "aboveAverageIn": [{{(aboveAverageSkills.Any() ? string.Join(", ", aboveAverageSkills.Select(s => $"\"{s}\"")) : "")}}],
              "belowAverageIn": [{{(belowAverageSkills.Any() ? string.Join(", ", belowAverageSkills.Select(s => $"\"{s}\"")) : "")}}],
              "comparisonSummary": "2 sentences comparing this student to the cohort"
            }
            """;

        var raw  = await CallClaude(prompt);
        var json = ExtractJson(raw);
        return JsonSerializer.Deserialize<StudentInsight>(json, JsonOpts) ?? new StudentInsight();
    }

    public async Task<CoordinatorInsight> GetCoordinatorInsight(List<InternSummary> interns)
    {
        var lines = interns.Select(i =>
            $"- {i.Name} at {i.Company}: {i.HoursPercent}% hours, {i.ReviewedTasks}/{i.TotalTasks} tasks reviewed, avg skill {i.AvgSkill:0.0}/5, {i.DaysLeft} days left");

        var internJson = string.Join(", ", interns.Select(i =>
            $"{{\"name\": \"{i.Name}\", \"hours\": {i.HoursPercent}, \"avgSkill\": {i.AvgSkill:0.0}}}"));

        double avgHours = interns.Any() ? interns.Average(i => i.HoursPercent) : 0;
        double avgSkill = interns.Any() ? interns.Average(i => i.AvgSkill) : 0;

        var prompt = $$"""
            You are an internship coordinator AI. Analyze all interns and return ONLY a JSON object with no extra text or markdown.

            Interns:
            {{string.Join("\n", lines)}}

            Return ONLY this JSON:
            {
              "overallSummary": "2-sentence cohort summary",
              "atRisk": ["intern name"],
              "onTrack": ["intern name"],
              "topPerformer": "name or null",
              "averageHoursPercent": {{avgHours:0.0}},
              "averageSkillRating": {{avgSkill:0.00}},
              "coordinatorRecommendation": "one actionable suggestion",
              "internCharts": [{{internJson}}]
            }
            """;

        var raw  = await CallClaude(prompt);
        var json = ExtractJson(raw);
        return JsonSerializer.Deserialize<CoordinatorInsight>(json, JsonOpts) ?? new CoordinatorInsight();
    }

    public async Task<SupervisorInsight> GetSupervisorInsight(
        string studentName, string position,
        List<(string Skill, double Rating)> skillRatings,
        List<(string TaskTitle, int? Rating, string? Feedback)> taskEvals,
        List<(string Label, double AvgRating)> evalByMonth,
        List<(string Label, int Rating)> taskRatingHistory)
    {
        var skills = skillRatings.Any()
            ? string.Join(", ", skillRatings.Select(s => $"{s.Skill}: {s.Rating:0.0}/5"))
            : "No evaluations yet";

        var tasks = taskEvals.Any()
            ? string.Join("; ", taskEvals.Select(t =>
                $"{t.TaskTitle} (rating: {(t.Rating.HasValue ? t.Rating + "/5" : "not rated")})"))
            : "No task evaluations yet";

        var evalTrendJson = evalByMonth.Any()
            ? string.Join(", ", evalByMonth.Select(e => $"{{\"label\": \"{e.Label}\", \"avgRating\": {e.AvgRating:0.0}}}"))
            : "";

        var taskTrendJson = taskRatingHistory.Any()
            ? string.Join(", ", taskRatingHistory.Select(t => $"{{\"label\": \"{t.Label}\", \"rating\": {t.Rating}}}"))
            : "";

        var monthlyAvg = evalByMonth.Any()
            ? string.Join(", ", evalByMonth.Select(e => $"{e.Label}: {e.AvgRating:0.0}/5"))
            : "No monthly evaluations yet";

        var prompt = $$"""
            You are a supervisor AI assistant. Analyze this intern's skill ratings and task performance and return ONLY a JSON object with no extra text or markdown.

            Intern: {{studentName}}, Position: {{position}}
            Skill ratings: {{skills}}
            Task ratings: {{tasks}}
            Monthly evaluation averages: {{monthlyAvg}}

            Return ONLY this JSON:
            {
              "skillGapSummary": "2-sentence analysis of skill gaps",
              "strongestSkill": "skill name or null",
              "weakestSkill": "skill name or null",
              "taskPerformanceSummary": "one sentence about task quality",
              "suggestedFocus": "one specific suggestion for improvement",
              "overallRating": "Excellent|Good|Needs Improvement",
              "skillScores": [{"skill": "name", "rating": 0.0, "comment": "short comment"}],
              "evalTrend": [{{evalTrendJson}}],
              "taskRatingTrend": [{{taskTrendJson}}]
            }
            """;

        var raw  = await CallClaude(prompt);
        var json = ExtractJson(raw);
        return JsonSerializer.Deserialize<SupervisorInsight>(json, JsonOpts) ?? new SupervisorInsight();
    }
}

public class StudentInsight
{
    public string Summary { get; set; } = string.Empty;
    public string RiskLevel { get; set; } = "low";
    public string RiskReason { get; set; } = string.Empty;
    public string? TopSkill { get; set; }
    public string? WeakestSkill { get; set; }
    public string Recommendation { get; set; } = string.Empty;
    public double HoursPercent { get; set; }
    public double TaskCompletionRate { get; set; }
    public double? AverageSkillRating { get; set; }
    public TaskBreakdownData TaskBreakdown { get; set; } = new();
    public List<ChartPoint> SkillScores { get; set; } = [];
    public List<HoursPoint> HoursTimeline { get; set; } = [];
    public int ProjectedHoursPercent { get; set; }
    public string ProjectedOutcome { get; set; } = string.Empty;
    public string TrendDirection { get; set; } = "flat";
    public string PerformanceTrend { get; set; } = string.Empty;
    public int CohortRank { get; set; }
    public int TotalStudents { get; set; }
    public string Percentile { get; set; } = string.Empty;
    public List<string> AboveAverageIn { get; set; } = [];
    public List<string> BelowAverageIn { get; set; } = [];
    public string ComparisonSummary { get; set; } = string.Empty;
}

public class CoordinatorInsight
{
    public string OverallSummary { get; set; } = string.Empty;
    public List<string> AtRisk { get; set; } = [];
    public List<string> OnTrack { get; set; } = [];
    public string? TopPerformer { get; set; }
    public double AverageHoursPercent { get; set; }
    public double AverageSkillRating { get; set; }
    public string CoordinatorRecommendation { get; set; } = string.Empty;
    public List<InternChartPoint> InternCharts { get; set; } = [];
}

public class SupervisorInsight
{
    public string SkillGapSummary { get; set; } = string.Empty;
    public string? StrongestSkill { get; set; }
    public string? WeakestSkill { get; set; }
    public string TaskPerformanceSummary { get; set; } = string.Empty;
    public string SuggestedFocus { get; set; } = string.Empty;
    public string OverallRating { get; set; } = string.Empty;
    public List<SkillScore> SkillScores { get; set; } = [];
    public List<TrendPoint> EvalTrend { get; set; } = [];
    public List<RatingPoint> TaskRatingTrend { get; set; } = [];
}

public class TaskBreakdownData
{
    public double Reviewed { get; set; }
    public double Submitted { get; set; }
    public double Rejected { get; set; }
    public double Pending { get; set; }
}

public class ChartPoint
{
    public string Skill { get; set; } = string.Empty;
    public double Rating { get; set; }
}

public class HoursPoint
{
    public string Label { get; set; } = string.Empty;
    public double Hours { get; set; }
}

public class InternChartPoint
{
    public string Name { get; set; } = string.Empty;
    public double Hours { get; set; }
    public double AvgSkill { get; set; }
}

public class TrendPoint
{
    public string Label { get; set; } = string.Empty;
    public double? AvgRating { get; set; }
}

public class RatingPoint
{
    public string Label { get; set; } = string.Empty;
    public double? Rating { get; set; }
}

public class SkillScore
{
    public string Skill { get; set; } = string.Empty;
    public double? Rating { get; set; }
    public string Comment { get; set; } = string.Empty;
}

public class InternSummary
{
    public string Name { get; set; } = string.Empty;
    public string Company { get; set; } = string.Empty;
    public int HoursPercent { get; set; }
    public int ReviewedTasks { get; set; }
    public int TotalTasks { get; set; }
    public double AvgSkill { get; set; }
    public int DaysLeft { get; set; }
}
