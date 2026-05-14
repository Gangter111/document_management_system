namespace DocumentManagement.Contracts.Auth;

public class RegisterRequest
{
    public string Username { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;

    public string FullName { get; set; } = string.Empty;

    public string Department { get; set; } = string.Empty;
}
