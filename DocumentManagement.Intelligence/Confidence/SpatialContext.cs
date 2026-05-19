using DocumentManagement.Intelligence.Models;

namespace DocumentManagement.Intelligence.Confidence;

public sealed record SpatialContext(
    bool IsTopLeft,
    bool IsTopRight,
    bool IsBottomLeft,
    bool IsBottomRight,
    bool IsCentered,
    double HorizontalDistanceFromCenter,
    double VerticalPosition)
{
    public static SpatialContext FromBlock(DocumentBlock block, DocumentStructure document)
    {
        var pageWidth = Math.Max(1, document.PageWidth);
        var pageHeight = Math.Max(1, document.PageHeight);
        var centerBandLeft = pageWidth * 0.35;
        var centerBandRight = pageWidth * 0.65;

        return new SpatialContext(
            block.BoundingBox.CenterY <= pageHeight * 0.32 && block.BoundingBox.CenterX <= pageWidth * 0.45,
            block.BoundingBox.CenterY <= pageHeight * 0.32 && block.BoundingBox.CenterX >= pageWidth * 0.55,
            block.BoundingBox.CenterY >= pageHeight * 0.62 && block.BoundingBox.CenterX <= pageWidth * 0.45,
            block.BoundingBox.CenterY >= pageHeight * 0.62 && block.BoundingBox.CenterX >= pageWidth * 0.55,
            block.BoundingBox.CenterX >= centerBandLeft && block.BoundingBox.CenterX <= centerBandRight,
            Math.Abs(block.BoundingBox.CenterX - (pageWidth / 2)) / pageWidth,
            block.BoundingBox.CenterY / pageHeight);
    }
}
