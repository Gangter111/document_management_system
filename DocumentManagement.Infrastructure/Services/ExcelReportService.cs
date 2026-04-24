using ClosedXML.Excel;
using DocumentManagement.Application.Interfaces;
using DocumentManagement.Domain.Entities;

namespace DocumentManagement.Infrastructure.Services;

public class ExcelReportService : IReportService
{
    public async Task<string> ExportDocumentsToExcelAsync(List<Document> documents, string outputFolder)
    {
        Directory.CreateDirectory(outputFolder);

        var filePath = Path.Combine(outputFolder, $"documents_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx");

        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add("Documents");

        worksheet.Cell(1, 1).Value = "ID";
        worksheet.Cell(1, 2).Value = "Số văn bản";
        worksheet.Cell(1, 3).Value = "Loại";
        worksheet.Cell(1, 4).Value = "Tiêu đề";
        worksheet.Cell(1, 5).Value = "Người gửi";
        worksheet.Cell(1, 6).Value = "Người nhận";
        worksheet.Cell(1, 7).Value = "Ngày văn bản";
        worksheet.Cell(1, 8).Value = "Khẩn";
        worksheet.Cell(1, 9).Value = "Mật";

        // Style the header
        var headerRange = worksheet.Range(1, 1, 1, 9);
        headerRange.Style.Font.Bold = true;
        headerRange.Style.Fill.BackgroundColor = XLColor.LightGray;

        for (int i = 0; i < documents.Count; i++)
        {
            var row = i + 2;
            var doc = documents[i];

            worksheet.Cell(row, 1).Value = doc.Id;
            worksheet.Cell(row, 2).Value = doc.DocumentNumber;
            worksheet.Cell(row, 3).Value = doc.DocumentType;
            worksheet.Cell(row, 4).Value = doc.Title;
            worksheet.Cell(row, 5).Value = doc.SenderName;
            worksheet.Cell(row, 6).Value = doc.ReceiverName;
            worksheet.Cell(row, 7).Value = doc.IssueDate;
            worksheet.Cell(row, 8).Value = doc.UrgencyLevel;
            worksheet.Cell(row, 9).Value = doc.ConfidentialityLevel;
        }

        worksheet.Columns().AdjustToContents();
        workbook.SaveAs(filePath);

        await Task.CompletedTask;
        return filePath;
    }
}
