using System.Security.Claims;
using IMS.Web.Data;
using IMS.Web.Enums;
using IMS.Web.Models;
using IMS.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IMS.Web.Controllers;

[Authorize(Roles = "Student")]
public class StudentController(AppDbContext db, IWebHostEnvironment env, AIService ai) : Controller
{
    private int UserId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    private Task<Internship?> GetActiveInternship() =>
        db.Internships
          .Include(i => i.Supervisor)
          .FirstOrDefaultAsync(i => i.StudentId == UserId
                                 && i.Status != InternshipStatus.Cancelled);

    private Task<bool> HasActiveInternship() =>
        db.Internships.AnyAsync(i => i.StudentId == UserId
                                  && i.Status != InternshipStatus.Cancelled);

    public async Task<IActionResult> Dashboard()
    {
        var internship = await GetActiveInternship();

        var assignments = internship is null ? [] : await db.Assignments
            .Include(a => a.Report)
            .Where(a => a.InternshipId == internship.Id)
            .OrderByDescending(a => a.CreatedAt)
            .Take(5).ToListAsync();

        var evaluations = internship is null ? [] : await db.Evaluations
            .Include(e => e.EvaluationSkills).ThenInclude(es => es.Skill)
            .Where(e => e.InternshipId == internship.Id)
            .OrderByDescending(e => e.EvaluatedAt)
            .Take(3).ToListAsync();

        var totalHoursWorked = internship is null ? 0 : await db.ActivityReports
            .Where(r => r.InternshipId == internship.Id
                     && r.Status != Enums.ReportStatus.Rejected)
            .SumAsync(r => (int?)r.HoursWorked) ?? 0;

        int requiredHours = 300;
        if (internship?.StartDate is not null && internship?.EndDate is not null)
        {
            var days = (internship.EndDate.Value.ToDateTime(TimeOnly.MinValue)
                      - internship.StartDate.Value.ToDateTime(TimeOnly.MinValue)).Days;
            requiredHours = Math.Max(1, (int)(days * 5.0 / 7.0 * 8));
        }

        var wasCancelled = internship is null &&
            await db.Internships.AnyAsync(i => i.StudentId == UserId
                                           && i.Status == InternshipStatus.Cancelled);

        ViewBag.Internship        = internship;
        ViewBag.Assignments       = assignments;
        ViewBag.Evaluations       = evaluations;
        ViewBag.WasCancelled      = wasCancelled;
        ViewBag.TotalHoursWorked  = totalHoursWorked;
        ViewBag.RequiredHours     = requiredHours;
        return View();
    }

    [HttpGet]
    public async Task<IActionResult> ApplyInternship()
    {
        var internship = await GetActiveInternship();

        if (internship is not null)
        {
            ViewBag.Internship = internship;
            return View("MyInternship");
        }

        return View();
    }

    [HttpPost]
    public async Task<IActionResult> ApplyInternship(Internship model)
    {
        if (await HasActiveInternship()) return RedirectToAction("Dashboard");

        model.StudentId = UserId;
        model.Status    = InternshipStatus.Pending;
        db.Internships.Add(model);
        await db.SaveChangesAsync();
        TempData["Success"] = "Internship application submitted.";
        return RedirectToAction("Dashboard");
    }

    public async Task<IActionResult> Opportunities()
    {
        var opportunities = await db.InternshipOpportunities
            .Include(o => o.Applications)
            .Where(o => o.IsOpen && o.ApplicationDeadline >= DateOnly.FromDateTime(DateTime.Today))
            .OrderByDescending(o => o.CreatedAt)
            .ToListAsync();

        var appliedIds = await db.Internships
            .Where(i => i.StudentId == UserId
                     && i.OpportunityId != null
                     && i.Status != InternshipStatus.Cancelled
                     && i.Status != InternshipStatus.Rejected)
            .Select(i => i.OpportunityId!.Value)
            .ToListAsync();

        ViewBag.HasActiveInternship = await HasActiveInternship();

        ViewBag.AppliedIds = appliedIds;
        return View(opportunities);
    }

    [HttpPost]
    public async Task<IActionResult> ApplyToOpportunity(int opportunityId)
    {
        if (await HasActiveInternship()) return RedirectToAction("Dashboard");

        var opp = await db.InternshipOpportunities
            .Include(o => o.Applications)
            .FirstOrDefaultAsync(o => o.Id == opportunityId);

        if (opp is null || !opp.IsOpen) return NotFound();

        var acceptedCount = opp.Applications.Count(a => a.Status != InternshipStatus.Rejected
                                                      && a.Status != InternshipStatus.Cancelled);
        if (acceptedCount >= opp.MaxStudents)
        {
            TempData["Error"] = "This internship is full. No more spots available.";
            return RedirectToAction("Opportunities");
        }

        db.Internships.Add(new Internship
        {
            StudentId     = UserId,
            OpportunityId = opportunityId,
            CompanyName   = opp.CompanyName,
            Position      = opp.Title,
            StartDate     = opp.StartDate,
            EndDate       = opp.EndDate,
            Status        = InternshipStatus.Pending
        });

        await db.SaveChangesAsync();
        TempData["Success"] = "Application submitted successfully!";
        return RedirectToAction("Dashboard");
    }

    [HttpPost]
    public async Task<IActionResult> DismissRejection(int internshipId)
    {
        var internship = await db.Internships
            .FirstOrDefaultAsync(i => i.Id == internshipId
                                   && i.StudentId == UserId
                                   && i.Status == InternshipStatus.Rejected);

        if (internship is null) return NotFound();

        internship.Status = InternshipStatus.Cancelled;
        await db.SaveChangesAsync();
        TempData["Success"] = "Rejection dismissed. You can now apply for a new internship.";
        return RedirectToAction("Dashboard");
    }

    [HttpPost]
    public async Task<IActionResult> WithdrawApplication(int internshipId)
    {
        var internship = await db.Internships
            .FirstOrDefaultAsync(i => i.Id == internshipId
                                   && i.StudentId == UserId
                                   && i.Status == InternshipStatus.Pending);

        if (internship is null) return NotFound();

        internship.Status = InternshipStatus.Cancelled;
        await db.SaveChangesAsync();
        TempData["Success"] = "Application withdrawn successfully.";
        return RedirectToAction("Dashboard");
    }

    public async Task<IActionResult> Reports()
    {
        var internship = await GetActiveInternship();
        if (internship is null) return RedirectToAction("Dashboard");

        var assignments = await db.Assignments
            .Include(a => a.Report)
            .Where(a => a.InternshipId == internship.Id)
            .OrderByDescending(a => a.CreatedAt)
            .ToListAsync();

        return View(assignments);
    }

    public async Task<IActionResult> DownloadTaskFile(int assignmentId)
    {
        var internship = await GetActiveInternship();
        if (internship is null) return Forbid();

        var assignment = await db.Assignments
            .FirstOrDefaultAsync(a => a.Id == assignmentId && a.InternshipId == internship.Id);

        if (assignment is null || assignment.AttachmentPath is null) return NotFound();

        var filePath = Path.Combine(env.WebRootPath, "uploads", "tasks", assignment.AttachmentPath);
        if (!System.IO.File.Exists(filePath)) return NotFound();

        var bytes = await System.IO.File.ReadAllBytesAsync(filePath);
        return File(bytes, "application/octet-stream", assignment.AttachmentName ?? "download");
    }

    [HttpGet]
    public async Task<IActionResult> SubmitReport(int assignmentId)
    {
        var internship = await GetActiveInternship();
        if (internship is null) return RedirectToAction("Dashboard");

        var assignment = await db.Assignments
            .Include(a => a.Report)
            .FirstOrDefaultAsync(a => a.Id == assignmentId && a.InternshipId == internship.Id);

        if (assignment is null) return NotFound();

        if (assignment.Report is not null && assignment.Report.Status != ReportStatus.Rejected)
            return RedirectToAction("Reports");

        ViewBag.Assignment   = assignment;
        ViewBag.InternshipId = internship.Id;
        ViewBag.Categories   = Enum.GetValues<Enums.ActivityCategory>();
        return View();
    }

    [HttpPost]
    public async Task<IActionResult> SubmitReport(int InternshipId, int AssignmentId,
        DateOnly ReportDate, int HoursWorked, Enums.ActivityCategory Category,
        string TasksCompleted, string? KeyLearnings, string? Notes,
        IFormFile? attachment)
    {
        var internship = await GetActiveInternship();
        if (internship is null) return RedirectToAction("Dashboard");

        var assignment = await db.Assignments
            .Include(a => a.Report)
            .FirstOrDefaultAsync(a => a.Id == AssignmentId && a.InternshipId == InternshipId);

        if (assignment is null) return NotFound();

        if (assignment.Report is not null && assignment.Report.Status == ReportStatus.Rejected)
        {
            var existing = assignment.Report;
            existing.ReportDate     = ReportDate;
            existing.HoursWorked    = HoursWorked;
            existing.Category       = Category;
            existing.TasksCompleted = TasksCompleted;
            existing.KeyLearnings   = KeyLearnings ?? string.Empty;
            existing.Notes          = Notes ?? string.Empty;
            existing.Status         = ReportStatus.Submitted;
            existing.RejectionReason = null;
            existing.SubmittedAt    = DateTime.UtcNow;

            if (attachment is { Length: > 0 })
            {
                if (existing.AttachmentPath is not null)
                {
                    var oldFile = Path.Combine(env.WebRootPath, "uploads", "reports", existing.AttachmentPath);
                    if (System.IO.File.Exists(oldFile)) System.IO.File.Delete(oldFile);
                }
                var uploadsDir = Path.Combine(env.WebRootPath, "uploads", "reports");
                Directory.CreateDirectory(uploadsDir);
                var uniqueName = $"{Guid.NewGuid()}{Path.GetExtension(attachment.FileName)}";
                using var s = System.IO.File.Create(Path.Combine(uploadsDir, uniqueName));
                await attachment.CopyToAsync(s);
                existing.AttachmentPath = uniqueName;
                existing.AttachmentName = attachment.FileName;
            }

            await db.SaveChangesAsync();
            TempData["Success"] = "Report resubmitted successfully.";
            return RedirectToAction("Reports");
        }

        var report = new ActivityReport
        {
            InternshipId   = InternshipId,
            AssignmentId   = AssignmentId,
            ReportDate     = ReportDate,
            HoursWorked    = HoursWorked,
            Category       = Category,
            TasksCompleted = TasksCompleted,
            KeyLearnings   = KeyLearnings ?? string.Empty,
            Notes          = Notes ?? string.Empty,
            Status         = ReportStatus.Submitted,
            SubmittedAt    = DateTime.UtcNow
        };

        if (attachment is { Length: > 0 })
        {
            var uploadsDir = Path.Combine(env.WebRootPath, "uploads", "reports");
            Directory.CreateDirectory(uploadsDir);

            var uniqueName = $"{Guid.NewGuid()}{Path.GetExtension(attachment.FileName)}";
            var filePath   = Path.Combine(uploadsDir, uniqueName);

            using var stream = System.IO.File.Create(filePath);
            await attachment.CopyToAsync(stream);

            report.AttachmentPath = uniqueName;
            report.AttachmentName = attachment.FileName;
        }

        db.ActivityReports.Add(report);
        await db.SaveChangesAsync();
        TempData["Success"] = "Report submitted successfully.";
        return RedirectToAction("Reports");
    }

    public async Task<IActionResult> Evaluations()
    {
        var internship = await GetActiveInternship();
        if (internship is null) return RedirectToAction("Dashboard");

        var evaluations = await db.Evaluations
            .Include(e => e.EvaluationSkills).ThenInclude(es => es.Skill)
            .Include(e => e.Supervisor)
            .Where(e => e.InternshipId == internship.Id)
            .OrderByDescending(e => e.EvaluatedAt)
            .ToListAsync();

        return View(evaluations);
    }

    [HttpGet]
    public async Task<IActionResult> GetAIInsight()
    {
        var internship = await db.Internships
            .Include(i => i.Student)
            .FirstOrDefaultAsync(i => i.StudentId == UserId && i.Status != InternshipStatus.Cancelled);
        if (internship is null)
            return Json(new { error = "No active internship." });

        var allReports = await db.ActivityReports
            .Where(r => r.InternshipId == internship.Id)
            .OrderBy(r => r.ReportDate)
            .ToListAsync();

        var totalHours = allReports
            .Where(r => r.Status != ReportStatus.Rejected)
            .Sum(r => r.HoursWorked);

        int requiredHours = 300;
        if (internship.StartDate is not null && internship.EndDate is not null)
        {
            var days = (internship.EndDate.Value.ToDateTime(TimeOnly.MinValue)
                      - internship.StartDate.Value.ToDateTime(TimeOnly.MinValue)).Days;
            requiredHours = Math.Max(1, (int)(days * 5.0 / 7.0 * 8));
        }

        var assignments = await db.Assignments
            .Include(a => a.Report)
            .Where(a => a.InternshipId == internship.Id)
            .ToListAsync();

        var evaluations = await db.Evaluations
            .Include(e => e.EvaluationSkills).ThenInclude(es => es.Skill)
            .Where(e => e.InternshipId == internship.Id)
            .ToListAsync();

        var skillRatings = evaluations
            .SelectMany(e => e.EvaluationSkills)
            .GroupBy(es => es.Skill?.Name ?? "Unknown")
            .Select(g => (Skill: g.Key, Rating: g.Average(es => es.Rating)))
            .ToList();

        int daysLeft = internship.EndDate.HasValue
            ? Math.Max(0, (internship.EndDate.Value.ToDateTime(TimeOnly.MinValue) - DateTime.Today).Days)
            : 0;

        int totalDays = (internship.StartDate is not null && internship.EndDate is not null)
            ? Math.Max(1, (internship.EndDate.Value.ToDateTime(TimeOnly.MinValue)
                         - internship.StartDate.Value.ToDateTime(TimeOnly.MinValue)).Days)
            : 90;

        var hoursTimeline = allReports
            .Where(r => r.Status != ReportStatus.Rejected)
            .GroupBy(r => new { r.ReportDate.Year, r.ReportDate.Month })
            .OrderBy(g => g.Key.Year).ThenBy(g => g.Key.Month)
            .Select(g => (
                Label: new DateOnly(g.Key.Year, g.Key.Month, 1).ToString("MMM yyyy"),
                Hours: g.Sum(r => r.HoursWorked)
            ))
            .ToList();

        int daysElapsed = internship.StartDate.HasValue
            ? Math.Max(1, (DateTime.Today - internship.StartDate.Value.ToDateTime(TimeOnly.MinValue)).Days)
            : 1;
        double dailyRate      = (double)totalHours / daysElapsed;
        int    projectedHours = (int)(dailyRate * totalDays);
        int    projectedPct   = (int)Math.Min(100, projectedHours * 100.0 / requiredHours);

        var allInternships = await db.Internships
            .Include(i => i.Assignments).ThenInclude(a => a.Report)
            .Include(i => i.ActivityReports)
            .Include(i => i.Evaluations).ThenInclude(e => e.EvaluationSkills)
            .Where(i => i.Status == InternshipStatus.Approved)
            .ToListAsync();

        var cohortStats = allInternships.Select(i =>
        {
            int req = 300;
            if (i.StartDate is not null && i.EndDate is not null)
            {
                var d = Math.Max(1, (i.EndDate.Value.ToDateTime(TimeOnly.MinValue)
                              - i.StartDate.Value.ToDateTime(TimeOnly.MinValue)).Days);
                req = Math.Max(1, (int)(d * 5.0 / 7.0 * 8));
            }
            var h     = i.ActivityReports.Where(r => r.Status != ReportStatus.Rejected).Sum(r => r.HoursWorked);
            var hPct  = Math.Min(100, h * 100.0 / req);
            var skill = i.Evaluations.SelectMany(e => e.EvaluationSkills).Select(es => (double)es.Rating).DefaultIfEmpty(0).Average();
            var tasks = i.Assignments.Count > 0
                ? i.Assignments.Count(a => a.Report?.Status == ReportStatus.Reviewed) * 100.0 / i.Assignments.Count
                : 0.0;
            return (Id: i.Id, HoursPct: hPct, AvgSkill: skill, TaskRate: tasks);
        }).ToList();

        double cohortAvgHours = cohortStats.Any() ? cohortStats.Average(s => s.HoursPct) : 0;
        double cohortAvgSkill = cohortStats.Any() ? cohortStats.Average(s => s.AvgSkill) : 0;
        double cohortAvgTask  = cohortStats.Any() ? cohortStats.Average(s => s.TaskRate)  : 0;

        int hoursPercent  = requiredHours > 0 ? (int)Math.Min(100, totalHours * 100.0 / requiredHours) : 0;
        double myAvgSkill = skillRatings.Any() ? skillRatings.Average(s => s.Rating) : 0;

        var ranked = cohortStats
            .OrderByDescending(s => s.HoursPct * 0.4 + s.TaskRate * 0.4 + s.AvgSkill * 20 * 0.2)
            .Select((s, idx) => (s.Id, Rank: idx + 1))
            .ToList();
        int myRank        = ranked.FirstOrDefault(r => r.Id == internship.Id).Rank;
        int totalStudents = cohortStats.Count;

        var aboveAvg = skillRatings.Where(s => s.Rating > cohortAvgSkill).Select(s => s.Skill).ToList();
        var belowAvg = skillRatings.Where(s => s.Rating < cohortAvgSkill).Select(s => s.Skill).ToList();

        try
        {
            var insight = await ai.GetStudentInsight(
                internship.Student?.FullName ?? "Student",
                internship.CompanyName, internship.Position,
                totalHours, requiredHours,
                assignments.Count,
                assignments.Count(a => a.Report?.Status == ReportStatus.Reviewed),
                assignments.Count(a => a.Report?.Status == ReportStatus.Rejected),
                assignments.Count(a => a.Report?.Status == ReportStatus.Submitted),
                assignments.Count(a => a.Report is null),
                skillRatings, daysLeft, hoursTimeline,
                daysElapsed, totalDays, dailyRate,
                projectedHours, projectedPct,
                myRank, totalStudents,
                cohortAvgHours, cohortAvgSkill, cohortAvgTask,
                aboveAvg, belowAvg);

            return Json(insight);
        }
        catch (Exception ex)
        {
            return Json(new { error = $"AI service error: {ex.Message}" });
        }
    }
}
