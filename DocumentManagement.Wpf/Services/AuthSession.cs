namespace DocumentManagement.Wpf.Services;

public static class AuthSession
{
    public static string Token { get; set; } = string.Empty;
    public static long UserId { get; set; }
    public static string Username { get; set; } = string.Empty;
    public static string FullName { get; set; } = string.Empty;
    public static string Role { get; set; } = string.Empty;

    public static bool IsLoggedIn => !string.IsNullOrWhiteSpace(Token);

    public static void Clear()
    {
        Token = string.Empty;
        UserId = 0;
        Username = string.Empty;
        FullName = string.Empty;
        Role = string.Empty;
    }
}