namespace IMS.API.Models;

public class EvaluationSkill
{
    public int EvaluationId { get; set; }
    public Evaluation Evaluation { get; set; } = null!;

    public int SkillId { get; set; }
    public Skill Skill { get; set; } = null!;

    public int Rating { get; set; }  // 1–5 scale
}
