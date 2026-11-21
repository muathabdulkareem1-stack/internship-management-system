using System.Security.Claims;
using IMS.API.Data;
using IMS.API.DTOs.Report;
using IMS.API.Enums;
using IMS.API.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IMS.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ReportsController(AppDbContext db) : ControllerBase
{
    // Student: submit a report
    [HttpPost]
    [Authorize(Roles = "Student")]
    public async Task<IActionResult> Create(CreateReportDto dto)
    {
        var studentId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        var internship = await db.Internships
            .FirstOrDefaultAsync(i => i.Id == dto.InternshipId && i.StudentId == studentId);

        if (internship is null)
            return Forbid();

        var report = new ActivityReport
        {
            InternshipId = dto.InternshipId,
            ReportDate = dto.ReportDate,
            HoursWorked = dto.HoursWorked,
            TasksCompleted = dto.TasksCompleted,
            Notes = dto.Notes,
            Status = ReportStatus.Submitted,
            SubmittedAt = DateTime.UtcNow
        };

        db.ActivityReports.Add(report);
        await db.SaveChangesAsync();
        return CreatedAtAction(nameof(GetById), new { id = report.Id }, report);
    }

    // Student: view own reports
    [HttpGet("my")]
    [Authorize(Roles = "Student")]
    public async Task<IActionResult> GetMine()
    {
        var studentId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var reports = await db.ActivityReports
            .Include(r => r.Internship)
            .Where(r => r.Internship.StudentId == studentId)
            .OrderByDescending(r => r.ReportDate)
            .ToListAsync();

        return Ok(reports);
    }

    // Supervisor: view reports for assigned interns
    [HttpGet("internship/{internshipId}")]
    [Authorize(Roles = "Supervisor,Coordinator")]
    public async Task<IActionResult> GetByInternship(int internshipId)
    {
        var reports = await db.ActivityReports
            .Where(r => r.InternshipId == internshipId)
            .OrderByDescending(r => r.ReportDate)
            .ToListAsync();

        return Ok(reports);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        var report = await db.ActivityReports.FindAsync(id);
        return report is null ? NotFound() : Ok(report);
    }

    // Supervisor: mark report as reviewed
    [HttpPatch("{id}/review")]
    [Authorize(Roles = "Supervisor")]
    public async Task<IActionResult> Review(int id)
    {
        var report = await db.ActivityReports.FindAsync(id);
        if (report is null) return NotFound();

        report.Status = ReportStatus.Reviewed;
        await db.SaveChangesAsync();
        return NoContent();
    }
}
