namespace IMS.Web.Models;

public class Evaluation
{
    public int Id { get; set; }
    public int Month { get; set; }  
    public int Year { get; set; }    
    public string Comments { get; set; } = string.Empty;
    public DateTime EvaluatedAt { get; set; } = DateTime.UtcNow;

    // FK
    public int InternshipId { get; set; }
    public Internship Internship { get; set; } = null!;


    public int SupervisorId { get; set; }
    public User Supervisor { get; set; } = null!;

   
    public ICollection<EvaluationSkill> EvaluationSkills { get; set; } = [];
}
