using System.Security.Claims;
using IMS.API.Data;
using IMS.API.DTOs.Evaluation;
using IMS.API.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IMS.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class EvaluationsController(AppDbContext db) : ControllerBase
{
    // Supervisor: submit evaluation
    [HttpPost]
    [Authorize(Roles = "Supervisor")]
    public async Task<IActionResult> Create(CreateEvaluationDto dto)
    {
        var supervisorId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        var internship = await db.Internships
            .FirstOrDefaultAsync(i => i.Id == dto.InternshipId && i.SupervisorId == supervisorId);

        if (internship is null) return Forbid();

        var evaluation = new Evaluation
        {
            InternshipId = dto.InternshipId,
            SupervisorId = supervisorId,
            Period = dto.Period,
            Comments = dto.Comments,
            EvaluationSkills = dto.SkillRatings.Select(sr => new EvaluationSkill
            {
                SkillId = sr.SkillId,
                Rating = sr.Rating
            }).ToList()
        };

        db.Evaluations.Add(evaluation);
        await db.SaveChangesAsync();
        return CreatedAtAction(nameof(GetById), new { id = evaluation.Id }, evaluation);
    }

    // Student: view own evaluations
    [HttpGet("my")]
    [Authorize(Roles = "Student")]
    public async Task<IActionResult> GetMine()
    {
        var studentId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var evaluations = await db.Evaluations
            .Include(e => e.EvaluationSkills).ThenInclude(es => es.Skill)
            .Include(e => e.Supervisor)
            .Where(e => e.Internship.StudentId == studentId)
            .OrderByDescending(e => e.EvaluatedAt)
            .ToListAsync();

        return Ok(evaluations);
    }

    // Coordinator: view all evaluations for an internship
    [HttpGet("internship/{internshipId}")]
    [Authorize(Roles = "Supervisor,Coordinator")]
    public async Task<IActionResult> GetByInternship(int internshipId)
    {
        var evaluations = await db.Evaluations
            .Include(e => e.EvaluationSkills).ThenInclude(es => es.Skill)
            .Include(e => e.Supervisor)
            .Where(e => e.InternshipId == internshipId)
            .OrderByDescending(e => e.EvaluatedAt)
            .ToListAsync();

        return Ok(evaluations);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        var evaluation = await db.Evaluations
            .Include(e => e.EvaluationSkills).ThenInclude(es => es.Skill)
            .FirstOrDefaultAsync(e => e.Id == id);

        return evaluation is null ? NotFound() : Ok(evaluation);
    }

    // Skills catalog
    [HttpGet("skills")]
    public async Task<IActionResult> GetSkills()
        => Ok(await db.Skills.ToListAsync());
}
