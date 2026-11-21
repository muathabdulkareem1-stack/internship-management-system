using System.Security.Claims;
using IMS.Web.Data;
using IMS.Web.Enums;
using IMS.Web.Models;
using IMS.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IMS.Web.Controllers;

[Authorize(Roles = "Supervisor")]
public class SupervisorController(AppDbContext db, IWebHostEnvironment env, AIService ai) : Controller
{
    private int UserId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    public async Task<IActionResult> Dashboard()
    {
        var interns = await db.Internships
            .Include(i => i.Student)
            .Where(i => i.SupervisorId == UserId && i.Status == InternshipStatus.Approved)
            .ToListAsync();

        var pendingReports = await db.ActivityReports
            .Include(r => r.Internship)
            .Where(r => r.Internship.SupervisorId == UserId && r.Status == ReportStatus.Submitted)
            .CountAsync();

        ViewBag.Interns = interns;
        ViewBag.PendingReports = pendingReports;
        return View();
    }

    // interns
    public async Task<IActionResult> Interns()
    {
        var interns = await db.Internships
            .Include(i => i.Student)
            .Where(i => i.SupervisorId == UserId)
            .ToListAsync();
        return View(interns);
    }

    // assigments
    [HttpGet]
    public async Task<IActionResult> AssignTask(int internshipId)
    {
        var internship = await db.Internships
            .Include(i => i.Student)
            .FirstOrDefaultAsync(i => i.Id == internshipId && i.SupervisorId == UserId);

        if (internship is null) return Forbid();

        ViewBag.Internship = internship;
        return View();
    }

    [HttpPost]
    public async Task<IActionResult> AssignTask(int internshipId, string Title,
        string Description, DateOnly DueDate, IFormFile? attachment)
    {
        var internship = await db.Internships
            .FirstOrDefaultAsync(i => i.Id == internshipId && i.SupervisorId == UserId);

        if (internship is null) return Forbid();

        var assignment = new Assignment
        {
            InternshipId = internshipId,
            Title        = Title,
            Description  = Description,
            DueDate      = DueDate
        };

        
        if (attachment is { Length: > 0 })
        {
            var uploadsDir = Path.Combine(env.WebRootPath, "uploads", "tasks");
            Directory.CreateDirectory(uploadsDir);

            var uniqueName = $"{Guid.NewGuid()}{Path.GetExtension(attachment.FileName)}";
            var filePath   = Path.Combine(uploadsDir, uniqueName);

            using var stream = System.IO.File.Create(filePath);
            await attachment.CopyToAsync(stream);

            assignment.AttachmentPath = uniqueName;
            assignment.AttachmentName = attachment.FileName;
        }

        db.Assignments.Add(assignment);
        await db.SaveChangesAsync();

        TempData["Success"] = "Task assigned successfully.";
        return RedirectToAction("InternReports", new { internshipId });
    }

    // reports 
    public async Task<IActionResult> InternReports(int internshipId)
    {
        var internship = await db.Internships
            .Include(i => i.Student)
            .FirstOrDefaultAsync(i => i.Id == internshipId && i.SupervisorId == UserId);

        if (internship is null) return Forbid();

        var assignments = await db.Assignments
            .Include(a => a.Report)
            .Where(a => a.InternshipId == internshipId)
            .OrderByDescending(a => a.CreatedAt)
            .ToListAsync();

        ViewBag.Internship = internship;
        return View(assignments);
    }

    [HttpGet]
    public async Task<IActionResult> EditTask(int assignmentId)
    {
        var assignment = await db.Assignments
            .Include(a => a.Internship).ThenInclude(i => i.Student)
            .FirstOrDefaultAsync(a => a.Id == assignmentId && a.Internship.SupervisorId == UserId);

        if (assignment is null) return NotFound();
        return View(assignment);
    }

    [HttpPost]
    public async Task<IActionResult> EditTask(int assignmentId, string Title,
        string Description, DateOnly DueDate, IFormFile? attachment)
    {
        var assignment = await db.Assignments
            .Include(a => a.Internship)
            .FirstOrDefaultAsync(a => a.Id == assignmentId && a.Internship.SupervisorId == UserId);

        if (assignment is null) return NotFound();

        assignment.Title       = Title;
        assignment.Description = Description;
        assignment.DueDate     = DueDate;

        
        if (attachment is { Length: > 0 })
        {
            
            if (assignment.AttachmentPath is not null)
            {
                var oldFile = Path.Combine(env.WebRootPath, "uploads", "tasks", assignment.AttachmentPath);
                if (System.IO.File.Exists(oldFile)) System.IO.File.Delete(oldFile);
            }

            var uploadsDir = Path.Combine(env.WebRootPath, "uploads", "tasks");
            Directory.CreateDirectory(uploadsDir);

            var uniqueName = $"{Guid.NewGuid()}{Path.GetExtension(attachment.FileName)}";
            var filePath   = Path.Combine(uploadsDir, uniqueName);

            using var stream = System.IO.File.Create(filePath);
            await attachment.CopyToAsync(stream);

            assignment.AttachmentPath = uniqueName;
            assignment.AttachmentName = attachment.FileName;
        }

        await db.SaveChangesAsync();
        TempData["Success"] = "Task updated successfully.";
        return RedirectToAction("InternReports", new { internshipId = assignment.InternshipId });
    }

    [HttpPost]
    public async Task<IActionResult> DeleteTask(int assignmentId)
    {
        var assignment = await db.Assignments
            .Include(a => a.Report)
            .FirstOrDefaultAsync(a => a.Id == assignmentId && a.Internship.SupervisorId == UserId);

        if (assignment is null) return NotFound();

        
        if (assignment.AttachmentPath is not null)
        {
            var taskFile = Path.Combine(env.WebRootPath, "uploads", "tasks", assignment.AttachmentPath);
            if (System.IO.File.Exists(taskFile)) System.IO.File.Delete(taskFile);
        }

        
        if (assignment.Report?.AttachmentPath is not null)
        {
            var reportFile = Path.Combine(env.WebRootPath, "uploads", "reports", assignment.Report.AttachmentPath);
            if (System.IO.File.Exists(reportFile)) System.IO.File.Delete(reportFile);
        }

        db.Assignments.Remove(assignment);
        await db.SaveChangesAsync();

        TempData["Success"] = "Task deleted.";
        return RedirectToAction("InternReports", new { internshipId = assignment.InternshipId });
    }

    [HttpPost]
    public async Task<IActionResult> ReviewReport(int reportId)
    {
        var report = await db.ActivityReports.FindAsync(reportId);
        if (report is null) return NotFound();

        report.Status = ReportStatus.Reviewed;
        await db.SaveChangesAsync();

        return RedirectToAction("InternReports", new { internshipId = report.InternshipId });
    }

    [HttpPost]
    public async Task<IActionResult> RejectReport(int reportId, string? rejectionReason)
    {
        var report = await db.ActivityReports
            .Include(r => r.Internship)
            .FirstOrDefaultAsync(r => r.Id == reportId && r.Internship.SupervisorId == UserId);

        if (report is null) return NotFound();

        report.Status          = ReportStatus.Rejected;
        report.RejectionReason = rejectionReason?.Trim();
        await db.SaveChangesAsync();

        TempData["Success"] = "Report rejected. The student will be notified to resubmit.";
        return RedirectToAction("InternReports", new { internshipId = report.InternshipId });
    }

    
    public async Task<IActionResult> DownloadReport(int reportId)
    {
        var report = await db.ActivityReports
            .Include(r => r.Internship)
            .FirstOrDefaultAsync(r => r.Id == reportId && r.Internship.SupervisorId == UserId);

        if (report is null || report.AttachmentPath is null) return NotFound();

        var filePath = Path.Combine(env.WebRootPath, "uploads", "reports", report.AttachmentPath);
        if (!System.IO.File.Exists(filePath)) return NotFound();

        var bytes = await System.IO.File.ReadAllBytesAsync(filePath);
        return File(bytes, "application/octet-stream", report.AttachmentName ?? "download");
    }

    // task evaluation 
    [HttpGet]
    public async Task<IActionResult> EvaluateTask(int assignmentId)
    {
        var assignment = await db.Assignments
            .Include(a => a.Report)
            .Include(a => a.Internship).ThenInclude(i => i.Student)
            .FirstOrDefaultAsync(a => a.Id == assignmentId && a.Internship.SupervisorId == UserId);

        if (assignment is null) return NotFound();
        if (assignment.Report is null) return RedirectToAction("InternReports", new { internshipId = assignment.InternshipId });

        return View(assignment);
    }

    [HttpPost]
    public async Task<IActionResult> EvaluateTask(int assignmentId, int rating, string feedback)
    {
        var assignment = await db.Assignments
            .Include(a => a.Internship)
            .FirstOrDefaultAsync(a => a.Id == assignmentId && a.Internship.SupervisorId == UserId);

        if (assignment is null) return NotFound();

        assignment.Rating      = rating;
        assignment.Feedback    = feedback;
        assignment.EvaluatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync();
        TempData["Success"] = "Task evaluated successfully.";
        return RedirectToAction("InternReports", new { internshipId = assignment.InternshipId });
    }

    // monthly evaluations
    [HttpGet]
    public async Task<IActionResult> Evaluate(int internshipId)
    {
        var internship = await db.Internships
            .Include(i => i.Student)
            .FirstOrDefaultAsync(i => i.Id == internshipId && i.SupervisorId == UserId);

        if (internship is null) return Forbid();

        
        var months = new List<(int Month, int Year, string Label)>();
        var startDate = internship.StartDate ?? DateOnly.FromDateTime(DateTime.Today);
        var start  = new DateTime(startDate.Year, startDate.Month, 1);
        var now    = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);

        for (var d = start; d <= now; d = d.AddMonths(1))
            months.Add((d.Month, d.Year, d.ToString("MMMM yyyy")));

        
        var evaluated = await db.Evaluations
            .Where(e => e.InternshipId == internshipId)
            .Select(e => new { e.Month, e.Year })
            .ToListAsync();

        var evaluatedSet = evaluated.Select(e => (e.Month, e.Year)).ToHashSet();

        ViewBag.Internship   = internship;
        ViewBag.Months       = months;
        ViewBag.EvaluatedSet = evaluatedSet;
        ViewBag.Skills       = await db.Skills.ToListAsync();

        
        var history = await db.Evaluations
            .Include(e => e.EvaluationSkills).ThenInclude(es => es.Skill)
            .Where(e => e.InternshipId == internshipId)
            .OrderByDescending(e => e.Year).ThenByDescending(e => e.Month)
            .ToListAsync();

        ViewBag.History = history;
        return View();
    }

    [HttpPost]
    public async Task<IActionResult> Evaluate(int internshipId, int month, int year,
        string comments, List<int> skillIds, List<int> ratings)
    {
        // Prevent duplicate evaluation for same month
        if (await db.Evaluations.AnyAsync(e => e.InternshipId == internshipId
                                            && e.Month == month && e.Year == year))
        {
            TempData["Error"] = "You already submitted an evaluation for that month.";
            return RedirectToAction("Evaluate", new { internshipId });
        }

        var evaluation = new Evaluation
        {
            InternshipId = internshipId,
            SupervisorId = UserId,
            Month        = month,
            Year         = year,
            Comments     = comments,
            EvaluationSkills = skillIds.Zip(ratings, (s, r) => new EvaluationSkill
            {
                SkillId = s,
                Rating  = r
            }).ToList()
        };

        db.Evaluations.Add(evaluation);
        await db.SaveChangesAsync();
        TempData["Success"] = "Monthly evaluation submitted.";
        return RedirectToAction("Evaluate", new { internshipId });
    }

    // AI skill gap insight
    [HttpGet]
    public async Task<IActionResult> GetAIInsight(int internshipId)
    {
        var internship = await db.Internships
            .Include(i => i.Student)
            .FirstOrDefaultAsync(i => i.Id == internshipId && i.SupervisorId == UserId);

        if (internship is null) return Json(new { error = "Internship not found." });

        var evaluations = await db.Evaluations
            .Include(e => e.EvaluationSkills).ThenInclude(es => es.Skill)
            .Where(e => e.InternshipId == internshipId)
            .ToListAsync();

        var assignments = await db.Assignments
            .Where(a => a.InternshipId == internshipId)
            .ToListAsync();

        var skillRatings = evaluations
            .SelectMany(e => e.EvaluationSkills)
            .GroupBy(es => es.Skill?.Name ?? "Unknown")
            .Select(g => (Skill: g.Key, Rating: g.Average(es => es.Rating)))
            .ToList();

        var taskEvals = assignments
            .Select(a => (TaskTitle: a.Title, Rating: a.Rating, Feedback: a.Feedback))
            .ToList();

        
        var evalByMonth = evaluations
            .OrderBy(e => e.Year).ThenBy(e => e.Month)
            .Select(e => (
                Label:     new DateOnly(e.Year, e.Month, 1).ToString("MMM yyyy"),
                AvgRating: e.EvaluationSkills.Any()
                           ? Math.Round(e.EvaluationSkills.Average(es => (double)es.Rating), 1)
                           : 0.0
            ))
            .ToList();

        
        var taskRatingHistory = assignments
            .Where(a => a.Rating.HasValue && a.EvaluatedAt.HasValue)
            .OrderBy(a => a.EvaluatedAt)
            .Select(a => (
                Label:  a.Title,
                Rating: a.Rating!.Value
            ))
            .ToList();

        try
        {
            var insight = await ai.GetSupervisorInsight(
                internship.Student?.FullName ?? "Student",
                internship.Position,
                skillRatings, taskEvals,
                evalByMonth, taskRatingHistory);

            return Json(insight);
        }
        catch (Exception ex)
        {
            return Json(new { error = $"AI service error: {ex.Message}" });
        }
    }
}
