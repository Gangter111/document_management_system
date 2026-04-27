using Microsoft.Data.Sqlite;

namespace DocumentManagement.Infrastructure.Data;

public static class DatabaseMigrator
{
    public static void Migrate(SqliteConnectionFactory connectionFactory)
    {
        using var connection = connectionFactory.CreateConnection();
        connection.Open();

        AddColumnIfMissing(connection, "documents", "reference_number", "TEXT");
        AddColumnIfMissing(connection, "documents", "summary", "TEXT");
        AddColumnIfMissing(connection, "documents", "content_text", "TEXT");
        AddColumnIfMissing(connection, "documents", "received_date", "TEXT");
        AddColumnIfMissing(connection, "documents", "due_date", "TEXT");
        AddColumnIfMissing(connection, "documents", "sender_name", "TEXT");
        AddColumnIfMissing(connection, "documents", "receiver_name", "TEXT");
        AddColumnIfMissing(connection, "documents", "signer_name", "TEXT");
        AddColumnIfMissing(connection, "documents", "is_expired", "INTEGER DEFAULT 0");
        AddColumnIfMissing(connection, "documents", "ocr_status", "TEXT DEFAULT 'PENDING'");
        AddColumnIfMissing(connection, "documents", "created_by", "TEXT");
        AddColumnIfMissing(connection, "documents", "updated_by", "TEXT");
    }

    private static void AddColumnIfMissing(
        SqliteConnection connection,
        string tableName,
        string columnName,
        string columnType)
    {
        if (ColumnExists(connection, tableName, columnName))
        {
            return;
        }

        using var command = connection.CreateCommand();
        command.CommandText = $"ALTER TABLE {tableName} ADD COLUMN {columnName} {columnType};";
        command.ExecuteNonQuery();
    }

    private static bool ColumnExists(
        SqliteConnection connection,
        string tableName,
        string columnName)
    {
        using var command = connection.CreateCommand();
        command.CommandText = $"PRAGMA table_info({tableName});";

        using var reader = command.ExecuteReader();

        while (reader.Read())
        {
            var name = reader["name"]?.ToString();

            if (string.Equals(name, columnName, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}