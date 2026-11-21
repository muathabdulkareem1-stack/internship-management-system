using IMS.API.Enums;

namespace IMS.API.Models;

public class ActivityReport
{
    public int Id { get; set; }
    public DateOnly ReportDate { get; set; }
    public int HoursWorked { get; set; }
    public string TasksCompleted { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
    public ReportStatus Status { get; set; } = ReportStatus.Draft;
    public DateTime SubmittedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // FK
    public int InternshipId { get; set; }
    public Internship Internship { get; set; } = null!;
}
