using IMS.Web.Enums;

namespace IMS.Web.Models;

public class ActivityReport
{
    public int Id { get; set; }
    public DateOnly ReportDate { get; set; }
    public int HoursWorked { get; set; }
    public ActivityCategory Category { get; set; } = ActivityCategory.Development;
    public string TasksCompleted { get; set; } = string.Empty;
    public string KeyLearnings { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
    public ReportStatus Status { get; set; } = ReportStatus.Draft;
    public string? RejectionReason { get; set; }
    public DateTime SubmittedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // FK
    public int InternshipId { get; set; }
    public Internship Internship { get; set; } = null!;

    public int? AssignmentId { get; set; }
    public Assignment? Assignment { get; set; }

    // file uploads
    public string? AttachmentPath { get; set; }
    public string? AttachmentName { get; set; }
}
