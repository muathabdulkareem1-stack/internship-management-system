using System.ComponentModel.DataAnnotations;

namespace IMS.API.DTOs.Evaluation;

public class CreateEvaluationDto
{
    [Required] public int InternshipId { get; set; }
    [Required] public string Period { get; set; } = string.Empty;
    public string Comments { get; set; } = string.Empty;
    [Required] public List<SkillRatingDto> SkillRatings { get; set; } = [];
}

public class SkillRatingDto
{
    public int SkillId { get; set; }
    [Range(1, 5)] public int Rating { get; set; }
}
