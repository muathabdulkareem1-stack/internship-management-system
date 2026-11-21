namespace IMS.Web.Models;

public class InternshipOpportunity
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string CompanyName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public DateOnly ApplicationDeadline { get; set; }
    public int MaxStudents { get; set; } = 1;
    public bool IsOpen { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // FK
    public int CoordinatorId { get; set; }
    public User Coordinator { get; set; } = null!;

    
    public ICollection<Internship> Applications { get; set; } = [];
}
