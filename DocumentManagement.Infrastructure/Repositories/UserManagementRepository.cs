using System.Data.Common;
using DocumentManagement.Application.Interfaces;
using DocumentManagement.Application.Models;
using DocumentManagement.Infrastructure.Data;

namespace DocumentManagement.Infrastructure.Repositories;

public class UserManagementRepository : IUserManagementRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public UserManagementRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<IReadOnlyList<UserAdminModel>> SearchUsersAsync(string? keyword, bool includeInactive)
    {
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = TopSql(@"
SELECT {0}
    u.Id,
    u.Username,
    u.FullName,
    u.Department,
    u.RoleId,
    COALESCE(r.Name, '') AS RoleName,
    u.IsActive
FROM Users u
LEFT JOIN Roles r ON r.Id = u.RoleId
WHERE (@includeInactive = 1 OR u.IsActive = 1)
  AND (
      @keyword IS NULL
      OR u.Username LIKE @keywordLike
      OR u.FullName LIKE @keywordLike
      OR u.Department LIKE @keywordLike
      OR r.Name LIKE @keywordLike
  )
ORDER BY u.Username", null);
        command.AddParameter("includeInactive", includeInactive ? 1 : 0);
        command.AddParameter("keyword", string.IsNullOrWhiteSpace(keyword) ? null : keyword.Trim());
        command.AddParameter("keywordLike", string.IsNullOrWhiteSpace(keyword) ? null : $"%{keyword.Trim()}%");

        await using var reader = await command.ExecuteReaderAsync();
        var users = new List<UserAdminModel>();

        while (await reader.ReadAsync())
        {
            users.Add(MapUser(reader));
        }

        return users;
    }

    public async Task<IReadOnlyList<RoleModel>> GetRolesAsync()
    {
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = @"
SELECT Id, Name
FROM Roles
ORDER BY Name;";

        await using var reader = await command.ExecuteReaderAsync();
        var roles = new List<RoleModel>();

        while (await reader.ReadAsync())
        {
            roles.Add(new RoleModel
            {
                Id = Convert.ToInt64(reader["Id"]),
                Name = reader["Name"]?.ToString() ?? string.Empty
            });
        }

        return roles;
    }

    public async Task<UserAdminModel> CreateUserAsync(SaveUserModel request)
    {
        ValidateRequest(request, requirePassword: true);

        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync();

        await EnsureUsernameIsUniqueAsync(connection, request.Username, null);
        await EnsureRoleExistsAsync(connection, request.RoleId);

        await using var command = connection.CreateCommand();
        command.CommandText = $@"
INSERT INTO Users(Username, PasswordHash, FullName, Department, IsActive, RoleId)
VALUES (@username, @passwordHash, @fullName, @department, @isActive, @roleId);
{IdentitySelectSql}";
        command.AddParameter("username", request.Username.Trim());
        command.AddParameter("passwordHash", BCrypt.Net.BCrypt.HashPassword(request.Password!));
        command.AddParameter("fullName", request.FullName.Trim());
        command.AddParameter("department", request.Department.Trim());
        command.AddParameter("isActive", request.IsActive ? 1 : 0);
        command.AddParameter("roleId", request.RoleId);

        var id = Convert.ToInt64(await command.ExecuteScalarAsync() ?? 0);
        return await GetRequiredUserAsync(connection, id);
    }

    public async Task<UserAdminModel> UpdateUserAsync(long id, SaveUserModel request)
    {
        if (id <= 0)
        {
            throw new InvalidOperationException("Id nguoi dung khong hop le.");
        }

        ValidateRequest(request, requirePassword: false);

        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync();

        await EnsureUsernameIsUniqueAsync(connection, request.Username, id);
        await EnsureRoleExistsAsync(connection, request.RoleId);
        await EnsureAdminContinuityAsync(connection, id, request.RoleId, request.IsActive);

        await using var command = connection.CreateCommand();

        if (string.IsNullOrWhiteSpace(request.Password))
        {
            command.CommandText = @"
UPDATE Users
SET Username = @username,
    FullName = @fullName,
    Department = @department,
    IsActive = @isActive,
    RoleId = @roleId
WHERE Id = @id;";
        }
        else
        {
            command.CommandText = @"
UPDATE Users
SET Username = @username,
    PasswordHash = @passwordHash,
    FullName = @fullName,
    Department = @department,
    IsActive = @isActive,
    RoleId = @roleId
WHERE Id = @id;";
            command.AddParameter("passwordHash", BCrypt.Net.BCrypt.HashPassword(request.Password));
        }

        command.AddParameter("username", request.Username.Trim());
        command.AddParameter("fullName", request.FullName.Trim());
        command.AddParameter("department", request.Department.Trim());
        command.AddParameter("isActive", request.IsActive ? 1 : 0);
        command.AddParameter("roleId", request.RoleId);
        command.AddParameter("id", id);

        var affected = await command.ExecuteNonQueryAsync();

        if (affected == 0)
        {
            throw new InvalidOperationException("Khong tim thay nguoi dung.");
        }

        return await GetRequiredUserAsync(connection, id);
    }

    public async Task DeleteUserAsync(long id)
    {
        if (id <= 0)
        {
            throw new InvalidOperationException("Id nguoi dung khong hop le.");
        }

        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync();

        await EnsureAdminContinuityAsync(connection, id, null, isActive: false);

        await using var command = connection.CreateCommand();
        command.CommandText = @"
UPDATE Users
SET IsActive = 0
WHERE Id = @id;";
        command.AddParameter("id", id);

        var affected = await command.ExecuteNonQueryAsync();

        if (affected == 0)
        {
            throw new InvalidOperationException("Khong tim thay nguoi dung.");
        }
    }

    private async Task<UserAdminModel> GetRequiredUserAsync(DbConnection connection, long id)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = @"
SELECT
    u.Id,
    u.Username,
    u.FullName,
    u.Department,
    u.RoleId,
    COALESCE(r.Name, '') AS RoleName,
    u.IsActive
FROM Users u
LEFT JOIN Roles r ON r.Id = u.RoleId
WHERE u.Id = @id;";
        command.AddParameter("id", id);

        await using var reader = await command.ExecuteReaderAsync();

        if (await reader.ReadAsync())
        {
            return MapUser(reader);
        }

        throw new InvalidOperationException("Khong tim thay nguoi dung.");
    }

    private async Task EnsureUsernameIsUniqueAsync(DbConnection connection, string username, long? excludedId)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = @"
SELECT COUNT(*)
FROM Users
WHERE LOWER(TRIM(Username)) = LOWER(TRIM(@username))
  AND (@excludedId IS NULL OR Id <> @excludedId);";
        command.AddParameter("username", username.Trim());
        command.AddParameter("excludedId", excludedId);

        var count = Convert.ToInt32(await command.ExecuteScalarAsync() ?? 0);

        if (count > 0)
        {
            throw new InvalidOperationException("Ten dang nhap da ton tai.");
        }
    }

    private async Task EnsureRoleExistsAsync(DbConnection connection, long roleId)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM Roles WHERE Id = @roleId;";
        command.AddParameter("roleId", roleId);

        var count = Convert.ToInt32(await command.ExecuteScalarAsync() ?? 0);

        if (count == 0)
        {
            throw new InvalidOperationException("Vai tro khong hop le.");
        }
    }

    private static async Task EnsureAdminContinuityAsync(
        DbConnection connection,
        long userId,
        long? newRoleId,
        bool isActive)
    {
        await using var currentCommand = connection.CreateCommand();
        currentCommand.CommandText = @"
SELECT COALESCE(r.Name, '') AS RoleName,
       u.IsActive
FROM Users u
LEFT JOIN Roles r ON r.Id = u.RoleId
WHERE u.Id = @userId;";
        currentCommand.AddParameter("userId", userId);

        await using var reader = await currentCommand.ExecuteReaderAsync();

        if (!await reader.ReadAsync())
        {
            return;
        }

        var currentRole = reader["RoleName"]?.ToString() ?? string.Empty;
        var currentlyActive = Convert.ToInt32(reader["IsActive"]) == 1;
        await reader.DisposeAsync();

        if (!currentlyActive || !string.Equals(currentRole, "Admin", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var keepsAdminRole = isActive;

        if (newRoleId.HasValue)
        {
            await using var roleCommand = connection.CreateCommand();
            roleCommand.CommandText = "SELECT Name FROM Roles WHERE Id = @roleId;";
            roleCommand.AddParameter("roleId", newRoleId.Value);
            var newRoleName = (await roleCommand.ExecuteScalarAsync())?.ToString() ?? string.Empty;
            keepsAdminRole = keepsAdminRole
                             && string.Equals(newRoleName, "Admin", StringComparison.OrdinalIgnoreCase);
        }

        if (keepsAdminRole)
        {
            return;
        }

        await using var countCommand = connection.CreateCommand();
        countCommand.CommandText = @"
SELECT COUNT(*)
FROM Users u
INNER JOIN Roles r ON r.Id = u.RoleId
WHERE u.IsActive = 1
  AND LOWER(TRIM(r.Name)) = 'admin'
  AND u.Id <> @userId;";
        countCommand.AddParameter("userId", userId);

        var remainingAdmins = Convert.ToInt32(await countCommand.ExecuteScalarAsync() ?? 0);

        if (remainingAdmins <= 0)
        {
            throw new InvalidOperationException("Phải còn ít nhất một tài khoản Admin đang hoạt động.");
        }
    }

    private string IdentitySelectSql => _connectionFactory.Provider == DatabaseProvider.SqlServer
        ? "SELECT CAST(SCOPE_IDENTITY() AS bigint);"
        : "SELECT last_insert_rowid();";

    private string TopSql(string sql, int? limit)
    {
        if (limit == null)
        {
            return string.Format(sql, string.Empty);
        }

        return _connectionFactory.Provider == DatabaseProvider.SqlServer
            ? string.Format(sql, $"TOP {limit.Value}")
            : string.Format(sql, string.Empty) + $" LIMIT {limit.Value}";
    }

    private static void ValidateRequest(SaveUserModel request, bool requirePassword)
    {
        if (string.IsNullOrWhiteSpace(request.Username))
        {
            throw new InvalidOperationException("Ten dang nhap khong duoc de trong.");
        }

        if (string.IsNullOrWhiteSpace(request.FullName))
        {
            throw new InvalidOperationException("Ho ten khong duoc de trong.");
        }

        if (request.RoleId <= 0)
        {
            throw new InvalidOperationException("Vai tro khong hop le.");
        }

        if (requirePassword && string.IsNullOrWhiteSpace(request.Password))
        {
            throw new InvalidOperationException("Mat khau khong duoc de trong.");
        }

        if (!string.IsNullOrWhiteSpace(request.Password) && request.Password.Length < 8)
        {
            throw new InvalidOperationException("Mat khau phai co toi thieu 8 ky tu.");
        }
    }

    private static UserAdminModel MapUser(DbDataReader reader)
    {
        return new UserAdminModel
        {
            Id = Convert.ToInt64(reader["Id"]),
            Username = reader["Username"]?.ToString() ?? string.Empty,
            FullName = reader["FullName"]?.ToString() ?? string.Empty,
            Department = reader["Department"]?.ToString() ?? string.Empty,
            RoleId = Convert.ToInt64(reader["RoleId"]),
            RoleName = reader["RoleName"]?.ToString() ?? string.Empty,
            IsActive = Convert.ToInt32(reader["IsActive"]) == 1
        };
    }
}
