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
            ExecuteNonQuery(connection, "UPDATE document_statuses SET name = 'Đã lưu trữ' WHERE id = 5 AND name = 'Hoàn thành';");
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
(5, 'Đã lưu trữ', 1);";

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

    private static void SeedOperationalDocuments(SqliteConnection connection)
    {
        using var countCmd = connection.CreateCommand();
        countCmd.CommandText = "SELECT COUNT(*) FROM documents;";

        var totalCount = Convert.ToInt32(countCmd.ExecuteScalar() ?? 0);

        using var seededCountCmd = connection.CreateCommand();
        seededCountCmd.CommandText = "SELECT COUNT(*) FROM documents WHERE created_by = 'seed';";
        var seededCount = Convert.ToInt32(seededCountCmd.ExecuteScalar() ?? 0);

        if (totalCount > 0 && seededCount != totalCount)
        {
            return;
        }

        if (seededCount == 40)
        {
            return;
        }

        if (seededCount > 0)
        {
            ExecuteNonQuery(connection, "DELETE FROM documents WHERE created_by = 'seed';");
        }

        ExecuteNonQuery(
            connection,
            @"
WITH RECURSIVE seed(n) AS (
    SELECT 1
    UNION ALL
    SELECT n + 1 FROM seed WHERE n < 40
)
INSERT INTO documents (
    document_type,
    document_number,
    reference_number,
    title,
    summary,
    content_text,
    issue_date,
    received_date,
    due_date,
    sender_name,
    receiver_name,
    signer_name,
    category_id,
    status_id,
    confidentiality_level,
    urgency_level,
    processing_department,
    assigned_to,
    notes,
    is_active,
    is_expired,
    ocr_status,
    created_at,
    updated_at,
    created_by,
    updated_by
)
SELECT
    CASE WHEN n % 3 = 0 THEN 'OUTGOING' ELSE 'INCOMING' END,
    'QA-2026-' || printf('%03d', n),
    'PG-' || strftime('%Y', '2026-01-01') || '/' || printf('%03d', n),
    CASE n % 8
        WHEN 0 THEN 'Rà soát hồ sơ hợp đồng mua sắm quý ' || ((n % 4) + 1)
        WHEN 1 THEN 'Thông báo lịch họp điều hành tuần ' || n
        WHEN 2 THEN 'Báo cáo tiến độ xử lý văn bản nội bộ số ' || n
        WHEN 3 THEN 'Quyết định phân công xử lý hồ sơ dự án ' || n
        WHEN 4 THEN 'Công văn phối hợp kiểm tra hiện trường đợt ' || n
        WHEN 5 THEN 'Kế hoạch đào tạo nghiệp vụ văn thư tháng ' || ((n % 12) + 1)
        WHEN 6 THEN 'Tờ trình phê duyệt ngân sách vận hành số ' || n
        ELSE 'Biên bản nghiệm thu hạng mục hành chính số ' || n
    END,
    'Dữ liệu QA cục bộ dùng để kiểm thử lọc, tìm kiếm, phân trang, dashboard, báo cáo và lưu trữ.',
    'Nội dung văn bản được tạo deterministically trong SQLite qua migrator để đi qua API và repository thật.',
    date('2026-01-05', '+' || (n * 3) || ' days'),
    date('2026-01-06', '+' || (n * 3) || ' days'),
    CASE
        WHEN n IN (6, 12, 18, 24, 32, 38) THEN date('2026-02-01', '-' || n || ' days')
        ELSE date('2026-02-01', '+' || (n * 2) || ' days')
    END,
    CASE n % 6
        WHEN 0 THEN 'UBND Thành phố'
        WHEN 1 THEN 'Sở Nội vụ'
        WHEN 2 THEN 'Sở Tài chính'
        WHEN 3 THEN 'Ban Quản lý dự án'
        WHEN 4 THEN 'Công ty TNHH Minh An'
        ELSE 'Trung tâm Lưu trữ'
    END,
    CASE n % 5
        WHEN 0 THEN 'Ban Giám đốc'
        WHEN 1 THEN 'Phòng HCNS'
        WHEN 2 THEN 'Phòng Kinh doanh'
        WHEN 3 THEN 'Phòng Kế toán'
        ELSE 'Phòng Pháp chế'
    END,
    CASE n % 5
        WHEN 0 THEN 'Nguyễn Văn An'
        WHEN 1 THEN 'Trần Thị Bình'
        WHEN 2 THEN 'Lê Minh Quang'
        WHEN 3 THEN 'Phạm Thu Hà'
        ELSE 'Đỗ Hoàng Nam'
    END,
    (n % 4) + 1,
    CASE
        WHEN n IN (5, 10, 15, 20, 25, 30, 35, 40) THEN 5
        WHEN n IN (1, 7, 13, 19, 31) THEN 1
        WHEN n IN (3, 8, 14, 21, 27, 33, 39) THEN 3
        ELSE 4
    END,
    CASE WHEN n % 18 = 0 THEN 'CONFIDENTIAL' ELSE 'NORMAL' END,
    CASE
        WHEN n IN (4, 12, 22, 34) THEN 'VERY_URGENT'
        WHEN n IN (2, 9, 16, 23, 28, 37) THEN 'URGENT'
        ELSE 'NORMAL'
    END,
    CASE n % 5
        WHEN 0 THEN 'Ban Giám đốc'
        WHEN 1 THEN 'Phòng HCNS'
        WHEN 2 THEN 'Phòng Kinh doanh'
        WHEN 3 THEN 'Phòng Kế toán'
        ELSE 'Phòng Pháp chế'
    END,
    CASE n % 4
        WHEN 0 THEN 'manager'
        WHEN 1 THEN 'publisher'
        WHEN 2 THEN 'staff'
        ELSE 'admin'
    END,
    CASE
        WHEN n IN (5, 10, 15, 20, 25, 30, 35, 40) THEN 'Đã lưu trữ để kiểm thử Archive và khôi phục.'
        WHEN n IN (6, 12, 18, 24, 32, 38) THEN 'Quá hạn để kiểm thử bộ lọc expired.'
        ELSE 'Dữ liệu kiểm thử vận hành.'
    END,
    1,
    CASE WHEN n IN (6, 12, 18, 24, 32, 38) THEN 1 ELSE 0 END,
    'SEEDED',
    datetime('2026-01-05', '+' || n || ' hours'),
    datetime('2026-03-01', '+' || n || ' hours'),
    'seed',
    'seed'
FROM seed;");
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
