namespace DocumentManagement.Contracts.Auth;

public class ChangePasswordRequest
{
    public long UserId { get; set; }
    public string NewPassword { get; set; } = string.Empty;
}
