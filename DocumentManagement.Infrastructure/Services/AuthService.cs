using DocumentManagement.Application.Interfaces;
using DocumentManagement.Application.Models;
using DocumentManagement.Infrastructure.Data;
using BCryptNet = BCrypt.Net.BCrypt;

namespace DocumentManagement.Infrastructure.Services;

public class AuthService : IAuthService
{
    private readonly SqliteConnectionFactory _connectionFactory;
    private readonly ICurrentUserContext _currentUserContext;

    public UserSession? CurrentUser { get; private set; }

    public AuthService(
        SqliteConnectionFactory connectionFactory,
        ICurrentUserContext currentUserContext)
    {
        _connectionFactory = connectionFactory;
        _currentUserContext = currentUserContext;
    }

    public async Task<UserSession?> LoginAsync(string username, string password)
    {
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
        {
            CurrentUser = null;
            _currentUserContext.Clear();
            return null;
        }

        using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync();

        using var command = connection.CreateCommand();
        command.CommandText = @"
SELECT
    u.Id,
    u.Username,
    u.PasswordHash,
    u.FullName,
    r.Name AS RoleName
FROM Users u
LEFT JOIN Roles r ON u.RoleId = r.Id
WHERE LOWER(TRIM(u.Username)) = LOWER(TRIM($username))
  AND u.IsActive = 1
LIMIT 1;";
        command.Parameters.AddWithValue("$username", username.Trim());

        using var reader = await command.ExecuteReaderAsync();

        if (!await reader.ReadAsync())
        {
            CurrentUser = null;
            _currentUserContext.Clear();
            return null;
        }

        var storedHash = reader["PasswordHash"]?.ToString();
        if (string.IsNullOrWhiteSpace(storedHash))
        {
            CurrentUser = null;
            _currentUserContext.Clear();
            return null;
        }

        var isValid = VerifyPassword(password, storedHash);
        if (!isValid)
        {
            CurrentUser = null;
            _currentUserContext.Clear();
            return null;
        }

        var session = new UserSession
        {
            Id = Convert.ToInt64(reader["Id"]),
            Username = reader["Username"]?.ToString() ?? string.Empty,
            DisplayName = reader["FullName"]?.ToString() ?? string.Empty,
            MustChangePassword = false,
            Roles = new List<string>()
        };

        var roleName = reader["RoleName"]?.ToString();
        if (!string.IsNullOrWhiteSpace(roleName))
        {
            session.Roles.Add(roleName.Trim());
        }

        CurrentUser = session;
        _currentUserContext.Set(session);

        return session;
    }

    public async Task<bool> ChangePasswordAsync(long userId, string newPassword)
    {
        if (userId <= 0 || string.IsNullOrWhiteSpace(newPassword))
        {
            return false;
        }

        using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync();

        using var command = connection.CreateCommand();
        command.CommandText = @"
UPDATE Users
SET PasswordHash = $hash
WHERE Id = $id;";
        command.Parameters.AddWithValue("$hash", BCryptNet.HashPassword(newPassword));
        command.Parameters.AddWithValue("$id", userId);

        var rows = await command.ExecuteNonQueryAsync();
        return rows > 0;
    }

    public void Logout()
    {
        CurrentUser = null;
        _currentUserContext.Clear();
    }

    private static bool VerifyPassword(string password, string storedHash)
    {
        if (string.IsNullOrWhiteSpace(password) || string.IsNullOrWhiteSpace(storedHash))
        {
            return false;
        }

        // Hỗ trợ bootstrap tạm thời: DB seed plain text vẫn login được
        if (storedHash == password)
        {
            return true;
        }

        try
        {
            return BCryptNet.Verify(password, storedHash);
        }
        catch
        {
            return false;
        }
    }
}