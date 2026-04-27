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

        if (!response.Success)
        {
            CurrentUser = null;
            _apiService.SetCurrentRole(null);
            return null;
        }

        CurrentUser = response;
        _apiService.SetCurrentRole(response.Role);

        return CurrentUser;
    }

    public async Task<bool> ChangePasswordAsync(long userId, string newPassword)
    {
        return await _apiService.ChangePasswordAsync(userId, newPassword);
    }

    public void Logout()
    {
        CurrentUser = null;
        _apiService.SetCurrentRole(null);
    }
}