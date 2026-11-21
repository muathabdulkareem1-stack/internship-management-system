namespace IMS.Web.Models;

public class Alert
{
    public int Id { get; set; }
    public string Message { get; set; } = string.Empty;
    public bool IsRead { get; set; } = false;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public int UserId { get; set; }
    public User User { get; set; } = null!;
}
