namespace IMS.Web.Models;

public class Skill
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;

    public ICollection<EvaluationSkill> EvaluationSkills { get; set; } = [];
}
