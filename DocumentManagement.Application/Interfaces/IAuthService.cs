using DocumentManagement.Application.Models;

namespace DocumentManagement.Application.Interfaces;

public interface IAuthService
{
    Task<UserSession?> LoginAsync(string username, string password);
    Task<UserSession?> RegisterAsync(string username, string password, string fullName, string department);
    Task<bool> ChangePasswordAsync(long userId, string newPassword);
    void Logout();

    UserSession? CurrentUser { get; }
}
