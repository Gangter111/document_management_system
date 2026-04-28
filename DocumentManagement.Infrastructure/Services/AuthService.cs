using DocumentManagement.Application.Interfaces;
using DocumentManagement.Application.Models;

namespace DocumentManagement.Infrastructure.Services;

public class AuthService : IAuthService
{
    public UserSession? CurrentUser { get; private set; }

    public Task<UserSession?> LoginAsync(string username, string password)
    {
        var roles = username.ToLowerInvariant() switch
        {
            "admin" => new List<string> { "ADMIN" },
            "manager" => new List<string> { "MANAGER" },
            _ => new List<string> { "STAFF" }
        };

        var session = new UserSession
        {
            Id = 1,
            Username = username,
            DisplayName = username,
            Roles = roles,
            MustChangePassword = false
        };

        CurrentUser = session;

        return Task.FromResult<UserSession?>(session);
    }

    public Task<bool> ChangePasswordAsync(long userId, string newPassword)
    {
        return Task.FromResult(true);
    }

    public void Logout()
    {
        CurrentUser = null;
    }
}
