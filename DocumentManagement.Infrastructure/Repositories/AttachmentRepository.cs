using DocumentManagement.Application.Interfaces;
using DocumentManagement.Domain.Entities;
using DocumentManagement.Infrastructure.Data;

namespace DocumentManagement.Infrastructure.Repositories;

public class AttachmentRepository : IAttachmentRepository
{
    private readonly IDbConnectionFactory _connectionFactory;
    private readonly IDatabaseDialect _dialect;

    public AttachmentRepository(
        IDbConnectionFactory connectionFactory,
        IDatabaseDialect dialect)
    {
        _connectionFactory = connectionFactory;
        _dialect = dialect;
    }

    public async Task<long> CreateAsync(DocumentAttachment attachment)
    {
        using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync();

        using var command = connection.CreateCommand();

        command.CommandText = $@"
INSERT INTO document_attachments (
    document_id, original_file_name, stored_file_name, stored_file_path,
    file_extension, mime_type, file_size, file_hash, extracted_text, upload_date
)
VALUES (
    @document_id, @original_file_name, @stored_file_name, @stored_file_path,
    @file_extension, @mime_type, @file_size, @file_hash, @extracted_text, @upload_date
);
{_dialect.IdentitySelectSql}";

        command.AddParameter("document_id", attachment.DocumentId);
        command.AddParameter("original_file_name", attachment.OriginalFileName);
        command.AddParameter("stored_file_name", attachment.StoredFileName);
        command.AddParameter("stored_file_path", attachment.StoredFilePath);
        command.AddParameter("file_extension", attachment.FileExtension);
        command.AddParameter("mime_type", attachment.MimeType);
        command.AddParameter("file_size", attachment.FileSize);
        command.AddParameter("file_hash", attachment.FileHash);
        command.AddParameter("extracted_text", attachment.ExtractedText);
        command.AddParameter("upload_date", attachment.UploadDate);

        var result = await command.ExecuteScalarAsync();
        return Convert.ToInt64(result ?? 0L);
    }

    public async Task<List<DocumentAttachment>> GetByDocumentIdAsync(long documentId)
    {
        using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync();

        using var command = connection.CreateCommand();
        command.CommandText = @"
SELECT *
FROM document_attachments
WHERE document_id = @documentId
ORDER BY upload_date DESC;";
        command.AddParameter("documentId", documentId);

        using var reader = await command.ExecuteReaderAsync();
        var result = new List<DocumentAttachment>();

        while (await reader.ReadAsync())
        {
            result.Add(new DocumentAttachment
            {
                Id = reader.GetInt64(reader.GetOrdinal("id")),
                DocumentId = reader.GetInt64(reader.GetOrdinal("document_id")),
                OriginalFileName = reader["original_file_name"]?.ToString() ?? string.Empty,
                StoredFileName = reader["stored_file_name"]?.ToString() ?? string.Empty,
                StoredFilePath = reader["stored_file_path"]?.ToString() ?? string.Empty,
                FileExtension = reader["file_extension"]?.ToString(),
                MimeType = reader["mime_type"]?.ToString(),
                FileSize = reader["file_size"] == DBNull.Value ? 0 : Convert.ToInt64(reader["file_size"]),
                FileHash = reader["file_hash"]?.ToString(),
                ExtractedText = reader["extracted_text"]?.ToString(),
                UploadDate = reader["upload_date"]?.ToString() ?? string.Empty
            });
        }

        return result;
    }

    public async Task<bool> DeleteByIdAsync(long id)
    {
        using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync();

        using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM document_attachments WHERE id = @id;";
        command.AddParameter("id", id);

        var rows = await command.ExecuteNonQueryAsync();
        return rows > 0;
    }
}
