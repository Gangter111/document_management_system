using DocumentManagement.Application.Interfaces;
using DocumentManagement.Application.Models;
using DocumentManagement.Infrastructure.Data;

namespace DocumentManagement.Infrastructure.Services;

public class AuthService : IAuthService
{
    private readonly IDbConnectionFactory _connectionFactory;
    private readonly ICurrentUserService _currentUserService;
    private UserSession? _currentUser;

    public AuthService(
        IDbConnectionFactory connectionFactory,
        ICurrentUserService currentUserService)
    {
        _connectionFactory = connectionFactory;
        _currentUserService = currentUserService;
    }

    public UserSession? CurrentUser => _currentUser ?? GetUserFromClaims();

    public async Task<UserSession?> RegisterAsync(string username, string password, string fullName, string department)
    {
        username = username.Trim();
        fullName = string.IsNullOrWhiteSpace(fullName) ? username : fullName.Trim();
        department = string.IsNullOrWhiteSpace(department) ? "Chưa cấu hình" : department.Trim();

        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
        {
            return null;
        }

        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync();

        var staffRoleId = await GetRoleIdAsync(connection, "Staff");
        if (staffRoleId <= 0)
        {
            return null;
        }

        await using (var existsCmd = connection.CreateCommand())
        {
            existsCmd.CommandText = _connectionFactory.Provider == DatabaseProvider.SqlServer
                ? "SELECT TOP 1 Id FROM Users WHERE LOWER(LTRIM(RTRIM(Username))) = LOWER(LTRIM(RTRIM(@username)));"
                : "SELECT Id FROM Users WHERE LOWER(TRIM(Username)) = LOWER(TRIM(@username)) LIMIT 1;";
            existsCmd.AddParameter("username", username);

            var existing = await existsCmd.ExecuteScalarAsync();
            if (existing != null && existing != DBNull.Value)
            {
                return null;
            }
        }

        await using (var insertCmd = connection.CreateCommand())
        {
            insertCmd.CommandText = @"
INSERT INTO Users(Username, PasswordHash, FullName, Department, IsActive, RoleId)
VALUES (@username, @passwordHash, @fullName, @department, 1, @roleId);";
            insertCmd.AddParameter("username", username);
            insertCmd.AddParameter("passwordHash", BCrypt.Net.BCrypt.HashPassword(password));
            insertCmd.AddParameter("fullName", fullName);
            insertCmd.AddParameter("department", department);
            insertCmd.AddParameter("roleId", staffRoleId);

            await insertCmd.ExecuteNonQueryAsync();
        }

        return await LoginAsync(username, password);
    }

    public async Task<UserSession?> LoginAsync(string username, string password)
    {
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
        {
            return null;
        }

        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync();

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = _connectionFactory.Provider == DatabaseProvider.SqlServer
            ? @"
SELECT TOP 1
    u.Id,
    u.Username,
    u.PasswordHash,
    u.FullName,
    u.Department,
    r.Name AS RoleName
FROM Users u
LEFT JOIN Roles r ON r.Id = u.RoleId
WHERE LOWER(TRIM(u.Username)) = LOWER(TRIM(@username))
  AND u.IsActive = 1;"
            : @"
SELECT
    u.Id,
    u.Username,
    u.PasswordHash,
    u.FullName,
    u.Department,
    r.Name AS RoleName
FROM Users u
LEFT JOIN Roles r ON r.Id = u.RoleId
WHERE LOWER(TRIM(u.Username)) = LOWER(TRIM(@username))
  AND u.IsActive = 1
LIMIT 1;";
        cmd.AddParameter("username", username);

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
SET PasswordHash = @passwordHash
WHERE Id = @userId
  AND IsActive = 1;";
        cmd.AddParameter("passwordHash", BCrypt.Net.BCrypt.HashPassword(newPassword));
        cmd.AddParameter("userId", userId);

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

    private async Task<long> GetRoleIdAsync(System.Data.Common.DbConnection connection, string roleName)
    {
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = _connectionFactory.Provider == DatabaseProvider.SqlServer
            ? "SELECT TOP 1 Id FROM Roles WHERE LOWER(LTRIM(RTRIM(Name))) = LOWER(LTRIM(RTRIM(@name)));"
            : "SELECT Id FROM Roles WHERE LOWER(TRIM(Name)) = LOWER(TRIM(@name)) LIMIT 1;";
        cmd.AddParameter("name", roleName);

        var result = await cmd.ExecuteScalarAsync();
        return result == null || result == DBNull.Value
            ? 0
            : Convert.ToInt64(result);
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
