using System.Security.Claims;
using IMS.Web.Data;
using IMS.Web.Enums;
using IMS.Web.Models;
using IMS.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IMS.Web.Controllers;

[Authorize(Roles = "Coordinator")]
public class CoordinatorController(AppDbContext db, IWebHostEnvironment env, AIService ai) : Controller
{
    private int UserId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    public async Task<IActionResult> Dashboard()
    {
        ViewBag.TotalInternships  = await db.Internships.CountAsync();
        ViewBag.Pending           = await db.Internships.CountAsync(i => i.Status == InternshipStatus.Pending);
        ViewBag.Approved          = await db.Internships.CountAsync(i => i.Status == InternshipStatus.Approved);
        ViewBag.TotalStudents     = await db.Users.CountAsync(u => u.Role == Role.Student);
        ViewBag.TotalSupervisors  = await db.Users.CountAsync(u => u.Role == Role.Supervisor);
        ViewBag.OpenOpportunities = await db.InternshipOpportunities.CountAsync(o => o.IsOpen);

        var recent = await db.Internships
            .Include(i => i.Student)
            .Include(i => i.Supervisor)
            .OrderByDescending(i => i.CreatedAt)
            .Take(5).ToListAsync();

        ViewBag.Recent = recent;
        return View();
    }

    public async Task<IActionResult> Internships(string? status, string? search)
    {
        var query = db.Internships
            .Include(i => i.Student)
            .Include(i => i.Supervisor)
            .Include(i => i.Opportunity)
            .AsQueryable();

        if (Enum.TryParse<InternshipStatus>(status, out var parsed))
            query = query.Where(i => i.Status == parsed);
        else
            query = query.Where(i => i.Status != InternshipStatus.Cancelled);

        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(i => i.Student!.FullName.Contains(search)
                                  || i.CompanyName.Contains(search)
                                  || i.Position.Contains(search));

        ViewBag.CurrentStatus = status;
        ViewBag.CurrentSearch = search;
        ViewBag.Supervisors   = await db.Users
            .Where(u => u.Role == Role.Supervisor)
            .OrderBy(u => u.FullName)
            .ToListAsync();

        return View(await query.OrderByDescending(i => i.CreatedAt).ToListAsync());
    }

    [HttpPost]
    public async Task<IActionResult> ApproveInternship(int id, int? supervisorId,
        DateOnly? startDate, DateOnly? endDate)
    {
        var internship = await db.Internships.FindAsync(id);
        if (internship is null) return NotFound();

        internship.Status       = InternshipStatus.Approved;
        internship.SupervisorId = supervisorId;

        //  override dates if provided 
        if (startDate.HasValue) internship.StartDate = startDate;
        if (endDate.HasValue)   internship.EndDate   = endDate;

        await db.SaveChangesAsync();
        TempData["Success"] = "Internship approved.";
        return RedirectToAction("Internships");
    }

    [HttpPost]
    public async Task<IActionResult> UpdateStatus(int id, InternshipStatus status)
    {
        var internship = await db.Internships.FindAsync(id);
        if (internship is null) return NotFound();
        internship.Status = status;
        await db.SaveChangesAsync();
        TempData["Success"] = $"Internship {status.ToString().ToLower()}.";
        return RedirectToAction("Internships");
    }

    [HttpPost]
    public async Task<IActionResult> AssignSupervisor(int id, int? supervisorId)
    {
        var internship = await db.Internships.FindAsync(id);
        if (internship is null) return NotFound();
        internship.SupervisorId = supervisorId;
        await db.SaveChangesAsync();
        TempData["Success"] = "Supervisor assigned successfully.";
        return RedirectToAction("Internships");
    }

    [HttpPost]
    public async Task<IActionResult> RemoveStudent(int id)
    {
        var internship = await db.Internships.FindAsync(id);
        if (internship is null) return NotFound();

        internship.Status     = InternshipStatus.Cancelled;
        internship.SupervisorId = null;

        await db.SaveChangesAsync();
        TempData["Success"] = "Student removed from internship.";
        return RedirectToAction("Internships");
    }

    [HttpPost]
    public async Task<IActionResult> DeleteInternship(int id)
    {
        var internship = await db.Internships
            .Include(i => i.Assignments).ThenInclude(a => a.Report)
            .Include(i => i.ActivityReports)
            .Include(i => i.Evaluations).ThenInclude(e => e.EvaluationSkills)
            .FirstOrDefaultAsync(i => i.Id == id);

        if (internship is null) return NotFound();

        
        foreach (var e in internship.Evaluations)
            db.EvaluationSkills.RemoveRange(e.EvaluationSkills);

        db.Evaluations.RemoveRange(internship.Evaluations);

        
        foreach (var a in internship.Assignments)
        {
            if (a.Report is not null)
                db.ActivityReports.Remove(a.Report);
        }
        db.Assignments.RemoveRange(internship.Assignments);

        
        db.ActivityReports.RemoveRange(internship.ActivityReports);

        db.Internships.Remove(internship);
        await db.SaveChangesAsync();

        TempData["Success"] = "Internship deleted successfully.";
        return RedirectToAction("Internships");
    }

    public async Task<IActionResult> Opportunities()
    {
        var opportunities = await db.InternshipOpportunities
            .Include(o => o.Applications)
            .OrderByDescending(o => o.CreatedAt)
            .ToListAsync();

        return View(opportunities);
    }

    [HttpGet]
    public IActionResult PostOpportunity() => View();

    [HttpPost]
    public async Task<IActionResult> PostOpportunity(InternshipOpportunity model)
    {
        model.CoordinatorId = UserId;
        db.InternshipOpportunities.Add(model);
        await db.SaveChangesAsync();
        TempData["Success"] = "Opportunity posted successfully.";
        return RedirectToAction("Opportunities");
    }

    [HttpPost]
    public async Task<IActionResult> ToggleOpportunity(int id)
    {
        var opp = await db.InternshipOpportunities.FindAsync(id);
        if (opp is null) return NotFound();
        opp.IsOpen = !opp.IsOpen;
        await db.SaveChangesAsync();
        return RedirectToAction("Opportunities");
    }

    [HttpPost]
    public async Task<IActionResult> DeleteOpportunity(int id)
    {
        var opp = await db.InternshipOpportunities.FindAsync(id);
        if (opp is null) return NotFound();
        db.InternshipOpportunities.Remove(opp);
        await db.SaveChangesAsync();
        TempData["Success"] = "Opportunity deleted.";
        return RedirectToAction("Opportunities");
    }

    [HttpGet]
    public async Task<IActionResult> EditOpportunity(int id)
    {
        var opp = await db.InternshipOpportunities.FindAsync(id);
        if (opp is null) return NotFound();
        return View(opp);
    }

    [HttpPost]
    public async Task<IActionResult> EditOpportunity(int id, string title, string companyName,
        string location, string description, DateOnly startDate, DateOnly endDate,
        DateOnly applicationDeadline, int maxStudents)
    {
        var opp = await db.InternshipOpportunities.FindAsync(id);
        if (opp is null) return NotFound();

        opp.Title                = title;
        opp.CompanyName          = companyName;
        opp.Location             = location;
        opp.Description          = description;
        opp.StartDate            = startDate;
        opp.EndDate              = endDate;
        opp.ApplicationDeadline  = applicationDeadline;
        opp.MaxStudents          = maxStudents;

        await db.SaveChangesAsync();
        TempData["Success"] = "Opportunity updated successfully.";
        return RedirectToAction("Opportunities");
    }

    [HttpGet]
    public async Task<IActionResult> EditDates(int id)
    {
        var internship = await db.Internships
            .Include(i => i.Student)
            .FirstOrDefaultAsync(i => i.Id == id);
        if (internship is null) return NotFound();
        return View(internship);
    }

    [HttpPost]
    public async Task<IActionResult> EditDates(int id, DateOnly startDate, DateOnly endDate)
    {
        var internship = await db.Internships.FindAsync(id);
        if (internship is null) return NotFound();

        internship.StartDate = startDate;
        internship.EndDate   = endDate;

        await db.SaveChangesAsync();
        TempData["Success"] = "Dates updated successfully.";
        return RedirectToAction("Internships");
    }

    public async Task<IActionResult> InternDetail(int internshipId)
    {
        var internship = await db.Internships
            .Include(i => i.Student)
            .Include(i => i.Supervisor)
            .FirstOrDefaultAsync(i => i.Id == internshipId);

        if (internship is null) return NotFound();

        var assignments = await db.Assignments
            .Include(a => a.Report)
            .Where(a => a.InternshipId == internshipId)
            .OrderByDescending(a => a.CreatedAt)
            .ToListAsync();

        var evaluations = await db.Evaluations
            .Include(e => e.EvaluationSkills).ThenInclude(es => es.Skill)
            .Include(e => e.Supervisor)
            .Where(e => e.InternshipId == internshipId)
            .OrderByDescending(e => e.EvaluatedAt)
            .ToListAsync();

        ViewBag.Internship   = internship;
        ViewBag.Assignments  = assignments;
        ViewBag.Evaluations  = evaluations;
        return View();
    }

    public async Task<IActionResult> Reports()
    {
        var reports = await db.ActivityReports
            .Include(r => r.Internship).ThenInclude(i => i.Student)
            .OrderByDescending(r => r.SubmittedAt)
            .ToListAsync();
        return View(reports);
    }

    public async Task<IActionResult> DownloadReport(int reportId)
    {
        var report = await db.ActivityReports.FindAsync(reportId);
        if (report is null || report.AttachmentPath is null) return NotFound();

        var filePath = Path.Combine(env.WebRootPath, "uploads", "reports", report.AttachmentPath);
        if (!System.IO.File.Exists(filePath)) return NotFound();

        var bytes = await System.IO.File.ReadAllBytesAsync(filePath);
        return File(bytes, "application/octet-stream", report.AttachmentName ?? "report");
    }

    public async Task<IActionResult> Evaluations()
    {
        var evaluations = await db.Evaluations
            .Include(e => e.Internship).ThenInclude(i => i.Student)
            .Include(e => e.Supervisor)
            .Include(e => e.EvaluationSkills).ThenInclude(es => es.Skill)
            .OrderByDescending(e => e.EvaluatedAt)
            .ToListAsync();
        return View(evaluations);
    }

    [HttpGet]
    public async Task<IActionResult> GetInternAIInsight(int internshipId)
    {
        var internship = await db.Internships
            .Include(i => i.Student)
            .Include(i => i.Supervisor)
            .Include(i => i.Assignments).ThenInclude(a => a.Report)
            .Include(i => i.ActivityReports)
            .Include(i => i.Evaluations).ThenInclude(e => e.EvaluationSkills).ThenInclude(es => es.Skill)
            .FirstOrDefaultAsync(i => i.Id == internshipId);

        if (internship is null) return Json(new { error = "Internship not found." });

        int required = 300;
        int totalDays = 90;
        if (internship.StartDate is not null && internship.EndDate is not null)
        {
            totalDays = Math.Max(1, (internship.EndDate.Value.ToDateTime(TimeOnly.MinValue)
                      - internship.StartDate.Value.ToDateTime(TimeOnly.MinValue)).Days);
            required  = Math.Max(1, (int)(totalDays * 5.0 / 7.0 * 8));
        }

        var hours = internship.ActivityReports
            .Where(r => r.Status != ReportStatus.Rejected)
            .Sum(r => r.HoursWorked);

        var skillRatings = internship.Evaluations
            .SelectMany(e => e.EvaluationSkills)
            .GroupBy(es => es.Skill?.Name ?? "Unknown")
            .Select(g => (Skill: g.Key, Rating: g.Average(es => es.Rating)))
            .ToList();

        int daysLeft = internship.EndDate.HasValue
            ? Math.Max(0, (internship.EndDate.Value.ToDateTime(TimeOnly.MinValue) - DateTime.Today).Days)
            : 0;

        var hoursTimeline = internship.ActivityReports
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
        double dailyRate      = (double)hours / daysElapsed;
        int    projectedHours = (int)(dailyRate * totalDays);
        int    projectedPct   = (int)Math.Min(100, projectedHours * 100.0 / required);

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
            var h      = i.ActivityReports.Where(r => r.Status != ReportStatus.Rejected).Sum(r => r.HoursWorked);
            var hPct   = Math.Min(100, h * 100.0 / req);
            var skill  = i.Evaluations.SelectMany(e => e.EvaluationSkills).Select(es => (double)es.Rating).DefaultIfEmpty(0).Average();
            var tasks  = i.Assignments.Count > 0
                ? i.Assignments.Count(a => a.Report?.Status == ReportStatus.Reviewed) * 100.0 / i.Assignments.Count
                : 0.0;
            return (Id: i.Id, HoursPct: hPct, AvgSkill: skill, TaskRate: tasks);
        }).ToList();

        double cohortAvgHours = cohortStats.Any() ? cohortStats.Average(s => s.HoursPct) : 0;
        double cohortAvgSkill = cohortStats.Any() ? cohortStats.Average(s => s.AvgSkill) : 0;
        double cohortAvgTask  = cohortStats.Any() ? cohortStats.Average(s => s.TaskRate)  : 0;

        int hoursPercent  = required > 0 ? (int)Math.Min(100, hours * 100.0 / required) : 0;
        double myAvgSkill = skillRatings.Any() ? skillRatings.Average(s => s.Rating) : 0;

        
        var ranked = cohortStats
            .OrderByDescending(s => s.HoursPct * 0.4 + s.TaskRate * 0.4 + s.AvgSkill * 20 * 0.2)
            .Select((s, idx) => (s.Id, Rank: idx + 1))
            .ToList();
        int myRank        = ranked.FirstOrDefault(r => r.Id == internshipId).Rank;
        int totalStudents = cohortStats.Count;

        var aboveAvg = skillRatings.Where(s => s.Rating > cohortAvgSkill).Select(s => s.Skill).ToList();
        var belowAvg = skillRatings.Where(s => s.Rating < cohortAvgSkill).Select(s => s.Skill).ToList();

        try
        {
            var insight = await ai.GetStudentInsight(
                internship.Student?.FullName ?? "Student",
                internship.CompanyName, internship.Position,
                hours, required,
                internship.Assignments.Count,
                internship.Assignments.Count(a => a.Report?.Status == ReportStatus.Reviewed),
                internship.Assignments.Count(a => a.Report?.Status == ReportStatus.Rejected),
                internship.Assignments.Count(a => a.Report?.Status == ReportStatus.Submitted),
                internship.Assignments.Count(a => a.Report is null),
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

    [HttpGet]
    public async Task<IActionResult> GetAIInsight()
    {
        var internships = await db.Internships
            .Include(i => i.Student)
            .Include(i => i.Assignments).ThenInclude(a => a.Report)
            .Include(i => i.ActivityReports)
            .Include(i => i.Evaluations).ThenInclude(e => e.EvaluationSkills)
            .Where(i => i.Status == InternshipStatus.Approved)
            .ToListAsync();

        if (!internships.Any())
            return Json(new { error = "No approved internships yet." });

        var summaries = internships.Select(i =>
        {
            int required = 300;
            if (i.StartDate is not null && i.EndDate is not null)
            {
                var days = (i.EndDate.Value.ToDateTime(TimeOnly.MinValue)
                          - i.StartDate.Value.ToDateTime(TimeOnly.MinValue)).Days;
                required = Math.Max(1, (int)(days * 5.0 / 7.0 * 8));
            }

            var hours = i.ActivityReports
                .Where(r => r.Status != ReportStatus.Rejected)
                .Sum(r => r.HoursWorked);

            var avgSkill = i.Evaluations
                .SelectMany(e => e.EvaluationSkills)
                .Select(es => (double)es.Rating)
                .DefaultIfEmpty(0)
                .Average();

            var daysLeft = i.EndDate.HasValue
                ? Math.Max(0, (i.EndDate.Value.ToDateTime(TimeOnly.MinValue) - DateTime.Today).Days)
                : 0;

            return new InternSummary
            {
                Name          = i.Student?.FullName ?? "Unknown",
                Company       = i.CompanyName,
                HoursPercent  = (int)Math.Min(100, hours * 100.0 / required),
                ReviewedTasks = i.Assignments.Count(a => a.Report?.Status == ReportStatus.Reviewed),
                TotalTasks    = i.Assignments.Count,
                AvgSkill      = Math.Round(avgSkill, 1),
                DaysLeft      = daysLeft
            };
        }).ToList();

        try
        {
            var insight = await ai.GetCoordinatorInsight(summaries);
            return Json(insight);
        }
        catch (Exception ex)
        {
            return Json(new { error = $"AI service error: {ex.Message}" });
        }
    }
}
