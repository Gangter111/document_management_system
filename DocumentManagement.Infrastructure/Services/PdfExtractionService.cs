using System.Text;
using System.Text.RegularExpressions;
using DocumentManagement.Application.Interfaces;
using DocumentManagement.Application.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using UglyToad.PdfPig;

namespace DocumentManagement.Infrastructure.Services;

/// <summary>
/// Extracts document metadata from digital PDFs and uses bounded local OCR for image-only PDFs.
/// </summary>
public class PdfExtractionService : IOcrService, IPdfExtractionWorker
{
    private const long MaxPdfBytes = 20L * 1024L * 1024L;
    private const int MaxPages = 200;
    private const int MaxExtractedCharacters = 500_000;

    private readonly IPdfPageImageRenderer? _pageImageRenderer;
    private readonly IPdfOcrEngine? _ocrEngine;
    private readonly IImagePreprocessor? _imagePreprocessor;
    private readonly IOcrNormalizationService _normalizationService;
    private readonly ILayoutAnalyzer _layoutAnalyzer;
    private readonly IDocumentClassifier _documentClassifier;
    private readonly IFieldExtractor _fieldExtractor;
    private readonly ISemanticParser _semanticParser;
    private readonly IValidationService _validationService;
    private readonly PdfOcrOptions _ocrOptions;
    private readonly ILogger<PdfExtractionService>? _logger;

    public PdfExtractionService(
        IPdfPageImageRenderer? pageImageRenderer = null,
        IPdfOcrEngine? ocrEngine = null,
        IImagePreprocessor? imagePreprocessor = null,
        IOcrNormalizationService? normalizationService = null,
        ILayoutAnalyzer? layoutAnalyzer = null,
        IDocumentClassifier? documentClassifier = null,
        IFieldExtractor? fieldExtractor = null,
        ISemanticParser? semanticParser = null,
        IValidationService? validationService = null,
        IOptions<PdfOcrOptions>? ocrOptions = null,
        ILogger<PdfExtractionService>? logger = null)
    {
        _pageImageRenderer = pageImageRenderer;
        _ocrEngine = ocrEngine;
        _imagePreprocessor = imagePreprocessor;
        _normalizationService = normalizationService ?? new VietnameseOcrNormalizationService();
        _layoutAnalyzer = layoutAnalyzer ?? new VietnameseLayoutAnalyzer();
        _documentClassifier = documentClassifier ?? new VietnameseAdministrativeDocumentClassifier();
        _fieldExtractor = fieldExtractor ?? new VietnameseAdministrativeFieldExtractor();
        _semanticParser = semanticParser ?? new VietnameseSemanticParser();
        _validationService = validationService ?? new VietnameseValidationService();
        _ocrOptions = ocrOptions?.Value ?? new PdfOcrOptions();
        _logger = logger;
    }

    public async Task<AutoFillDocumentResult> ExtractAndParseAsync(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        return await ExtractAsync(filePath, cancellationToken);
    }

    public async Task<AutoFillDocumentResult> ExtractAsync(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        var fileInfo = new FileInfo(filePath);
        var request = new PdfExtractionJobRequest(
            Guid.NewGuid().ToString("N"),
            Path.GetFileName(filePath),
            filePath,
            fileInfo.Exists ? fileInfo.Length : 0,
            TimeSpan.FromSeconds(30));

        return await ExtractAsync(request, cancellationToken);
    }

    public async Task<AutoFillDocumentResult> ExtractAsync(
        PdfExtractionJobRequest request,
        CancellationToken cancellationToken = default)
    {
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        if (request.Timeout > TimeSpan.Zero)
            timeoutCts.CancelAfter(request.Timeout);

        var filePath = request.FilePath;
        cancellationToken = timeoutCts.Token;

        if (!File.Exists(filePath))
            throw new FileNotFoundException("PDF file was not found.", filePath);

        var extension = Path.GetExtension(filePath).ToLowerInvariant();
        if (extension != ".pdf")
        {
            throw new PdfExtractionException(PdfExtractionFailureKind.Unsupported, "Only PDF files are supported.");
        }

        var fileInfo = new FileInfo(filePath);
        if (fileInfo.Length <= 0)
            throw new InvalidOperationException("PDF file is empty.");

        if (fileInfo.Length > MaxPdfBytes)
            throw new PdfExtractionException(PdfExtractionFailureKind.TooLarge, "PDF exceeds the extraction size limit.");

        await ValidatePdfHeaderAsync(filePath, cancellationToken);

        try
        {
            var text = await Task.Run(
                () => ExtractTextFromPdf(filePath, cancellationToken),
                cancellationToken);

            return ParseText(text);
        }
        catch (PdfExtractionException ex) when (ex.FailureKind == PdfExtractionFailureKind.ScannedImageOnly)
        {
            return await ExtractWithOcrFallbackAsync(request, cancellationToken);
        }
    }

    private async Task<AutoFillDocumentResult> ExtractWithOcrFallbackAsync(
        PdfExtractionJobRequest request,
        CancellationToken cancellationToken)
    {
        if (!_ocrOptions.Enabled || _pageImageRenderer == null || _ocrEngine == null)
        {
            throw new PdfExtractionException(
                PdfExtractionFailureKind.ScannedImageOnly,
                "No PDF text layer was found.");
        }

        var tempRoot = Path.Combine(Path.GetTempPath(), "DocumentManagement", "pdf-ocr");
        var workDirectory = Path.Combine(tempRoot, SanitizeToken(request.CorrelationId));
        Directory.CreateDirectory(workDirectory);

        try
        {
            _logger?.LogInformation(
                "PDF OCR fallback started. correlationId={CorrelationId} pageLimit={PageLimit} dpi={Dpi}",
                request.CorrelationId,
                _ocrOptions.SafePageLimit,
                _ocrOptions.SafeDpi);
            var pages = await _pageImageRenderer.RenderPagesAsync(
                request,
                workDirectory,
                _ocrOptions.SafePageLimit,
                _ocrOptions.SafeDpi,
                _ocrOptions.MaxRenderedImageBytes,
                cancellationToken);

            var textBuilder = new StringBuilder(capacity: 4096);
            var ocrLines = new List<OcrLine>();
            var confidences = new List<double>();
            var processedPages = 0;

            foreach (var page in pages.Take(_ocrOptions.SafePageLimit))
            {
                cancellationToken.ThrowIfCancellationRequested();
                processedPages++;

                _logger?.LogInformation(
                    "PDF OCR page read started. correlationId={CorrelationId} page={PageNumber} imageBytes={ImageBytes}",
                    request.CorrelationId,
                    page.PageNumber,
                    page.FileSize);
                var ocrInputPath = _imagePreprocessor == null
                    ? page.ImagePath
                    : await _imagePreprocessor.PreprocessAsync(page.ImagePath, workDirectory, cancellationToken);
                var pageResult = await ReadOcrPageWithLanguageFallbackAsync(ocrInputPath, cancellationToken);
                if (pageResult.Confidence is { } confidence)
                    confidences.Add(confidence);

                if (string.IsNullOrWhiteSpace(pageResult.Text))
                    continue;

                if (pageResult.Lines != null)
                {
                    ocrLines.AddRange(pageResult.Lines.Select(line => line with
                    {
                        Index = ocrLines.Count + line.Index,
                        PageIndex = page.PageNumber - 1
                    }));
                }

                _logger?.LogInformation(
                    "PDF OCR page read completed. correlationId={CorrelationId} page={PageNumber} extractedTextLength={ExtractedTextLength} confidence={Confidence}",
                    request.CorrelationId,
                    page.PageNumber,
                    pageResult.Text.Length,
                    pageResult.Confidence);

                var remaining = _ocrOptions.MaxOcrCharacters - textBuilder.Length;
                if (remaining <= 0)
                    break;

                textBuilder.AppendLine(pageResult.Text.Length > remaining
                    ? pageResult.Text[..remaining]
                    : pageResult.Text);
            }

            if (textBuilder.Length == 0)
            {
                throw new PdfExtractionException(
                    PdfExtractionFailureKind.ScannedImageOnly,
                    "OCR did not find readable text.");
            }

            var result = ParseText(textBuilder.ToString(), ocrLines);
            _logger?.LogInformation(
                "PDF OCR fallback completed. correlationId={CorrelationId} processedPages={ProcessedPages} totalTextLength={TotalTextLength}",
                request.CorrelationId,
                processedPages,
                textBuilder.Length);
            result.IsFromOcr = true;
            result.OcrConfidence = confidences.Count == 0 ? null : confidences.Average();
            result.IsPartialExtraction = processedPages >= _ocrOptions.SafePageLimit || result.OcrConfidence is null or < 0.65;
            result.FailureMessage = result.IsPartialExtraction
                ? "OCR extraction completed with limited confidence. User verification is required."
                : null;

            return result;
        }
        catch (OperationCanceledException)
        {
            _logger?.LogWarning("PDF OCR fallback timed out. correlationId={CorrelationId}", request.CorrelationId);
            throw new PdfExtractionException(PdfExtractionFailureKind.Timeout, "PDF OCR extraction timed out.");
        }
        finally
        {
            TryDeleteDirectory(workDirectory);
        }
    }

    private async Task<PdfOcrPageResult> ReadOcrPageWithLanguageFallbackAsync(
        string imagePath,
        CancellationToken cancellationToken)
    {
        try
        {
            return await _ocrEngine!.ReadTextAsync(
                imagePath,
                _ocrOptions.Language,
                _ocrOptions.Timeout,
                cancellationToken);
        }
        catch (PdfExtractionException ex) when (
            ex.FailureKind == PdfExtractionFailureKind.ScannedImageOnly &&
            !string.IsNullOrWhiteSpace(_ocrOptions.FallbackLanguage) &&
            !string.Equals(_ocrOptions.Language, _ocrOptions.FallbackLanguage, StringComparison.OrdinalIgnoreCase))
        {
            return await _ocrEngine!.ReadTextAsync(
                imagePath,
                _ocrOptions.FallbackLanguage,
                _ocrOptions.Timeout,
                cancellationToken);
        }
    }

    private static async Task ValidatePdfHeaderAsync(string filePath, CancellationToken cancellationToken)
    {
        var header = new byte[4];
        await using var stream = new FileStream(
            filePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: header.Length,
            options: FileOptions.Asynchronous | FileOptions.SequentialScan);

        var read = await stream.ReadAsync(header.AsMemory(0, header.Length), cancellationToken);
        if (read < 4 || header[0] != 0x25 || header[1] != 0x50 || header[2] != 0x44 || header[3] != 0x46)
            throw new PdfExtractionException(PdfExtractionFailureKind.Malformed, "Uploaded file is not a valid PDF.");
    }

    private static string ExtractTextFromPdf(string filePath, CancellationToken cancellationToken)
    {
        var sb = new StringBuilder(capacity: 4096);

        try
        {
            using var pdf = PdfDocument.Open(filePath);
            var pagesRead = 0;

            foreach (var page in pdf.GetPages())
            {
                cancellationToken.ThrowIfCancellationRequested();
                pagesRead++;

                if (pagesRead > MaxPages)
                {
                    sb.AppendLine();
                    sb.AppendLine("[Extraction stopped: PDF page limit exceeded.]");
                    break;
                }

                if (sb.Length >= MaxExtractedCharacters)
                {
                    sb.AppendLine();
                    sb.AppendLine("[Extraction stopped: extracted text limit exceeded.]");
                    break;
                }

                var pageText = page.Text;
                if (string.IsNullOrWhiteSpace(pageText))
                    continue;

                var remaining = MaxExtractedCharacters - sb.Length;
                sb.AppendLine(pageText.Length > remaining ? pageText[..remaining] : pageText);
            }
        }
        catch (OperationCanceledException)
        {
            throw new PdfExtractionException(PdfExtractionFailureKind.Timeout, "PDF extraction timed out.");
        }
        catch (Exception ex) when (IsExpectedPdfParseFailure(ex))
        {
            throw new PdfExtractionException(ClassifyParseFailure(ex), "PDF text extraction failed.", ex);
        }

        if (sb.Length == 0)
            throw new PdfExtractionException(PdfExtractionFailureKind.ScannedImageOnly, "No PDF text layer was found.");

        return sb.ToString();
    }

    private static bool IsExpectedPdfParseFailure(Exception ex)
    {
        return ex is InvalidOperationException
               || ex is IOException
               || ex.GetType().Name.Contains("Pdf", StringComparison.OrdinalIgnoreCase)
               || ex.GetType().Name.Contains("Password", StringComparison.OrdinalIgnoreCase);
    }

    private static string ClassifyParseFailure(Exception ex)
    {
        var text = $"{ex.GetType().Name} {ex.Message}";
        if (text.Contains("password", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("encrypt", StringComparison.OrdinalIgnoreCase))
            return PdfExtractionFailureKind.Encrypted;

        return PdfExtractionFailureKind.Malformed;
    }

    private AutoFillDocumentResult ParseText(string text, IReadOnlyList<OcrLine>? suppliedLines = null)
    {
        var result = new AutoFillDocumentResult
        {
            RawText = text,
            IsFromOcr = false
        };

        if (string.IsNullOrWhiteSpace(text)) return result;
        if (text.StartsWith("WARNING:", StringComparison.OrdinalIgnoreCase)) return result;

        var ocrDocument = new OcrDocument(text, suppliedLines?.Count > 0 ? suppliedLines : BuildSyntheticLines(text));
        var layout = _layoutAnalyzer.Analyze(ocrDocument);
        var classification = _documentClassifier.Classify(layout);
        var candidates = _fieldExtractor.ExtractCandidates(layout, classification);
        var semanticResult = _semanticParser.Parse(layout, classification, candidates);
        var validation = _validationService.Validate(layout, semanticResult);
        var fields = validation.Fields.Values.ToList();

        result.DocumentKind = classification.Kind;
        result.DocumentNumber = GetField(fields, "DocumentNumber");
        result.Title = GetField(fields, "Title");
        result.Summary = result.Title;
        result.IssueDate = GetField(fields, "IssueDate");
        result.SenderName = GetField(fields, "SenderName");
        result.ReceiverName = GetField(fields, "ReceiverName");
        result.SignerName = GetField(fields, "SignerName");
        result.ContentText = GetField(fields, "BodyText");
        result.UrgencyLevel = layout.NormalizedText.Contains("HOA TOC", StringComparison.Ordinal)
            ? "VERY_URGENT"
            : layout.NormalizedText.Contains("KHAN", StringComparison.Ordinal)
                ? "URGENT"
                : null;
        result.Fields = fields;
        result.RequiresManualReview = validation.RequiresManualReview;
        result.ReviewReasons = validation.ReviewReasons;
        result.ExtractionTrace = semanticResult.Trace;

        return result;
    }

    private static string? GetField(IEnumerable<ExtractedFieldValue> fields, string name)
        => fields.FirstOrDefault(field => field.FieldName == name)?.Value;

    private IReadOnlyList<OcrLine> BuildSyntheticLines(string text)
    {
        return text
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select((line, index) => new OcrLine(
                index,
                0,
                _normalizationService.NormalizeForDisplay(line),
                _normalizationService.NormalizeForComparison(line),
                new BoundingBox(0, index * 60, Math.Max(80, line.Length * 8), 20),
                1))
            .ToList();
    }

    private static string? ExtractTitle(IReadOnlyList<string> lines)
    {
        var explicitTitle = lines
            .Select(line => Regex.Match(line, @"^(?:Trích yếu|Trich yeu|Về việc|Ve viec)\s*[:\-]\s*(.+)$", RegexOptions.IgnoreCase))
            .FirstOrDefault(match => match.Success);

        if (explicitTitle is { Success: true })
            return Truncate(explicitTitle.Groups[1].Value.Trim(), 200);

        var candidate = lines.FirstOrDefault(line =>
            !Regex.IsMatch(line, @"^\s*S[oố]\s*:", RegexOptions.IgnoreCase)
            && !Regex.IsMatch(line, @"ng[aà]y\s+\d{1,2}\s+th[aá]ng", RegexOptions.IgnoreCase)
            && line.Length >= 4);

        return candidate == null ? null : Truncate(candidate, 200);
    }

    private static string? ExtractSummary(IReadOnlyList<string> lines)
    {
        var summary = lines
            .Select(line => Regex.Match(line, @"^(?:Trích yếu|Trich yeu|Về việc|Ve viec)\s*[:\-]\s*(.+)$", RegexOptions.IgnoreCase))
            .FirstOrDefault(match => match.Success);

        return summary is { Success: true }
            ? Truncate(summary.Groups[1].Value.Trim(), 500)
            : null;
    }

    private static string? ExtractSigner(string text, IReadOnlyList<string> lines)
    {
        var labeledSigner = ExtractLabeledValue(text, "Người ký", "Nguoi ky", "Ký bởi", "Ky boi");
        if (!string.IsNullOrWhiteSpace(labeledSigner))
            return labeledSigner;

        for (var index = 0; index < lines.Count - 1; index++)
        {
            if (Regex.IsMatch(
                    lines[index],
                    @"^(?:KT\.|TL\.|TM\.)?\s*(?:CHỦ TỊCH|CHU TICH|GIÁM ĐỐC|GIAM DOC|PHÓ GIÁM ĐỐC|PHO GIAM DOC)$",
                    RegexOptions.IgnoreCase))
            {
                return Truncate(lines[index + 1], 200);
            }
        }

        return null;
    }

    private static string? ExtractLabeledValue(string text, params string[] labels)
    {
        foreach (var label in labels)
        {
            var match = Regex.Match(
                text,
                $@"^\s*{Regex.Escape(label)}\s*[:\-]\s*(.+)$",
                RegexOptions.IgnoreCase | RegexOptions.Multiline);
            if (match.Success)
                return Truncate(match.Groups[1].Value.Trim(), 200);
        }

        return null;
    }

    private static string Truncate(string value, int maxLength)
    {
        return value.Length > maxLength ? value[..maxLength] : value;
    }

    private static string SanitizeToken(string value)
    {
        var filtered = new string(value.Where(char.IsLetterOrDigit).ToArray());
        return string.IsNullOrWhiteSpace(filtered) ? Guid.NewGuid().ToString("N") : filtered;
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
                Directory.Delete(path, recursive: true);
        }
        catch
        {
            // Best effort cleanup for OCR work files.
        }
    }
}
