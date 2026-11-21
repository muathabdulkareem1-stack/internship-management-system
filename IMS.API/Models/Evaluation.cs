namespace IMS.API.Models;

public class Evaluation
{
    public int Id { get; set; }
    public string Period { get; set; } = string.Empty;  // e.g. "Week 4", "Midterm", "Final"
    public string Comments { get; set; } = string.Empty;
    public DateTime EvaluatedAt { get; set; } = DateTime.UtcNow;

    // FK: Internship
    public int InternshipId { get; set; }
    public Internship Internship { get; set; } = null!;

    // FK: Supervisor who submitted
    public int SupervisorId { get; set; }
    public User Supervisor { get; set; } = null!;

    // Skill ratings (join table)
    public ICollection<EvaluationSkill> EvaluationSkills { get; set; } = [];
}
