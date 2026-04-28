using DocumentManagement.Contracts.Auth;

namespace DocumentManagement.Wpf.Services;

public class ApiAuthService
{
    private readonly ApiService _apiService;

    public ApiAuthService(ApiService apiService)
    {
        _apiService = apiService;
    }

    public LoginResponse? CurrentUser { get; private set; }

    public async Task<LoginResponse?> LoginAsync(string username, string password)
    {
        var response = await _apiService.LoginAsync(username, password);

        if (response == null || !response.Success || string.IsNullOrWhiteSpace(response.Token))
        {
            CurrentUser = null;
            AuthSession.Clear();
            _apiService.SetCurrentRole(null);
            _apiService.SetToken(null);
            return null;
        }

        CurrentUser = response;

        AuthSession.Token = response.Token;
        AuthSession.UserId = response.UserId;
        AuthSession.Username = response.Username;
        AuthSession.FullName = response.FullName;
        AuthSession.Role = response.Role;

        _apiService.SetCurrentRole(response.Role);
        _apiService.SetToken(response.Token);

        return CurrentUser;
    }

    public async Task<bool> ChangePasswordAsync(long userId, string newPassword)
    {
        return await _apiService.ChangePasswordAsync(userId, newPassword);
    }

    public void Logout()
    {
        CurrentUser = null;
        AuthSession.Clear();
        _apiService.SetCurrentRole(null);
        _apiService.SetToken(null);
    }
}