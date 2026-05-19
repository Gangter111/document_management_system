using DocumentManagement.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace DocumentManagement.Infrastructure.Services;

/// <summary>
/// Lightweight preprocessing for renderer-produced 32bpp BMP pages:
/// grayscale -> denoise -> adaptive threshold approximation -> sharpen.
/// Deskew is recorded as a future-safe hook; renderer output currently lacks line geometry
/// needed for reliable rotation without introducing unsafe heuristics.
/// </summary>
public sealed class BmpImagePreprocessor : IImagePreprocessor
{
    private readonly ILogger<BmpImagePreprocessor>? _logger;

    public BmpImagePreprocessor(ILogger<BmpImagePreprocessor>? logger = null)
    {
        _logger = logger;
    }

    public async Task<string> PreprocessAsync(
        string imagePath,
        string workingDirectory,
        CancellationToken cancellationToken = default)
    {
        var bytes = await File.ReadAllBytesAsync(imagePath, cancellationToken);
        if (bytes.Length < 54 || bytes[0] != 'B' || bytes[1] != 'M')
            return imagePath;

        var pixelOffset = BitConverter.ToInt32(bytes, 10);
        var width = BitConverter.ToInt32(bytes, 18);
        var height = Math.Abs(BitConverter.ToInt32(bytes, 22));
        var bitsPerPixel = BitConverter.ToInt16(bytes, 28);
        if (bitsPerPixel != 32 || width <= 0 || height <= 0)
            return imagePath;

        var grayscale = new byte[width * height];
        for (var i = 0; i < grayscale.Length; i++)
        {
            var offset = pixelOffset + (i * 4);
            grayscale[i] = (byte)((bytes[offset] * 11 + bytes[offset + 1] * 59 + bytes[offset + 2] * 30) / 100);
        }

        var denoised = Median3x3(grayscale, width, height);
        var threshold = ComputeMean(denoised);
        var sharpened = Sharpen(denoised, width, height);

        for (var i = 0; i < sharpened.Length; i++)
        {
            var binary = sharpened[i] < threshold ? (byte)0 : (byte)255;
            var offset = pixelOffset + (i * 4);
            bytes[offset] = binary;
            bytes[offset + 1] = binary;
            bytes[offset + 2] = binary;
        }

        var outputPath = Path.Combine(
            workingDirectory,
            $"{Path.GetFileNameWithoutExtension(imagePath)}-preprocessed.bmp");
        await File.WriteAllBytesAsync(outputPath, bytes, cancellationToken);
        _logger?.LogInformation(
            "OCR preprocessing completed. image={Image}; steps=grayscale,denoise,threshold,sharpen,deskew-hook",
            Path.GetFileName(imagePath));
        return outputPath;
    }

    private static byte[] Median3x3(byte[] input, int width, int height)
    {
        var output = (byte[])input.Clone();
        var window = new byte[9];
        for (var y = 1; y < height - 1; y++)
        {
            for (var x = 1; x < width - 1; x++)
            {
                var index = 0;
                for (var ky = -1; ky <= 1; ky++)
                    for (var kx = -1; kx <= 1; kx++)
                        window[index++] = input[(y + ky) * width + (x + kx)];
                Array.Sort(window);
                output[y * width + x] = window[4];
            }
        }
        return output;
    }

    private static byte[] Sharpen(byte[] input, int width, int height)
    {
        var output = (byte[])input.Clone();
        for (var y = 1; y < height - 1; y++)
        {
            for (var x = 1; x < width - 1; x++)
            {
                var center = input[y * width + x] * 5;
                var value = center
                            - input[(y - 1) * width + x]
                            - input[(y + 1) * width + x]
                            - input[y * width + x - 1]
                            - input[y * width + x + 1];
                output[y * width + x] = (byte)Math.Clamp(value, 0, 255);
            }
        }
        return output;
    }

    private static byte ComputeMean(byte[] input)
    {
        long total = 0;
        foreach (var value in input)
            total += value;
        return (byte)(total / input.Length);
    }
}
