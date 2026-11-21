using IMS.API.Enums;

namespace IMS.API.DTOs.Internship;

public class InternshipResponseDto
{
    public int Id { get; set; }
    public string CompanyName { get; set; } = string.Empty;
    public string Position { get; set; } = string.Empty;
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public InternshipStatus Status { get; set; }
    public string StudentName { get; set; } = string.Empty;
    public string? SupervisorName { get; set; }
}
