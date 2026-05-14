using System.Reflection;
using DocumentManagement.Application.Models;
using DocumentManagement.Infrastructure.Services;
using Xunit;

namespace DocumentManagement.Tests.Services;

public class PdfExtractionServiceTests
{
    [Fact]
    public void ParseText_MapsDeterministicVietnameseFields()
    {
        var service = new PdfExtractionService();
        var parseMethod = typeof(PdfExtractionService).GetMethod(
            "ParseText",
            BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.NotNull(parseMethod);

        var text = """
            QUYẾT ĐỊNH PHÊ DUYỆT HỒ SƠ
            Số: 123/QD-UBND
            Hà Nội, ngày 5 tháng 4 năm 2026
            KHẨN
            """;

        var result = Assert.IsType<AutoFillDocumentResult>(parseMethod.Invoke(service, new object[] { text }));

        Assert.False(result.IsFromOcr);
        Assert.Equal("123/QD-UBND", result.DocumentNumber);
        Assert.Equal("2026-04-05", result.IssueDate);
        Assert.Equal("URGENT", result.UrgencyLevel);
        Assert.Equal("QUYẾT ĐỊNH PHÊ DUYỆT HỒ SƠ", result.Title);
        Assert.Equal(text, result.ContentText);
    }
}
