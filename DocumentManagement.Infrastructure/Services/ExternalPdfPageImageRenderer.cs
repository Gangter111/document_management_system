using System.Buffers.Binary;
using Docnet.Core;
using Docnet.Core.Models;
using DocumentManagement.Application.Interfaces;
using DocumentManagement.Application.Models;
using Microsoft.Extensions.Logging;

namespace DocumentManagement.Infrastructure.Services;

public sealed class ExternalPdfPageImageRenderer : IPdfPageImageRenderer
{
    private const int BitsPerPixel = 32;
    private const int BmpHeaderBytes = 54;
    private readonly ILogger<ExternalPdfPageImageRenderer>? _logger;

    public ExternalPdfPageImageRenderer(ILogger<ExternalPdfPageImageRenderer>? logger = null)
    {
        _logger = logger;
    }

    public async Task<IReadOnlyList<PdfRenderedPageImage>> RenderPagesAsync(
        PdfExtractionJobRequest request,
        string workingDirectory,
        int maxPages,
        int dpi,
        long maxImageBytes,
        CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(workingDirectory);

        try
        {
            var rendered = new List<PdfRenderedPageImage>();
            using var docReader = DocLib.Instance.GetDocReader(
                request.FilePath,
                new PageDimensions(dpi / 72d));

            var totalPageCount = docReader.GetPageCount();
            var pageCount = Math.Min(totalPageCount, maxPages);
            _logger?.LogInformation(
                "PDF OCR render started. correlationId={CorrelationId} totalPages={TotalPages} renderPages={RenderPages} dpi={Dpi}",
                request.CorrelationId,
                totalPageCount,
                pageCount,
                dpi);

            for (var pageIndex = 0; pageIndex < pageCount; pageIndex++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                using var pageReader = docReader.GetPageReader(pageIndex);
                var width = pageReader.GetPageWidth();
                var height = pageReader.GetPageHeight();
                if (width <= 0 || height <= 0)
                    continue;

                var pixels = pageReader.GetImage();
                _logger?.LogInformation(
                    "PDF OCR page rendered. correlationId={CorrelationId} page={PageNumber} width={Width} height={Height} pixelBytes={PixelBytes}",
                    request.CorrelationId,
                    pageIndex + 1,
                    width,
                    height,
                    pixels.LongLength);
                if (pixels.LongLength <= 0 || pixels.LongLength > maxImageBytes)
                {
                    throw new PdfExtractionException(
                        PdfExtractionFailureKind.TooLarge,
                        "Rendered OCR page image exceeded configured safety limits.");
                }

                var imagePath = Path.Combine(workingDirectory, $"page-{pageIndex + 1}.bmp");
                await WriteBmpAsync(imagePath, width, height, pixels, cancellationToken);

                var fileInfo = new FileInfo(imagePath);
                if (fileInfo.Length <= 0 || fileInfo.Length > maxImageBytes + BmpHeaderBytes)
                {
                    throw new PdfExtractionException(
                        PdfExtractionFailureKind.TooLarge,
                        "Rendered OCR page image exceeded configured safety limits.");
                }

                rendered.Add(new PdfRenderedPageImage(pageIndex + 1, imagePath, fileInfo.Length));
            }

            if (rendered.Count == 0)
            {
                throw new PdfExtractionException(
                    PdfExtractionFailureKind.ScannedImageOnly,
                    "PDF OCR fallback could not render any pages.");
            }

            return rendered;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (PdfExtractionException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new PdfExtractionException(
                PdfExtractionFailureKind.Malformed,
                "PDF OCR fallback could not render pages.",
                ex);
        }
    }

    private static async Task WriteBmpAsync(
        string path,
        int width,
        int height,
        byte[] pixels,
        CancellationToken cancellationToken)
    {
        var expectedPixelBytes = checked(width * height * (BitsPerPixel / 8));
        if (pixels.Length != expectedPixelBytes)
        {
            throw new PdfExtractionException(
                PdfExtractionFailureKind.Malformed,
                "Rendered OCR page image has an invalid pixel buffer.");
        }

        var header = new byte[BmpHeaderBytes];
        header[0] = (byte)'B';
        header[1] = (byte)'M';
        BinaryPrimitives.WriteInt32LittleEndian(header.AsSpan(2, 4), BmpHeaderBytes + pixels.Length);
        BinaryPrimitives.WriteInt32LittleEndian(header.AsSpan(10, 4), BmpHeaderBytes);
        BinaryPrimitives.WriteInt32LittleEndian(header.AsSpan(14, 4), 40);
        BinaryPrimitives.WriteInt32LittleEndian(header.AsSpan(18, 4), width);
        BinaryPrimitives.WriteInt32LittleEndian(header.AsSpan(22, 4), -height);
        BinaryPrimitives.WriteInt16LittleEndian(header.AsSpan(26, 2), 1);
        BinaryPrimitives.WriteInt16LittleEndian(header.AsSpan(28, 2), BitsPerPixel);
        BinaryPrimitives.WriteInt32LittleEndian(header.AsSpan(34, 4), pixels.Length);

        await using var stream = new FileStream(
            path,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 81920,
            options: FileOptions.Asynchronous | FileOptions.SequentialScan);
        await stream.WriteAsync(header.AsMemory(), cancellationToken);
        await stream.WriteAsync(pixels.AsMemory(), cancellationToken);
    }
}
