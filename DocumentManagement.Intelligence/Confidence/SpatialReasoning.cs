using DocumentManagement.Intelligence.Models;

namespace DocumentManagement.Intelligence.Confidence;

public static class SpatialReasoning
{
    public static bool IsTopLeft(DocumentBlock block, DocumentStructure document) =>
        SpatialContext.FromBlock(block, document).IsTopLeft;

    public static bool IsTopRight(DocumentBlock block, DocumentStructure document) =>
        SpatialContext.FromBlock(block, document).IsTopRight;

    public static bool IsBottomLeft(DocumentBlock block, DocumentStructure document) =>
        SpatialContext.FromBlock(block, document).IsBottomLeft;

    public static bool IsBottomRight(DocumentBlock block, DocumentStructure document) =>
        SpatialContext.FromBlock(block, document).IsBottomRight;

    public static bool IsCentered(DocumentBlock block, DocumentStructure document) =>
        SpatialContext.FromBlock(block, document).IsCentered;

    public static IReadOnlyList<DocumentBlock> NearbyBlocks(
        DocumentBlock source,
        DocumentStructure document,
        double maxHorizontalDistance,
        double maxVerticalDistance) =>
        document.Blocks
            .Where(block => block.PageNumber == source.PageNumber && block.ReadingOrder != source.ReadingOrder)
            .Where(block => Math.Abs(block.BoundingBox.CenterX - source.BoundingBox.CenterX) <= maxHorizontalDistance)
            .Where(block => Math.Abs(block.BoundingBox.CenterY - source.BoundingBox.CenterY) <= maxVerticalDistance)
            .OrderBy(block => Math.Abs(block.BoundingBox.CenterY - source.BoundingBox.CenterY))
            .ThenBy(block => Math.Abs(block.BoundingBox.CenterX - source.BoundingBox.CenterX))
            .ToArray();

    public static IReadOnlyList<DocumentBlock> VerticalGroup(
        DocumentBlock source,
        DocumentStructure document,
        double maxHorizontalDistance) =>
        document.Blocks
            .Where(block => block.PageNumber == source.PageNumber)
            .Where(block => Math.Abs(block.BoundingBox.CenterX - source.BoundingBox.CenterX) <= maxHorizontalDistance)
            .OrderBy(block => block.BoundingBox.Y1)
            .ToArray();
}
