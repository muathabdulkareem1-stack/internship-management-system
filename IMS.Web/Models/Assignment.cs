namespace IMS.Web.Models;

public class Assignment
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateOnly DueDate { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    
    public string? AttachmentPath { get; set; }
    public string? AttachmentName { get; set; }

    // filled after report is submitted
    public int? Rating { get; set; }       
    public string? Feedback { get; set; }
    public DateTime? EvaluatedAt { get; set; }

    // FK
    public int InternshipId { get; set; }
    public Internship Internship { get; set; } = null!;

   
    public ActivityReport? Report { get; set; }
}
