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

        SeedCategories(connection);
        SeedStatuses(connection);
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
    username TEXT NOT NULL,
    created_at TEXT NOT NULL
);";

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
}