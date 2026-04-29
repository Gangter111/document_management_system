using Microsoft.Data.SqlClient;

namespace DocumentManagement.Infrastructure.Data;

public static class SqlServerDatabaseMigrator
{
    public static void Migrate(SqlServerConnectionFactory connectionFactory)
    {
        using var connection = connectionFactory.CreateConnection();
        connection.Open();

        Execute(connection, @"
IF OBJECT_ID('dbo.Roles', 'U') IS NULL
CREATE TABLE dbo.Roles (
    Id bigint IDENTITY(1,1) PRIMARY KEY,
    Name nvarchar(100) NOT NULL UNIQUE
);");

        Execute(connection, @"
IF OBJECT_ID('dbo.Users', 'U') IS NULL
CREATE TABLE dbo.Users (
    Id bigint IDENTITY(1,1) PRIMARY KEY,
    Username nvarchar(150) NOT NULL UNIQUE,
    PasswordHash nvarchar(500) NOT NULL DEFAULT '',
    FullName nvarchar(250) NOT NULL DEFAULT '',
    Department nvarchar(250) NOT NULL DEFAULT '',
    IsActive bit NOT NULL DEFAULT 1,
    RoleId bigint NOT NULL DEFAULT 0
);");

        Execute(connection, @"
IF OBJECT_ID('dbo.document_categories', 'U') IS NULL
CREATE TABLE dbo.document_categories (
    id bigint IDENTITY(1,1) PRIMARY KEY,
    name nvarchar(250) NOT NULL,
    is_active bit NOT NULL DEFAULT 1
);");

        Execute(connection, @"
IF OBJECT_ID('dbo.document_statuses', 'U') IS NULL
CREATE TABLE dbo.document_statuses (
    id bigint NOT NULL PRIMARY KEY,
    name nvarchar(250) NOT NULL,
    is_active bit NOT NULL DEFAULT 1
);");

        Execute(connection, @"
IF OBJECT_ID('dbo.documents', 'U') IS NULL
CREATE TABLE dbo.documents (
    id bigint IDENTITY(1,1) PRIMARY KEY,
    document_type nvarchar(50) NULL,
    document_number nvarchar(250) NOT NULL DEFAULT '',
    reference_number nvarchar(250) NULL,
    title nvarchar(450) NOT NULL DEFAULT '',
    summary nvarchar(max) NULL,
    content_text nvarchar(max) NULL,
    issue_date nvarchar(50) NULL,
    received_date nvarchar(50) NULL,
    due_date nvarchar(50) NULL,
    sender_name nvarchar(500) NULL,
    receiver_name nvarchar(500) NULL,
    signer_name nvarchar(250) NULL,
    category_id bigint NULL,
    status_id bigint NULL,
    confidentiality_level nvarchar(50) NULL,
    urgency_level nvarchar(50) NULL,
    processing_department nvarchar(250) NULL,
    assigned_to nvarchar(250) NULL,
    notes nvarchar(max) NULL,
    is_active bit NOT NULL DEFAULT 1,
    is_expired bit NOT NULL DEFAULT 0,
    ocr_status nvarchar(50) NULL,
    created_at nvarchar(50) NULL,
    updated_at nvarchar(50) NULL,
    created_by nvarchar(150) NULL,
    updated_by nvarchar(150) NULL
);");

        Execute(connection, @"
IF OBJECT_ID('dbo.document_history', 'U') IS NULL
CREATE TABLE dbo.document_history (
    id bigint IDENTITY(1,1) PRIMARY KEY,
    document_id bigint NULL,
    action_type nvarchar(100) NOT NULL,
    action_description nvarchar(max) NULL,
    old_value nvarchar(max) NULL,
    new_value nvarchar(max) NULL,
    action_by nvarchar(150) NULL,
    action_at datetime2 NOT NULL DEFAULT SYSUTCDATETIME()
);");

        Execute(connection, @"
IF OBJECT_ID('dbo.audit_logs', 'U') IS NULL
CREATE TABLE dbo.audit_logs (
    id bigint IDENTITY(1,1) PRIMARY KEY,
    entity_name nvarchar(150) NOT NULL,
    entity_id bigint NOT NULL,
    action nvarchar(100) NOT NULL,
    old_values nvarchar(max) NULL,
    new_values nvarchar(max) NULL,
    changed_columns nvarchar(max) NULL,
    username nvarchar(150) NOT NULL,
    created_at nvarchar(50) NOT NULL
);");

        Execute(connection, @"
IF OBJECT_ID('dbo.document_attachments', 'U') IS NULL
CREATE TABLE dbo.document_attachments (
    id bigint IDENTITY(1,1) PRIMARY KEY,
    document_id bigint NOT NULL,
    original_file_name nvarchar(500) NOT NULL,
    stored_file_name nvarchar(500) NOT NULL,
    stored_file_path nvarchar(1000) NOT NULL,
    file_extension nvarchar(50) NULL,
    mime_type nvarchar(150) NULL,
    file_size bigint NOT NULL DEFAULT 0,
    file_hash nvarchar(250) NULL,
    extracted_text nvarchar(max) NULL,
    upload_date nvarchar(50) NULL
);");

        SeedRoles(connection);
        SeedLookups(connection);
        SeedUsers(connection);
        EnsureIndexes(connection);
    }

    private static void SeedRoles(SqlConnection connection)
    {
        foreach (var role in new[] { "Admin", "Manager", "Publisher", "Staff" })
        {
            Execute(connection, @"
IF NOT EXISTS (SELECT 1 FROM dbo.Roles WHERE LOWER(LTRIM(RTRIM(Name))) = LOWER(LTRIM(RTRIM(@name))))
INSERT INTO dbo.Roles(Name) VALUES (@name);", ("@name", role));
        }
    }

    private static void SeedLookups(SqlConnection connection)
    {
        foreach (var category in new[] { "Công văn", "Quyết định", "Thông báo", "Báo cáo" })
        {
            Execute(connection, @"
IF NOT EXISTS (SELECT 1 FROM dbo.document_categories WHERE name = @name)
INSERT INTO dbo.document_categories(name, is_active) VALUES (@name, 1);", ("@name", category));
        }

        foreach (var status in new[] { (1, "Bản nháp"), (2, "Chờ duyệt"), (3, "Đã ban hành"), (4, "Đang xử lý"), (5, "Hoàn thành") })
        {
            Execute(connection, @"
IF NOT EXISTS (SELECT 1 FROM dbo.document_statuses WHERE id = @id)
INSERT INTO dbo.document_statuses(id, name, is_active) VALUES (@id, @name, 1);", ("@id", status.Item1), ("@name", status.Item2));
        }
    }

    private static void SeedUsers(SqlConnection connection)
    {
        SeedUser(connection, "admin", "admin123", "Administrator", "Admin", "Ban Giám đốc");
        SeedUser(connection, "manager", "manager123", "Manager User", "Manager", "Phòng HCNS");
        SeedUser(connection, "publisher", "publisher123", "Publisher User", "Publisher", "Phòng HCNS");
        SeedUser(connection, "staff", "staff123", "Staff User", "Staff", "Phòng Kinh doanh");
    }

    private static void SeedUser(SqlConnection connection, string username, string defaultPassword, string fullName, string roleName, string department)
    {
        var passwordHash = BCrypt.Net.BCrypt.HashPassword(defaultPassword);

        Execute(connection, @"
DECLARE @roleId bigint = (SELECT TOP 1 Id FROM dbo.Roles WHERE LOWER(LTRIM(RTRIM(Name))) = LOWER(LTRIM(RTRIM(@roleName))));

IF NOT EXISTS (SELECT 1 FROM dbo.Users WHERE LOWER(LTRIM(RTRIM(Username))) = LOWER(LTRIM(RTRIM(@username))))
INSERT INTO dbo.Users(Username, PasswordHash, FullName, Department, IsActive, RoleId)
VALUES (@username, @passwordHash, @fullName, @department, 1, @roleId);

UPDATE dbo.Users
SET PasswordHash = CASE WHEN PasswordHash IS NULL OR LTRIM(RTRIM(PasswordHash)) = '' OR PasswordHash = @username THEN @passwordHash ELSE PasswordHash END,
    FullName = CASE WHEN FullName IS NULL OR LTRIM(RTRIM(FullName)) = '' THEN @fullName ELSE FullName END,
    Department = CASE WHEN Department IS NULL OR LTRIM(RTRIM(Department)) = '' THEN @department ELSE Department END,
    RoleId = CASE WHEN RoleId IS NULL OR RoleId = 0 THEN @roleId ELSE RoleId END
WHERE LOWER(LTRIM(RTRIM(Username))) = LOWER(LTRIM(RTRIM(@username)));",
            ("@username", username),
            ("@passwordHash", passwordHash),
            ("@fullName", fullName),
            ("@department", department),
            ("@roleName", roleName));
    }

    private static void EnsureIndexes(SqlConnection connection)
    {
        Execute(connection, "IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'idx_documents_document_number') CREATE INDEX idx_documents_document_number ON dbo.documents(document_number);");
        Execute(connection, "IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'idx_documents_title') CREATE INDEX idx_documents_title ON dbo.documents(title);");
        Execute(connection, "IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'idx_documents_sender_name') CREATE INDEX idx_documents_sender_name ON dbo.documents(sender_name);");
        Execute(connection, "IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'idx_documents_status_id') CREATE INDEX idx_documents_status_id ON dbo.documents(status_id);");
        Execute(connection, "IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'idx_audit_logs_entity') CREATE INDEX idx_audit_logs_entity ON dbo.audit_logs(entity_name, entity_id);");
    }

    private static void Execute(SqlConnection connection, string sql, params (string Name, object Value)[] parameters)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;

        foreach (var parameter in parameters)
        {
            var dbParameter = command.CreateParameter();
            dbParameter.ParameterName = parameter.Name;
            dbParameter.Value = parameter.Value;
            command.Parameters.Add(dbParameter);
        }

        command.ExecuteNonQuery();
    }
}
