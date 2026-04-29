using Microsoft.Data.Sqlite;

namespace DocumentManagement.Infrastructure.Data;

public static class DatabaseMigrator
{
    public static void Migrate(SqliteConnectionFactory connectionFactory)
    {
        using var connection = connectionFactory.CreateConnection();
        connection.Open();

        EnsureDocumentsTable(connection);
        EnsureCategoriesTable(connection);
        EnsureStatusesTable(connection);
        EnsureHistoryTable(connection);
        EnsureAuditLogTable(connection);
        EnsureAuthTables(connection);

        SeedCategories(connection);
        SeedStatuses(connection);
        SeedRoles(connection);
        SeedDefaultUsers(connection);
    }

    private static void EnsureDocumentsTable(SqliteConnection connection)
    {
        using var cmd = connection.CreateCommand();

        cmd.CommandText = @"
CREATE TABLE IF NOT EXISTS documents (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    document_type TEXT,
    document_number TEXT NOT NULL,
    reference_number TEXT,
    title TEXT NOT NULL,
    summary TEXT,
    content_text TEXT,
    issue_date TEXT,
    received_date TEXT,
    due_date TEXT,
    sender_name TEXT,
    receiver_name TEXT,
    signer_name TEXT,
    category_id INTEGER,
    status_id INTEGER,
    confidentiality_level TEXT,
    urgency_level TEXT,
    processing_department TEXT,
    assigned_to TEXT,
    notes TEXT,
    is_active INTEGER NOT NULL DEFAULT 1,
    is_expired INTEGER NOT NULL DEFAULT 0,
    ocr_status TEXT,
    created_at TEXT,
    updated_at TEXT,
    created_by TEXT,
    updated_by TEXT
);";

        cmd.ExecuteNonQuery();

        EnsureDocumentColumns(connection);
        EnsureDocumentIndexes(connection);
    }

    private static void EnsureDocumentColumns(SqliteConnection connection)
    {
        EnsureColumn(connection, "documents", "document_type", "TEXT");
        EnsureColumn(connection, "documents", "document_number", "TEXT");
        EnsureColumn(connection, "documents", "reference_number", "TEXT");
        EnsureColumn(connection, "documents", "title", "TEXT NOT NULL DEFAULT ''");
        EnsureColumn(connection, "documents", "summary", "TEXT");
        EnsureColumn(connection, "documents", "content_text", "TEXT");
        EnsureColumn(connection, "documents", "issue_date", "TEXT");
        EnsureColumn(connection, "documents", "received_date", "TEXT");
        EnsureColumn(connection, "documents", "due_date", "TEXT");
        EnsureColumn(connection, "documents", "sender_name", "TEXT");
        EnsureColumn(connection, "documents", "receiver_name", "TEXT");
        EnsureColumn(connection, "documents", "signer_name", "TEXT");
        EnsureColumn(connection, "documents", "category_id", "INTEGER");
        EnsureColumn(connection, "documents", "status_id", "INTEGER");
        EnsureColumn(connection, "documents", "confidentiality_level", "TEXT");
        EnsureColumn(connection, "documents", "urgency_level", "TEXT");
        EnsureColumn(connection, "documents", "processing_department", "TEXT");
        EnsureColumn(connection, "documents", "assigned_to", "TEXT");
        EnsureColumn(connection, "documents", "notes", "TEXT");
        EnsureColumn(connection, "documents", "is_active", "INTEGER NOT NULL DEFAULT 1");
        EnsureColumn(connection, "documents", "is_expired", "INTEGER NOT NULL DEFAULT 0");
        EnsureColumn(connection, "documents", "ocr_status", "TEXT");
        EnsureColumn(connection, "documents", "created_at", "TEXT");
        EnsureColumn(connection, "documents", "updated_at", "TEXT");
        EnsureColumn(connection, "documents", "created_by", "TEXT");
        EnsureColumn(connection, "documents", "updated_by", "TEXT");
    }

    private static void EnsureDocumentIndexes(SqliteConnection connection)
    {
        ExecuteNonQuery(connection, "CREATE INDEX IF NOT EXISTS idx_documents_is_active ON documents(is_active);");
        ExecuteNonQuery(connection, "CREATE INDEX IF NOT EXISTS idx_documents_document_number ON documents(document_number);");
        ExecuteNonQuery(connection, "CREATE INDEX IF NOT EXISTS idx_documents_title ON documents(title);");
        ExecuteNonQuery(connection, "CREATE INDEX IF NOT EXISTS idx_documents_sender_name ON documents(sender_name);");
        ExecuteNonQuery(connection, "CREATE INDEX IF NOT EXISTS idx_documents_signer_name ON documents(signer_name);");
        ExecuteNonQuery(connection, "CREATE INDEX IF NOT EXISTS idx_documents_issue_date ON documents(issue_date);");
        ExecuteNonQuery(connection, "CREATE INDEX IF NOT EXISTS idx_documents_status_id ON documents(status_id);");
        ExecuteNonQuery(connection, "CREATE INDEX IF NOT EXISTS idx_documents_urgency_level ON documents(urgency_level);");
        ExecuteNonQuery(connection, "CREATE INDEX IF NOT EXISTS idx_documents_active_status_date ON documents(is_active, status_id, issue_date);");
        ExecuteNonQuery(connection, "CREATE INDEX IF NOT EXISTS idx_documents_updated_at ON documents(updated_at);");
    }

    private static void EnsureCategoriesTable(SqliteConnection connection)
    {
        using var cmd = connection.CreateCommand();

        cmd.CommandText = @"
CREATE TABLE IF NOT EXISTS document_categories (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    name TEXT NOT NULL,
    is_active INTEGER NOT NULL DEFAULT 1
);";

        cmd.ExecuteNonQuery();
    }

    private static void EnsureStatusesTable(SqliteConnection connection)
    {
        using var cmd = connection.CreateCommand();

        cmd.CommandText = @"
CREATE TABLE IF NOT EXISTS document_statuses (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    name TEXT NOT NULL,
    is_active INTEGER NOT NULL DEFAULT 1
);";

        cmd.ExecuteNonQuery();
    }

    private static void EnsureHistoryTable(SqliteConnection connection)
    {
        using var cmd = connection.CreateCommand();

        cmd.CommandText = @"
CREATE TABLE IF NOT EXISTS document_history (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    document_id INTEGER,
    action_type TEXT NOT NULL,
    action_description TEXT,
    old_value TEXT,
    new_value TEXT,
    action_by TEXT,
    action_at TEXT NOT NULL
);";

        cmd.ExecuteNonQuery();
    }

    private static void EnsureAuditLogTable(SqliteConnection connection)
    {
        using var cmd = connection.CreateCommand();

        cmd.CommandText = @"
CREATE TABLE IF NOT EXISTS audit_logs (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    entity_name TEXT NOT NULL,
    entity_id INTEGER NOT NULL,
    action TEXT NOT NULL,
    old_values TEXT,
    new_values TEXT,
    changed_columns TEXT,
    username TEXT NOT NULL,
    created_at TEXT NOT NULL
);";

        cmd.ExecuteNonQuery();

        EnsureColumn(connection, "audit_logs", "changed_columns", "TEXT");
        ExecuteNonQuery(connection, "CREATE INDEX IF NOT EXISTS idx_audit_logs_entity ON audit_logs(entity_name, entity_id);");
        ExecuteNonQuery(connection, "CREATE INDEX IF NOT EXISTS idx_audit_logs_created_at ON audit_logs(created_at);");
    }

    private static void EnsureAuthTables(SqliteConnection connection)
    {
        ExecuteNonQuery(
            connection,
            @"
CREATE TABLE IF NOT EXISTS Roles (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    Name TEXT NOT NULL UNIQUE
);");

        ExecuteNonQuery(
            connection,
            @"
CREATE TABLE IF NOT EXISTS Users (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    Username TEXT NOT NULL UNIQUE,
    PasswordHash TEXT NOT NULL DEFAULT '',
    FullName TEXT NOT NULL DEFAULT '',
    Department TEXT NOT NULL DEFAULT '',
    IsActive INTEGER NOT NULL DEFAULT 1,
    RoleId INTEGER NOT NULL DEFAULT 0
);");

        EnsureColumn(connection, "Users", "PasswordHash", "TEXT NOT NULL DEFAULT ''");
        EnsureColumn(connection, "Users", "FullName", "TEXT NOT NULL DEFAULT ''");
        EnsureColumn(connection, "Users", "Department", "TEXT NOT NULL DEFAULT ''");
        EnsureColumn(connection, "Users", "IsActive", "INTEGER NOT NULL DEFAULT 1");
        EnsureColumn(connection, "Users", "RoleId", "INTEGER NOT NULL DEFAULT 0");

        ExecuteNonQuery(connection, "CREATE INDEX IF NOT EXISTS idx_users_username ON Users(Username);");
        ExecuteNonQuery(connection, "CREATE INDEX IF NOT EXISTS idx_users_role_id ON Users(RoleId);");
    }

    private static void EnsureColumn(
        SqliteConnection connection,
        string tableName,
        string columnName,
        string definition)
    {
        using var checkCmd = connection.CreateCommand();
        checkCmd.CommandText = $"PRAGMA table_info({tableName});";

        using var reader = checkCmd.ExecuteReader();

        while (reader.Read())
        {
            if (string.Equals(reader["name"]?.ToString(), columnName, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }
        }

        ExecuteNonQuery(connection, $"ALTER TABLE {tableName} ADD COLUMN {columnName} {definition};");
    }

    private static void ExecuteNonQuery(SqliteConnection connection, string commandText)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = commandText;
        cmd.ExecuteNonQuery();
    }

    private static void SeedCategories(SqliteConnection connection)
    {
        using var countCmd = connection.CreateCommand();
        countCmd.CommandText = "SELECT COUNT(*) FROM document_categories;";

        var count = Convert.ToInt32(countCmd.ExecuteScalar() ?? 0);

        if (count > 0)
        {
            return;
        }

        using var cmd = connection.CreateCommand();

        cmd.CommandText = @"
INSERT INTO document_categories (id, name, is_active) VALUES
(1, 'Công văn', 1),
(2, 'Quyết định', 1),
(3, 'Thông báo', 1),
(4, 'Báo cáo', 1);";

        cmd.ExecuteNonQuery();
    }

    private static void SeedStatuses(SqliteConnection connection)
    {
        using var countCmd = connection.CreateCommand();
        countCmd.CommandText = "SELECT COUNT(*) FROM document_statuses;";

        var count = Convert.ToInt32(countCmd.ExecuteScalar() ?? 0);

        if (count > 0)
        {
            return;
        }

        using var cmd = connection.CreateCommand();

        cmd.CommandText = @"
INSERT INTO document_statuses (id, name, is_active) VALUES
(1, 'Bản nháp', 1),
(2, 'Chờ duyệt', 1),
(3, 'Đã ban hành', 1),
(4, 'Đang xử lý', 1),
(5, 'Hoàn thành', 1);";

        cmd.ExecuteNonQuery();
    }

    private static void SeedRoles(SqliteConnection connection)
    {
        SeedRole(connection, "Admin");
        SeedRole(connection, "Manager");
        SeedRole(connection, "Publisher");
        SeedRole(connection, "Staff");
    }

    private static void SeedRole(SqliteConnection connection, string roleName)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
INSERT OR IGNORE INTO Roles(Name)
VALUES ($name);";
        cmd.Parameters.AddWithValue("$name", roleName);
        cmd.ExecuteNonQuery();
    }

    private static void SeedDefaultUsers(SqliteConnection connection)
    {
        SeedUser(connection, "admin", "admin123", "Administrator", "Admin", "Ban Giám đốc");
        SeedUser(connection, "manager", "manager123", "Manager User", "Manager", "Phòng HCNS");
        SeedUser(connection, "publisher", "publisher123", "Publisher User", "Publisher", "Phòng HCNS");
        SeedUser(connection, "staff", "staff123", "Staff User", "Staff", "Phòng Kinh doanh");
    }

    private static void SeedUser(
        SqliteConnection connection,
        string username,
        string defaultPassword,
        string fullName,
        string roleName,
        string department)
    {
        var roleId = GetRoleId(connection, roleName);
        var passwordHash = BCrypt.Net.BCrypt.HashPassword(defaultPassword);

        using var insertCmd = connection.CreateCommand();
        insertCmd.CommandText = @"
INSERT OR IGNORE INTO Users(Username, PasswordHash, FullName, Department, IsActive, RoleId)
VALUES ($username, $passwordHash, $fullName, $department, 1, $roleId);";
        insertCmd.Parameters.AddWithValue("$username", username);
        insertCmd.Parameters.AddWithValue("$passwordHash", passwordHash);
        insertCmd.Parameters.AddWithValue("$fullName", fullName);
        insertCmd.Parameters.AddWithValue("$department", department);
        insertCmd.Parameters.AddWithValue("$roleId", roleId);
        insertCmd.ExecuteNonQuery();

        using var updateCmd = connection.CreateCommand();
        updateCmd.CommandText = @"
UPDATE Users
SET PasswordHash = CASE
        WHEN PasswordHash IS NULL OR TRIM(PasswordHash) = '' OR PasswordHash = $legacyPassword THEN $passwordHash
        ELSE PasswordHash
    END,
    FullName = CASE
        WHEN FullName IS NULL OR TRIM(FullName) = '' THEN $fullName
        ELSE FullName
    END,
    Department = CASE
        WHEN Department IS NULL OR TRIM(Department) = '' THEN $department
        ELSE Department
    END,
    IsActive = COALESCE(IsActive, 1),
    RoleId = CASE
        WHEN RoleId IS NULL OR RoleId = 0 THEN $roleId
        ELSE RoleId
    END
WHERE LOWER(TRIM(Username)) = LOWER(TRIM($username));";
        updateCmd.Parameters.AddWithValue("$username", username);
        updateCmd.Parameters.AddWithValue("$legacyPassword", username);
        updateCmd.Parameters.AddWithValue("$passwordHash", passwordHash);
        updateCmd.Parameters.AddWithValue("$fullName", fullName);
        updateCmd.Parameters.AddWithValue("$department", department);
        updateCmd.Parameters.AddWithValue("$roleId", roleId);
        updateCmd.ExecuteNonQuery();
    }

    private static long GetRoleId(SqliteConnection connection, string roleName)
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
            throw new InvalidOperationException($"Role '{roleName}' was not seeded.");
        }

        return Convert.ToInt64(result);
    }
}
