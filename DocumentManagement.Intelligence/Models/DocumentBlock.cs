namespace DocumentManagement.Intelligence.Models;

public sealed record DocumentBlock(
    int PageNumber,
    int ReadingOrder,
    string BlockType,
    string RawText,
    string NormalizedText,
    BoundingBox BoundingBox,
    IReadOnlyList<DocumentLineMetadata>? Lines = null,
    IReadOnlyList<DocumentSpanMetadata>? Spans = null,
    IReadOnlyDictionary<string, string>? NonTextMetadata = null)
{
    public bool IsTop(double pageHeight) => BoundingBox.CenterY <= pageHeight * 0.32;

    public bool IsBottom(double pageHeight) => BoundingBox.CenterY >= pageHeight * 0.62;

    public bool IsLeft(double pageWidth) => BoundingBox.CenterX <= pageWidth * 0.45;

    public bool IsRight(double pageWidth) => BoundingBox.CenterX >= pageWidth * 0.55;
}

public sealed record DocumentLineMetadata(string Text, BoundingBox? BoundingBox = null);

public sealed record DocumentSpanMetadata(string Text, BoundingBox? BoundingBox = null, double? Confidence = null);
