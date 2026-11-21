using IMS.Web.Enums;

namespace IMS.Web.Models;

public class Internship
{
    public int Id { get; set; }
    public string CompanyName { get; set; } = string.Empty;
    public string Position { get; set; } = string.Empty;
    public DateOnly? StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public InternshipStatus Status { get; set; } = InternshipStatus.Pending;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // FK
    public int StudentId { get; set; }
    public User Student { get; set; } = null!;

   
    public int? SupervisorId { get; set; }
    public User? Supervisor { get; set; }

 
    public int? OpportunityId { get; set; }
    public InternshipOpportunity? Opportunity { get; set; }

 
    public ICollection<Assignment> Assignments { get; set; } = [];
    public ICollection<ActivityReport> ActivityReports { get; set; } = [];
    public ICollection<Evaluation> Evaluations { get; set; } = [];
}
