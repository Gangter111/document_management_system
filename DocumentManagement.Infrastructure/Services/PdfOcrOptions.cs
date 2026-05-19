namespace DocumentManagement.Infrastructure.Services;

public sealed class PdfOcrOptions
{
    public bool Enabled { get; set; } = true;

    public string TesseractPath { get; set; } = "tesseract";

    public string TessdataPath { get; set; } = @"C:\Program Files\Tesseract-OCR\tessdata";

    public string Language { get; set; } = "vie+eng";

    public string FallbackLanguage { get; set; } = "eng";

    public int PageLimit { get; set; } = 5;

    public int Dpi { get; set; } = 200;

    public int TimeoutSeconds { get; set; } = 20;

    public long MaxRenderedImageBytes { get; set; } = 24L * 1024L * 1024L;

    public int MaxOcrCharacters { get; set; } = 100_000;

    public TimeSpan Timeout => TimeSpan.FromSeconds(Math.Clamp(TimeoutSeconds, 1, 120));

    public int SafePageLimit => Math.Clamp(PageLimit, 1, 20);

    public int SafeDpi => Math.Clamp(Dpi, 96, 300);
}
