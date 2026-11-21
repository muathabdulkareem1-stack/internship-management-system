using System.Security.Claims;
using IMS.API.Data;
using IMS.API.DTOs.Internship;
using IMS.API.Enums;
using IMS.API.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IMS.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class InternshipsController(AppDbContext db) : ControllerBase
{
    // Student: create internship application
    [HttpPost]
    [Authorize(Roles = "Student")]
    public async Task<IActionResult> Create(CreateInternshipDto dto)
    {
        var studentId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        if (await db.Internships.AnyAsync(i => i.StudentId == studentId))
            return BadRequest("Student already has an internship registered.");

        var internship = new Internship
        {
            CompanyName = dto.CompanyName,
            Position = dto.Position,
            StartDate = dto.StartDate,
            EndDate = dto.EndDate,
            StudentId = studentId,
            SupervisorId = dto.SupervisorId
        };

        db.Internships.Add(internship);
        await db.SaveChangesAsync();
        return CreatedAtAction(nameof(GetById), new { id = internship.Id }, MapToDto(internship));
    }

    // Student: view own internship
    [HttpGet("my")]
    [Authorize(Roles = "Student")]
    public async Task<IActionResult> GetMine()
    {
        var studentId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var internship = await db.Internships
            .Include(i => i.Student)
            .Include(i => i.Supervisor)
            .FirstOrDefaultAsync(i => i.StudentId == studentId);

        return internship is null ? NotFound() : Ok(MapToDto(internship));
    }

    // Supervisor: view assigned interns
    [HttpGet("assigned")]
    [Authorize(Roles = "Supervisor")]
    public async Task<IActionResult> GetAssigned()
    {
        var supervisorId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var internships = await db.Internships
            .Include(i => i.Student)
            .Include(i => i.Supervisor)
            .Where(i => i.SupervisorId == supervisorId)
            .ToListAsync();

        return Ok(internships.Select(MapToDto));
    }

    // Coordinator: view all internships
    [HttpGet]
    [Authorize(Roles = "Coordinator")]
    public async Task<IActionResult> GetAll()
    {
        var internships = await db.Internships
            .Include(i => i.Student)
            .Include(i => i.Supervisor)
            .ToListAsync();

        return Ok(internships.Select(MapToDto));
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        var internship = await db.Internships
            .Include(i => i.Student)
            .Include(i => i.Supervisor)
            .FirstOrDefaultAsync(i => i.Id == id);

        return internship is null ? NotFound() : Ok(MapToDto(internship));
    }

    // Coordinator: approve or reject
    [HttpPatch("{id}/status")]
    [Authorize(Roles = "Coordinator")]
    public async Task<IActionResult> UpdateStatus(int id, [FromBody] InternshipStatus status)
    {
        var internship = await db.Internships.FindAsync(id);
        if (internship is null) return NotFound();

        internship.Status = status;
        await db.SaveChangesAsync();
        return NoContent();
    }

    private static InternshipResponseDto MapToDto(Internship i) => new()
    {
        Id = i.Id,
        CompanyName = i.CompanyName,
        Position = i.Position,
        StartDate = i.StartDate,
        EndDate = i.EndDate,
        Status = i.Status,
        StudentName = i.Student?.FullName ?? "",
        SupervisorName = i.Supervisor?.FullName
    };
}
