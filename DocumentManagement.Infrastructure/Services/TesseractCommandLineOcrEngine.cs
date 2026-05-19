using DocumentManagement.Application.Interfaces;
using DocumentManagement.Application.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Tesseract;

namespace DocumentManagement.Infrastructure.Services;

public sealed class TesseractCommandLineOcrEngine : IPdfOcrEngine
{
    private readonly PdfOcrOptions _options;
    private readonly IOcrNormalizationService _normalizationService;
    private readonly ILogger<TesseractCommandLineOcrEngine>? _logger;

    public TesseractCommandLineOcrEngine(
        IOptions<PdfOcrOptions>? options = null,
        IOcrNormalizationService? normalizationService = null,
        ILogger<TesseractCommandLineOcrEngine>? logger = null)
    {
        _options = options?.Value ?? new PdfOcrOptions();
        _normalizationService = normalizationService ?? new VietnameseOcrNormalizationService();
        _logger = logger;
    }

    public async Task<PdfOcrPageResult> ReadTextAsync(
        string imagePath,
        string language,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(imagePath) || !File.Exists(imagePath))
            throw new PdfExtractionException(PdfExtractionFailureKind.Malformed, "OCR page image is unavailable.");

        var effectiveLanguage = string.IsNullOrWhiteSpace(language) ? "eng" : language;
        _logger?.LogInformation(
            "TesseractEngine OCR page processing started. imageFile={ImageFile} language={Language} tessdataPathConfigured={TessdataPathConfigured}",
            Path.GetFileName(imagePath),
            effectiveLanguage,
            !string.IsNullOrWhiteSpace(_options.TessdataPath));

        try
        {
            var recognizeTask = Task.Run(
                () => Recognize(imagePath, effectiveLanguage),
                CancellationToken.None);

            return await recognizeTask.WaitAsync(timeout, cancellationToken);
        }
        catch (TimeoutException)
        {
            _logger?.LogWarning("TesseractEngine OCR page processing timed out.");
            throw new PdfExtractionException(PdfExtractionFailureKind.Timeout, "OCR processing timed out.");
        }
        catch (OperationCanceledException)
        {
            _logger?.LogWarning("TesseractEngine OCR page processing cancelled.");
            throw;
        }
        catch (Exception ex) when (ex is not PdfExtractionException)
        {
            _logger?.LogWarning(ex, "TesseractEngine OCR page processing failed.");
            throw new PdfExtractionException(PdfExtractionFailureKind.ScannedImageOnly, "OCR engine failed to read the rendered page.", ex);
        }
    }

    private PdfOcrPageResult Recognize(string imagePath, string language)
    {
        using var engine = new TesseractEngine(_options.TessdataPath, language, EngineMode.Default);
        _logger?.LogInformation("TesseractEngine initialized. language={Language}", language);

        using var pix = Pix.LoadFromFile(imagePath);
        using var page = engine.Process(pix, PageSegMode.Auto);
        var text = page.GetText() ?? string.Empty;
        var confidence = page.GetMeanConfidence();
        var lines = ExtractLines(page);

        _logger?.LogInformation(
            "TesseractEngine OCR page processing completed. extractedTextLength={ExtractedTextLength} confidence={Confidence}",
            text.Length,
            confidence);

        return new PdfOcrPageResult(text, confidence, lines);
    }

    private IReadOnlyList<OcrLine> ExtractLines(Page page)
    {
        var lines = new List<OcrLine>();
        using var iterator = page.GetIterator();
        iterator.Begin();
        var index = 0;
        do
        {
            var text = iterator.GetText(PageIteratorLevel.TextLine);
            if (string.IsNullOrWhiteSpace(text))
                continue;

            iterator.TryGetBoundingBox(PageIteratorLevel.TextLine, out var rect);
            lines.Add(new OcrLine(
                index++,
                0,
                _normalizationService.NormalizeForDisplay(text),
                _normalizationService.NormalizeForComparison(text),
                new BoundingBox(rect.X1, rect.Y1, Math.Max(0, rect.Width), Math.Max(0, rect.Height)),
                iterator.GetConfidence(PageIteratorLevel.TextLine)));
        }
        while (iterator.Next(PageIteratorLevel.TextLine));

        return lines;
    }
}
