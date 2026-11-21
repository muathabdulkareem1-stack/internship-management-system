using IMS.API.Enums;

namespace IMS.API.Models;

public class User
{
    public int Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public Role Role { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public Internship? Internship { get; set; }                    // if Student
    public ICollection<Internship> SupervisedInternships { get; set; } = [];  // if Supervisor
    public ICollection<Evaluation> Evaluations { get; set; } = []; // if Supervisor
}
