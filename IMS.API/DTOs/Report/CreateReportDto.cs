using System.ComponentModel.DataAnnotations;

namespace IMS.API.DTOs.Report;

public class CreateReportDto
{
    [Required] public DateOnly ReportDate { get; set; }
    [Range(1, 12)] public int HoursWorked { get; set; }
    [Required] public string TasksCompleted { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
    [Required] public int InternshipId { get; set; }
}
