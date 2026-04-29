using DocumentManagement.Application.Interfaces;
using DocumentManagement.Application.Models;
using DocumentManagement.Infrastructure.Data;

namespace DocumentManagement.Infrastructure.Services;

public class AuthService : IAuthService
{
    private readonly SqliteConnectionFactory _connectionFactory;
    private readonly ICurrentUserService _currentUserService;
    private UserSession? _currentUser;

    public AuthService(
        SqliteConnectionFactory connectionFactory,
        ICurrentUserService currentUserService)
    {
        _connectionFactory = connectionFactory;
        _currentUserService = currentUserService;
    }

    public UserSession? CurrentUser => _currentUser ?? GetUserFromClaims();

    public async Task<UserSession?> LoginAsync(string username, string password)
    {
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
        {
            return null;
        }

        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync();

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
SELECT
    u.Id,
    u.Username,
    u.PasswordHash,
    u.FullName,
    u.Department,
    r.Name AS RoleName
FROM Users u
LEFT JOIN Roles r ON r.Id = u.RoleId
WHERE LOWER(TRIM(u.Username)) = LOWER(TRIM($username))
  AND u.IsActive = 1
LIMIT 1;";
        cmd.Parameters.AddWithValue("$username", username);

        await using var reader = await cmd.ExecuteReaderAsync();

        if (!await reader.ReadAsync())
        {
            return null;
        }

        var passwordHash = reader["PasswordHash"]?.ToString() ?? string.Empty;

        if (!VerifyPassword(password, passwordHash))
        {
            return null;
        }

        var role = NormalizeRole(reader["RoleName"]?.ToString());

        var session = new UserSession
        {
            Id = Convert.ToInt64(reader["Id"]),
            Username = reader["Username"]?.ToString() ?? username,
            DisplayName = reader["FullName"]?.ToString() ?? username,
            Department = reader["Department"]?.ToString() ?? string.Empty,
            Roles = new List<string> { role },
            MustChangePassword = false
        };

        _currentUser = session;

        return session;
    }

    public async Task<bool> ChangePasswordAsync(long userId, string newPassword)
    {
        if (userId <= 0 || string.IsNullOrWhiteSpace(newPassword))
        {
            return false;
        }

        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync();

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
UPDATE Users
SET PasswordHash = $passwordHash
WHERE Id = $userId
  AND IsActive = 1;";
        cmd.Parameters.AddWithValue("$passwordHash", BCrypt.Net.BCrypt.HashPassword(newPassword));
        cmd.Parameters.AddWithValue("$userId", userId);

        var affected = await cmd.ExecuteNonQueryAsync();

        return affected > 0;
    }

    public void Logout()
    {
        _currentUser = null;
    }

    private static bool VerifyPassword(string password, string passwordHash)
    {
        if (string.IsNullOrWhiteSpace(passwordHash))
        {
            return false;
        }

        try
        {
            return BCrypt.Net.BCrypt.Verify(password, passwordHash);
        }
        catch
        {
            return false;
        }
    }

    private static string NormalizeRole(string? roleName)
    {
        return roleName?.Trim().ToUpperInvariant() switch
        {
            "ADMIN" => "ADMIN",
            "MANAGER" => "MANAGER",
            "PUBLISHER" => "PUBLISHER",
            _ => "STAFF"
        };
    }

    private UserSession? GetUserFromClaims()
    {
        if (string.IsNullOrWhiteSpace(_currentUserService.Username))
        {
            return null;
        }

        var userId = long.TryParse(_currentUserService.UserId, out var parsedUserId)
            ? parsedUserId
            : 0;

        return new UserSession
        {
            Id = userId,
            Username = _currentUserService.Username,
            DisplayName = _currentUserService.Username,
            Department = _currentUserService.Department ?? string.Empty,
            Roles = new List<string> { NormalizeRole(_currentUserService.Role) },
            MustChangePassword = false
        };
    }
}
