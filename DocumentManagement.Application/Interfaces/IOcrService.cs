using DocumentManagement.Application.Models;

namespace DocumentManagement.Application.Interfaces;

public interface IOcrService
{
    Task<AutoFillDocumentResult> ExtractAndParseAsync(string filePath);
}
