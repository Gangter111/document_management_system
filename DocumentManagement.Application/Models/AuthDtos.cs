using DocumentManagement.Application.Security;

namespace DocumentManagement.Application.Models;

public class LoginRequest
{
    public string Username { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;
}

public class LoginResponse
{
    public bool Success { get; set; }

    public string? Message { get; set; }

    public UserSession? User { get; set; }
}

public class ChangePasswordRequest
{
    public long UserId { get; set; }

    public string NewPassword { get; set; } = string.Empty;
}