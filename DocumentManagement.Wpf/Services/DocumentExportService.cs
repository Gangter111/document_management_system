using System.IO;
using DocumentManagement.Contracts.Documents;
using Microsoft.Win32;
using OfficeOpenXml;

namespace DocumentManagement.Wpf.Services;

public sealed class DocumentExportService
{
    private const int ExportPageSize = 1000;
    private const int AutoFitColumnLimit = 1000;
    private readonly ApiService _apiService;

    public DocumentExportService(ApiService apiService)
    {
        _apiService = apiService;
    }

    public async Task<int?> ExportFilteredAsync(
        DocumentSearchCriteria criteria,
        CancellationToken cancellationToken,
        IProgress<int>? progress = null)
    {
        var dialog = new SaveFileDialog
        {
            Filter = "Excel Files (*.xlsx)|*.xlsx",
            FileName = $"Bao_cao_van_ban_{DateTime.Now:yyyyMMdd_HHmm}.xlsx"
        };

        if (dialog.ShowDialog() != true)
        {
            return null;
        }

        var exported = 0;

        await Task.Run(async () =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;

            using var package = new ExcelPackage();
            var sheet = package.Workbook.Worksheets.Add("Documents");
            WriteHeader(sheet);

            var row = 2;
            var page = 1;
            var totalPages = 1;

            while (page <= totalPages)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var result = await _apiService.SearchDocumentsAsync(
                    criteria.Keyword,
                    criteria.CategoryId,
                    criteria.StatusId,
                    criteria.Urgency,
                    criteria.FromDate,
                    criteria.ToDate,
                    page,
                    ExportPageSize,
                    cancellationToken);

                foreach (var document in result.Items)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    WriteRow(sheet, row, exported + 1, document);
                    row++;
                    exported++;
                }

                progress?.Report(exported);
                totalPages = result.TotalPages;
                page++;
            }

            cancellationToken.ThrowIfCancellationRequested();
            if (exported <= AutoFitColumnLimit)
            {
                sheet.Cells.AutoFitColumns();
            }

            cancellationToken.ThrowIfCancellationRequested();
            using var stream = new FileStream(dialog.FileName, FileMode.Create, FileAccess.Write, FileShare.None);
            package.SaveAs(stream);
        }, cancellationToken);

        return exported;
    }

    private static void WriteHeader(ExcelWorksheet sheet)
    {
        sheet.Cells[1, 1].Value = "STT";
        sheet.Cells[1, 2].Value = "Số hiệu";
        sheet.Cells[1, 3].Value = "Trích yếu";
        sheet.Cells[1, 4].Value = "Loại văn bản";
        sheet.Cells[1, 5].Value = "Trạng thái";
        sheet.Cells[1, 6].Value = "Độ khẩn";
        sheet.Cells[1, 7].Value = "Cơ quan ban hành";
        sheet.Cells[1, 8].Value = "Phòng xử lý";
        sheet.Cells[1, 9].Value = "Người xử lý";
        sheet.Cells[1, 10].Value = "Ngày ban hành";
        sheet.Cells[1, 11].Value = "Hạn xử lý";
    }

    private static void WriteRow(ExcelWorksheet sheet, int row, int index, DocumentDto document)
    {
        sheet.Cells[row, 1].Value = index;
        sheet.Cells[row, 2].Value = document.DocumentNumber;
        sheet.Cells[row, 3].Value = document.Title;
        sheet.Cells[row, 4].Value = document.DocumentType;
        sheet.Cells[row, 5].Value = document.StatusText;
        sheet.Cells[row, 6].Value = document.UrgencyText;
        sheet.Cells[row, 7].Value = document.SenderName;
        sheet.Cells[row, 8].Value = document.ProcessingDepartment;
        sheet.Cells[row, 9].Value = document.AssignedTo;
        sheet.Cells[row, 10].Value = document.IssueDate;
        sheet.Cells[row, 11].Value = document.DueDate;
    }
}
