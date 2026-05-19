using System.Reflection;
using System.Text;
using DocumentManagement.Application.Interfaces;
using DocumentManagement.Application.Models;
using DocumentManagement.Infrastructure.Services;
using Microsoft.Extensions.Options;
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
            QUYET DINH PHE DUYET HO SO
            So: 123/QD-UBND
            Ha Noi, ngay 5 thang 4 nam 2026
            KHAN
            """;

        var result = Assert.IsType<AutoFillDocumentResult>(parseMethod.Invoke(service, new object?[] { text, null }));

        Assert.False(result.IsFromOcr);
        Assert.Equal("123/QD-UBND", result.DocumentNumber);
        Assert.Equal("2026-04-05", result.IssueDate);
        Assert.Equal("URGENT", result.UrgencyLevel);
        Assert.Equal("QUYET DINH PHE DUYET HO SO", result.Title);
        Assert.Equal(text, result.RawText);
    }

    [Fact]
    public void ParseText_MapsLightweightAutofillFields()
    {
        var service = new PdfExtractionService();
        var parseMethod = typeof(PdfExtractionService).GetMethod(
            "ParseText",
            BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.NotNull(parseMethod);

        var text = """
            CONG VAN
            So: 77/CV
            Trich yeu: Ve viec ra soat ho so
            Co quan ban hanh: UBND Quan Hoan Kiem
            Kinh gui: Phong Noi vu
            Nguoi ky: Nguyen Van A
            ngay 12 thang 5 nam 2026
            """;

        var result = Assert.IsType<AutoFillDocumentResult>(parseMethod.Invoke(service, new object?[] { text, null }));

        Assert.Equal("Ve viec ra soat ho so", result.Title);
        Assert.Equal("Ve viec ra soat ho so", result.Summary);
        Assert.Equal("UBND Quan Hoan Kiem", result.SenderName);
        Assert.Equal("Phong Noi vu", result.ReceiverName);
        Assert.Equal("Nguyen Van A", result.SignerName);
    }

    [Fact]
    public void ParseText_DoesNotTreatBirthDateAsIssueDate()
    {
        var result = Parse("""
            QUYET DINH
            So: 12/QD-UBND
            Ong Nguyen Van A, sinh ngay 01/10/1979, CCCD 012345678901
            Ha Noi, ngay 5 thang 4 nam 2026
            """);

        Assert.Equal("2026-04-05", result.IssueDate);
    }

    [Fact]
    public void ParseText_NormalizesOcrConfusionForDecisionNumber()
    {
        var result = Parse("""
            QUYET DINH
            So: 12/DQ-UBND
            ngay 5 thang 4 nam 2026
            """);

        Assert.Equal("12/QD-UBND", result.DocumentNumber);
    }

    [Fact]
    public void ParseText_DoesNotUseHeaderOrganizationAsTitle()
    {
        var result = Parse("""
            CONG TY CO PHAN ABC
            THONG BAO
            So: 02/TB
            Trich yeu: Ve viec nghi le
            """);

        Assert.Equal("Ve viec nghi le", result.Title);
        Assert.Equal("CONG TY CO PHAN ABC", result.SenderName);
    }

    [Fact]
    public void ParseText_FlagsLowConfidenceRecipientForReview()
    {
        var result = Parse("""
            CONG VAN
            So: 77/CV
            Noi nhan: 123456
            """);

        Assert.True(result.RequiresManualReview);
        Assert.Contains(result.Fields, field => field.FieldName == "ReceiverName" && field.RequiresReview);
    }

    [Fact]
    public void ParseText_EmitsTraceForRejectedAndAcceptedIssueDateCandidates()
    {
        var result = Parse("""
            QUYET DINH
            So: 12/QD-UBND
            Ong Nguyen Van A sinh ngay 01/10/1979
            Ha Noi, ngay 18 thang 5 nam 2026
            """);

        Assert.Contains(result.ExtractionTrace, item => item.Stage == "IssueDate" && item.Message.Contains("rejected: 1979-10-01"));
        Assert.Contains(result.ExtractionTrace, item => item.Stage == "IssueDate" && item.Message.Contains("accepted: 2026-05-18"));
    }

    [Fact]
    public void LayoutAnalyzer_SplitsOrganizationFromNationalHeaderWhenVerticalGapExists()
    {
        var analyzer = new VietnameseLayoutAnalyzer();
        var document = new OcrDocument(
            "CONG TY CO PHAN ABC\nCONG HOA XA HOI CHU NGHIA VIET NAM",
            new[]
            {
                new OcrLine(0, 0, "CONG TY CO PHAN ABC", "CONG TY CO PHAN ABC", new BoundingBox(20, 20, 200, 20), 0.95),
                new OcrLine(1, 0, "CONG HOA XA HOI CHU NGHIA VIET NAM", "CONG HOA XA HOI CHU NGHIA VIET NAM", new BoundingBox(350, 80, 260, 20), 0.95)
            });

        var layout = analyzer.Analyze(document);

        Assert.Equal(2, layout.Blocks.Count);
        Assert.Contains(layout.Blocks, block => block.Role == BlockRole.Organization);
        Assert.Contains(layout.Blocks, block => block.SectionType == SectionType.NationalHeader);
    }

    [Fact]
    public async Task ExtractAndParseAsync_ClassifiesUnsupportedFiles()
    {
        var service = new PdfExtractionService();
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.txt");
        await File.WriteAllTextAsync(path, "text");

        try
        {
            var ex = await Assert.ThrowsAsync<PdfExtractionException>(() => service.ExtractAndParseAsync(path));
            Assert.Equal(PdfExtractionFailureKind.Unsupported, ex.FailureKind);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task ExtractAsync_HonorsCancellation()
    {
        var service = new PdfExtractionService();
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.pdf");
        await File.WriteAllBytesAsync(path, new byte[] { 0x25, 0x50, 0x44, 0x46, 0x2D, 0x31, 0x2E, 0x37 });
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        try
        {
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.ExtractAsync(path, cts.Token));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task ExtractAsync_RunsOcrFallbackForScannedPdf()
    {
        var renderer = new StubPageImageRenderer();
        var engine = new StubOcrEngine("CONG VAN OCR\nSo: 42/OCR\nngay 1 thang 5 nam 2026");
        var service = CreateService(renderer, engine);
        var path = await CreateBlankPdfAsync();

        try
        {
            var result = await service.ExtractAsync(path);

            Assert.True(result.IsFromOcr);
            Assert.True(result.IsPartialExtraction);
            Assert.Equal("42/OCR", result.DocumentNumber);
            Assert.Equal("2026-05-01", result.IssueDate);
            Assert.Contains("CONG VAN OCR", result.RawText);
            Assert.Equal(2, renderer.LastMaxPages);
            Assert.Equal(150, renderer.LastDpi);
            Assert.False(Directory.Exists(renderer.LastWorkingDirectory));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task ExtractAsync_OcrTimeoutIsClassifiedAndCleansTemp()
    {
        var renderer = new StubPageImageRenderer();
        var engine = new ThrowingOcrEngine(PdfExtractionFailureKind.Timeout);
        var service = CreateService(renderer, engine);
        var path = await CreateBlankPdfAsync();

        try
        {
            var ex = await Assert.ThrowsAsync<PdfExtractionException>(() => service.ExtractAsync(path));

            Assert.Equal(PdfExtractionFailureKind.Timeout, ex.FailureKind);
            Assert.False(Directory.Exists(renderer.LastWorkingDirectory));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task ExtractAsync_OcrWithoutReadableTextPreservesScannedClassificationAndCleansTemp()
    {
        var renderer = new StubPageImageRenderer();
        var engine = new StubOcrEngine("");
        var service = CreateService(renderer, engine);
        var path = await CreateBlankPdfAsync();

        try
        {
            var ex = await Assert.ThrowsAsync<PdfExtractionException>(() => service.ExtractAsync(path));

            Assert.Equal(PdfExtractionFailureKind.ScannedImageOnly, ex.FailureKind);
            Assert.False(Directory.Exists(renderer.LastWorkingDirectory));
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static PdfExtractionService CreateService(IPdfPageImageRenderer renderer, IPdfOcrEngine engine)
    {
        return new PdfExtractionService(
            pageImageRenderer: renderer,
            ocrEngine: engine,
            ocrOptions: Options.Create(new PdfOcrOptions
            {
                Enabled = true,
                PageLimit = 2,
                Dpi = 150,
                TimeoutSeconds = 2,
                MaxRenderedImageBytes = 1024 * 1024,
                MaxOcrCharacters = 10_000
            }));
    }

    private static AutoFillDocumentResult Parse(string text)
    {
        var service = new PdfExtractionService();
        var parseMethod = typeof(PdfExtractionService).GetMethod(
            "ParseText",
            BindingFlags.Instance | BindingFlags.NonPublic);
        return Assert.IsType<AutoFillDocumentResult>(parseMethod!.Invoke(service, new object?[] { text, null }));
    }

    private static async Task<string> CreateBlankPdfAsync()
    {
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.pdf");
        await File.WriteAllBytesAsync(path, BuildMinimalBlankPdf());
        return path;
    }

    private static byte[] BuildMinimalBlankPdf()
    {
        var objects = new[]
        {
            "1 0 obj\n<< /Type /Catalog /Pages 2 0 R >>\nendobj\n",
            "2 0 obj\n<< /Type /Pages /Kids [3 0 R] /Count 1 >>\nendobj\n",
            "3 0 obj\n<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Resources << >> >>\nendobj\n"
        };

        var builder = new StringBuilder("%PDF-1.4\n");
        var offsets = new List<int> { 0 };
        foreach (var item in objects)
        {
            offsets.Add(Encoding.ASCII.GetByteCount(builder.ToString()));
            builder.Append(item);
        }

        var xrefOffset = Encoding.ASCII.GetByteCount(builder.ToString());
        builder.Append("xref\n0 4\n");
        builder.Append("0000000000 65535 f \n");
        foreach (var offset in offsets.Skip(1))
            builder.Append(offset.ToString("D10")).Append(" 00000 n \n");
        builder.Append("trailer\n<< /Size 4 /Root 1 0 R >>\nstartxref\n");
        builder.Append(xrefOffset).Append('\n');
        builder.Append("%%EOF\n");

        return Encoding.ASCII.GetBytes(builder.ToString());
    }

    private sealed class StubPageImageRenderer : IPdfPageImageRenderer
    {
        public string? LastWorkingDirectory { get; private set; }
        public int LastMaxPages { get; private set; }
        public int LastDpi { get; private set; }

        public async Task<IReadOnlyList<PdfRenderedPageImage>> RenderPagesAsync(
            PdfExtractionJobRequest request,
            string workingDirectory,
            int maxPages,
            int dpi,
            long maxImageBytes,
            CancellationToken cancellationToken = default)
        {
            LastWorkingDirectory = workingDirectory;
            LastMaxPages = maxPages;
            LastDpi = dpi;
            Directory.CreateDirectory(workingDirectory);
            var imagePath = Path.Combine(workingDirectory, "page-1.png");
            await File.WriteAllBytesAsync(imagePath, new byte[] { 1, 2, 3 }, cancellationToken);
            return new[] { new PdfRenderedPageImage(1, imagePath, 3) };
        }
    }

    private sealed class StubOcrEngine : IPdfOcrEngine
    {
        private readonly string _text;

        public StubOcrEngine(string text)
        {
            _text = text;
        }

        public Task<PdfOcrPageResult> ReadTextAsync(
            string imagePath,
            string language,
            TimeSpan timeout,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new PdfOcrPageResult(_text, Confidence: null));
        }
    }

    private sealed class ThrowingOcrEngine : IPdfOcrEngine
    {
        private readonly string _failureKind;

        public ThrowingOcrEngine(string failureKind)
        {
            _failureKind = failureKind;
        }

        public Task<PdfOcrPageResult> ReadTextAsync(
            string imagePath,
            string language,
            TimeSpan timeout,
            CancellationToken cancellationToken = default)
        {
            throw new PdfExtractionException(_failureKind, "OCR failure.");
        }
    }
}
