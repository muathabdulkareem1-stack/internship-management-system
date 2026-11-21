using System.ComponentModel.DataAnnotations;

namespace IMS.API.DTOs.Internship;

public class CreateInternshipDto
{
    [Required] public string CompanyName { get; set; } = string.Empty;
    [Required] public string Position { get; set; } = string.Empty;
    [Required] public DateOnly StartDate { get; set; }
    [Required] public DateOnly EndDate { get; set; }
    public int? SupervisorId { get; set; }
}
