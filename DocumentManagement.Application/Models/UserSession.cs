namespace DocumentManagement.Application.Models;

public class UserSession
{
    public long Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Department { get; set; } = string.Empty;
    public List<string> Roles { get; set; } = new();
    public bool MustChangePassword { get; set; }

    public bool IsAdmin => Roles.Contains("ADMIN");
}
