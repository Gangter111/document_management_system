using System.Collections.Generic;
using System.Threading;
using DocumentManagement.Domain.Entities;

namespace DocumentManagement.Application.Interfaces;

public interface IReportService
{
    Task<string> ExportDocumentsToExcelAsync(
        List<Document> documents,
        string outputFolder,
        CancellationToken cancellationToken = default);
}
