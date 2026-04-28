using DocumentManagement.Application.Models;

namespace DocumentManagement.Application.Interfaces;

public interface IAuthService
{
    Task<UserSession?> LoginAsync(string username, string password);
    Task<bool> ChangePasswordAsync(long userId, string newPassword);
    void Logout();

    UserSession? CurrentUser { get; }
}
