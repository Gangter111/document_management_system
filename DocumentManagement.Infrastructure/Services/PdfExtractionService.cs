using System.Text;
using System.Text.RegularExpressions;
using DocumentManagement.Application.Interfaces;
using DocumentManagement.Application.Models;
using UglyToad.PdfPig;

namespace DocumentManagement.Infrastructure.Services;

/// <summary>
/// Dịch vụ trích xuất thông tin từ file văn bản.
/// LƯU Ý: Hiện tại chỉ hỗ trợ trích xuất văn bản từ PDF kỹ thuật số, chưa hỗ trợ OCR cho PDF scan ảnh.
/// </summary>
public class PdfExtractionService : IOcrService
{
    public async Task<AutoFillDocumentResult> ExtractAndParseAsync(string filePath)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException("Không tìm thấy file.", filePath);

        var extension = Path.GetExtension(filePath).ToLowerInvariant();
        string text = string.Empty;

        if (extension == ".pdf")
        {
            text = ExtractTextFromPdf(filePath);
        }
        else
        {
            text = "Hệ thống chỉ hỗ trợ trích xuất dữ liệu từ file PDF.";
        }

        return await Task.FromResult(ParseText(text));
    }

    private string ExtractTextFromPdf(string filePath)
    {
        var sb = new StringBuilder();
        try
        {
            using (var pdf = PdfDocument.Open(filePath))
            {
                foreach (var page in pdf.GetPages())
                {
                    sb.AppendLine(page.Text);
                }
            }
        }
        catch (Exception)
        {
            return "CẢNH BÁO: Không trích xuất được văn bản từ PDF. File có thể là PDF scan ảnh hoặc đã bị bảo vệ. OCR chưa được bật; vui lòng nhập thủ công.";
        }
        return sb.ToString();
    }

    private AutoFillDocumentResult ParseText(string text)
    {
        var result = new AutoFillDocumentResult
        {
            ContentText = text,
            IsFromOcr = false
        };

        if (string.IsNullOrWhiteSpace(text)) return result;
        if (text.StartsWith("CẢNH BÁO:", StringComparison.OrdinalIgnoreCase)) return result;

        // RegEx patterns for common document metadata
        var numberMatch = Regex.Match(text, @"Số[:\s]*([A-Za-z0-9\/\-\.]+)", RegexOptions.IgnoreCase);
        if (numberMatch.Success) result.DocumentNumber = numberMatch.Groups[1].Value.Trim();

        var dateMatch = Regex.Match(text, @"ngày\s+(\d{1,2})\s+tháng\s+(\d{1,2})\s+năm\s+(\d{4})", RegexOptions.IgnoreCase);
        if (dateMatch.Success) 
            result.IssueDate = $"{dateMatch.Groups[3].Value}-{dateMatch.Groups[2].Value.PadLeft(2, '0')}-{dateMatch.Groups[1].Value.PadLeft(2, '0')}";

        if (text.Contains("HỎA TỐC", StringComparison.OrdinalIgnoreCase)) result.UrgencyLevel = "VERY_URGENT";
        else if (text.Contains("KHẨN", StringComparison.OrdinalIgnoreCase)) result.UrgencyLevel = "URGENT";

        var lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (lines.Length > 0) result.Title = lines[0].Length > 200 ? lines[0].Substring(0, 200) : lines[0];

        return result;
    }
}
