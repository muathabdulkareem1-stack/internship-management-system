using IMS.Web.Enums;

namespace IMS.Web.Models;

public class User
{
    public int Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public Role Role { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

   
    public ICollection<Internship> Internships { get; set; } = []; 
    public ICollection<Internship> SupervisedInternships { get; set; } = [];  
    public ICollection<Evaluation> Evaluations { get; set; } = []; 
}
