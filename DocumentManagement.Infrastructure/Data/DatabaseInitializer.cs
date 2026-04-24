using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;

namespace DocumentManagement.Infrastructure.Data;

public sealed class DatabaseInitializer
{
    private readonly SqliteConnectionFactory _connectionFactory;

    public DatabaseInitializer(SqliteConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public void Initialize()
    {
        using var connection = _connectionFactory.CreateConnection();
        connection.Open();

        CreateTables(connection);
        MigrateUsersTable(connection);

        SeedRoles(connection);
        BackfillUsersRoleId(connection);

        SeedPermissions(connection);
        SeedRolePermissions(connection);
        SeedLookupTables(connection);
        SeedDefaultUsers(connection);
    }

    private static void CreateTables(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = @"
CREATE TABLE IF NOT EXISTS Roles (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    Name TEXT NOT NULL UNIQUE
);

CREATE TABLE IF NOT EXISTS Permissions (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    Code TEXT NOT NULL UNIQUE,
    DisplayName TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS RolePermissions (
    RoleId INTEGER NOT NULL,
    PermissionId INTEGER NOT NULL,
    PRIMARY KEY (RoleId, PermissionId)
);

CREATE TABLE IF NOT EXISTS Users (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    Username TEXT NOT NULL UNIQUE,
    PasswordHash TEXT NOT NULL DEFAULT '',
    FullName TEXT NOT NULL DEFAULT '',
    IsActive INTEGER NOT NULL DEFAULT 1,
    RoleId INTEGER NOT NULL DEFAULT 0
);

CREATE TABLE IF NOT EXISTS document_categories (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    name TEXT NOT NULL UNIQUE,
    is_active INTEGER NOT NULL DEFAULT 1,
    created_at TEXT NULL,
    updated_at TEXT NULL
);

CREATE TABLE IF NOT EXISTS document_statuses (
    id INTEGER PRIMARY KEY,
    name TEXT NOT NULL UNIQUE,
    is_active INTEGER NOT NULL DEFAULT 1,
    created_at TEXT NULL,
    updated_at TEXT NULL
);

CREATE TABLE IF NOT EXISTS documents (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    document_type TEXT NULL,
    document_number TEXT NULL,
    document_code TEXT NULL,
    title TEXT NOT NULL,
    issue_date TEXT NULL,
    due_date TEXT NULL,
    category_id INTEGER NULL,
    status_id INTEGER NULL,
    urgency_level TEXT NULL,
    confidentiality_level TEXT NULL,
    processing_department TEXT NULL,
    assigned_to TEXT NULL,
    is_active INTEGER NOT NULL DEFAULT 1,
    created_at TEXT NULL,
    updated_at TEXT NULL
);

CREATE TABLE IF NOT EXISTS document_attachments (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    document_id INTEGER NOT NULL,
    original_file_name TEXT NOT NULL,
    stored_file_name TEXT NOT NULL,
    stored_file_path TEXT NOT NULL,
    file_extension TEXT NULL,
    mime_type TEXT NULL,
    file_size INTEGER NOT NULL DEFAULT 0,
    file_hash TEXT NULL,
    extracted_text TEXT NULL,
    upload_date TEXT NULL
);

CREATE TABLE IF NOT EXISTS document_history (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    document_id INTEGER NULL,
    action_type TEXT NOT NULL,
    action_description TEXT NULL,
    old_value TEXT NULL,
    new_value TEXT NULL,
    action_at TEXT NOT NULL,
    action_by TEXT NULL
);
";
        command.ExecuteNonQuery();
    }

    private static void MigrateUsersTable(SqliteConnection connection)
    {
        EnsureColumnExists(connection, "Users", "PasswordHash", "TEXT NOT NULL DEFAULT ''");
        EnsureColumnExists(connection, "Users", "FullName", "TEXT NOT NULL DEFAULT ''");
        EnsureColumnExists(connection, "Users", "IsActive", "INTEGER NOT NULL DEFAULT 1");
        EnsureColumnExists(connection, "Users", "RoleId", "INTEGER NOT NULL DEFAULT 0");
    }

    private static void EnsureColumnExists(
        SqliteConnection connection,
        string tableName,
        string columnName,
        string columnDefinition)
    {
        using var checkCmd = connection.CreateCommand();
        checkCmd.CommandText = $"PRAGMA table_info({tableName});";

        var exists = false;

        using (var reader = checkCmd.ExecuteReader())
        {
            while (reader.Read())
            {
                var existingColumnName = reader["name"]?.ToString();
                if (string.Equals(existingColumnName, columnName, StringComparison.OrdinalIgnoreCase))
                {
                    exists = true;
                    break;
                }
            }
        }

        if (!exists)
        {
            using var alterCmd = connection.CreateCommand();
            alterCmd.CommandText = $"ALTER TABLE {tableName} ADD COLUMN {columnName} {columnDefinition};";
            alterCmd.ExecuteNonQuery();
        }
    }

    private static void SeedRoles(SqliteConnection connection)
    {
        var roles = new[] { "Admin", "Manager", "Staff" };

        foreach (var role in roles)
        {
            using var cmd = connection.CreateCommand();
            cmd.CommandText = @"
INSERT OR IGNORE INTO Roles(Name)
VALUES ($name);";
            cmd.Parameters.AddWithValue("$name", role);
            cmd.ExecuteNonQuery();
        }

        foreach (var role in roles)
        {
            using var verifyCmd = connection.CreateCommand();
            verifyCmd.CommandText = @"
SELECT COUNT(1)
FROM Roles
WHERE LOWER(TRIM(Name)) = LOWER(TRIM($name));";
            verifyCmd.Parameters.AddWithValue("$name", role);

            var count = Convert.ToInt32(verifyCmd.ExecuteScalar());
            if (count <= 0)
            {
                throw new InvalidOperationException($"Không seed được role '{role}'.");
            }
        }
    }

    private static int GetRoleId(SqliteConnection connection, string roleName)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
SELECT Id
FROM Roles
WHERE LOWER(TRIM(Name)) = LOWER(TRIM($name))
LIMIT 1;";
        cmd.Parameters.AddWithValue("$name", roleName);

        var result = cmd.ExecuteScalar();
        if (result == null || result == DBNull.Value)
        {
            throw new InvalidOperationException($"Không tìm thấy role '{roleName}'.");
        }

        return Convert.ToInt32(result);
    }

    private static void BackfillUsersRoleId(SqliteConnection connection)
    {
        var staffRoleId = GetRoleId(connection, "Staff");

        using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
UPDATE Users
SET RoleId = $staffRoleId
WHERE RoleId IS NULL OR RoleId = 0;";
        cmd.Parameters.AddWithValue("$staffRoleId", staffRoleId);
        cmd.ExecuteNonQuery();
    }

    private static void SeedPermissions(SqliteConnection connection)
    {
        var permissions = new (string Code, string DisplayName)[]
        {
            ("document.view", "Xem văn bản"),
            ("document.create", "Tạo văn bản"),
            ("document.edit", "Sửa văn bản"),
            ("document.delete", "Xóa văn bản"),
            ("document.approve", "Duyệt văn bản"),
            ("task.view", "Xem công việc"),
            ("task.create", "Tạo công việc"),
            ("task.edit", "Sửa công việc"),
            ("report.view", "Xem báo cáo"),
            ("settings.view", "Xem cài đặt"),
            ("settings.edit", "Sửa cài đặt"),
            ("user.manage", "Quản lý người dùng")
        };

        foreach (var permission in permissions)
        {
            using var cmd = connection.CreateCommand();
            cmd.CommandText = @"
INSERT OR IGNORE INTO Permissions(Code, DisplayName)
VALUES ($code, $displayName);";
            cmd.Parameters.AddWithValue("$code", permission.Code);
            cmd.Parameters.AddWithValue("$displayName", permission.DisplayName);
            cmd.ExecuteNonQuery();
        }
    }

    private static void SeedRolePermissions(SqliteConnection connection)
    {
        AssignPermissions(connection, "Admin", new[]
        {
            "document.view",
            "document.create",
            "document.edit",
            "document.delete",
            "document.approve",
            "task.view",
            "task.create",
            "task.edit",
            "report.view",
            "settings.view",
            "settings.edit",
            "user.manage"
        });

        AssignPermissions(connection, "Manager", new[]
        {
            "document.view",
            "document.create",
            "document.edit",
            "document.approve",
            "task.view",
            "task.create",
            "task.edit",
            "report.view",
            "settings.view"
        });

        AssignPermissions(connection, "Staff", new[]
        {
            "document.view",
            "document.create",
            "document.edit",
            "task.view",
            "task.create"
        });
    }

    private static void AssignPermissions(
        SqliteConnection connection,
        string roleName,
        IEnumerable<string> permissionCodes)
    {
        foreach (var code in permissionCodes)
        {
            using var cmd = connection.CreateCommand();
            cmd.CommandText = @"
INSERT OR IGNORE INTO RolePermissions(RoleId, PermissionId)
SELECT r.Id, p.Id
FROM Roles r
CROSS JOIN Permissions p
WHERE LOWER(TRIM(r.Name)) = LOWER(TRIM($roleName))
  AND LOWER(TRIM(p.Code)) = LOWER(TRIM($permissionCode));";
            cmd.Parameters.AddWithValue("$roleName", roleName);
            cmd.Parameters.AddWithValue("$permissionCode", code);
            cmd.ExecuteNonQuery();
        }
    }

    private static void SeedLookupTables(SqliteConnection connection)
    {
        SeedCategories(connection);
        SeedStatuses(connection);
    }

    private static void SeedCategories(SqliteConnection connection)
    {
        var categories = new[]
        {
            "Công văn đến",
            "Công văn đi",
            "Thông báo",
            "Quyết định",
            "Tờ trình",
            "Biên bản",
            "Hợp đồng",
            "Khác"
        };

        foreach (var category in categories)
        {
            using var cmd = connection.CreateCommand();
            cmd.CommandText = @"
INSERT OR IGNORE INTO document_categories(name, is_active, created_at, updated_at)
VALUES ($name, 1, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP);";
            cmd.Parameters.AddWithValue("$name", category);
            cmd.ExecuteNonQuery();
        }
    }

    private static void SeedStatuses(SqliteConnection connection)
    {
        var statuses = new (int Id, string Name)[]
        {
            (1, "Bản nháp"),
            (2, "Chờ duyệt"),
            (3, "Đang xử lý"),
            (4, "Đã ban hành"),
            (5, "Đã lưu trữ"),
            (6, "Bị từ chối")
        };

        foreach (var status in statuses)
        {
            using var cmd = connection.CreateCommand();
            cmd.CommandText = @"
INSERT OR IGNORE INTO document_statuses(id, name, is_active, created_at, updated_at)
VALUES ($id, $name, 1, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP);";
            cmd.Parameters.AddWithValue("$id", status.Id);
            cmd.Parameters.AddWithValue("$name", status.Name);
            cmd.ExecuteNonQuery();
        }
    }

    private static void SeedDefaultUsers(SqliteConnection connection)
    {
        SeedUser(
            connection,
            username: "admin",
            passwordHash: "admin",
            fullName: "Administrator",
            roleName: "Admin");

        SeedUser(
            connection,
            username: "manager",
            passwordHash: "manager",
            fullName: "Manager User",
            roleName: "Manager");

        SeedUser(
            connection,
            username: "staff",
            passwordHash: "staff",
            fullName: "Staff User",
            roleName: "Staff");
    }

    private static void SeedUser(
        SqliteConnection connection,
        string username,
        string passwordHash,
        string fullName,
        string roleName)
    {
        var roleId = GetRoleId(connection, roleName);

        using var insertCmd = connection.CreateCommand();
        insertCmd.CommandText = @"
INSERT OR IGNORE INTO Users(Username, PasswordHash, FullName, IsActive, RoleId)
VALUES ($username, $passwordHash, $fullName, 1, $roleId);";
        insertCmd.Parameters.AddWithValue("$username", username);
        insertCmd.Parameters.AddWithValue("$passwordHash", passwordHash);
        insertCmd.Parameters.AddWithValue("$fullName", fullName);
        insertCmd.Parameters.AddWithValue("$roleId", roleId);
        insertCmd.ExecuteNonQuery();

        using var updateCmd = connection.CreateCommand();
        updateCmd.CommandText = @"
UPDATE Users
SET PasswordHash = CASE WHEN PasswordHash IS NULL OR TRIM(PasswordHash) = '' THEN $passwordHash ELSE PasswordHash END,
    FullName = CASE WHEN FullName IS NULL OR TRIM(FullName) = '' THEN $fullName ELSE FullName END,
    IsActive = COALESCE(IsActive, 1),
    RoleId = CASE WHEN RoleId IS NULL OR RoleId = 0 THEN $roleId ELSE RoleId END
WHERE LOWER(TRIM(Username)) = LOWER(TRIM($username));";
        updateCmd.Parameters.AddWithValue("$username", username);
        updateCmd.Parameters.AddWithValue("$passwordHash", passwordHash);
        updateCmd.Parameters.AddWithValue("$fullName", fullName);
        updateCmd.Parameters.AddWithValue("$roleId", roleId);
        updateCmd.ExecuteNonQuery();
    }
}