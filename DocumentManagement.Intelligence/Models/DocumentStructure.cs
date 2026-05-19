namespace DocumentManagement.Intelligence.Models;

public sealed record DocumentStructure(
    IReadOnlyList<DocumentBlock> Blocks,
    IReadOnlyList<DocumentPageMetrics>? ExplicitPages = null)
{
    public double PageWidth => ExplicitPages is { Count: > 0 } && ExplicitPages.Any(page => page.Width > 0)
        ? ExplicitPages.Where(page => page.Width > 0).Max(page => page.Width)
        : Blocks.Count == 0 ? 0 : Blocks.Max(block => block.BoundingBox.X2);

    public double PageHeight => ExplicitPages is { Count: > 0 } && ExplicitPages.Any(page => page.Height > 0)
        ? ExplicitPages.Where(page => page.Height > 0).Max(page => page.Height)
        : Blocks.Count == 0 ? 0 : Blocks.Max(block => block.BoundingBox.Y2);

    public IReadOnlyList<DocumentPageMetrics> Pages =>
        ExplicitPages is { Count: > 0 }
            ? ExplicitPages
                .Where(page => page.Width > 0 && page.Height > 0)
                .OrderBy(page => page.PageIndex)
                .ToArray()
            : Blocks
            .GroupBy(block => block.PageNumber)
            .OrderBy(group => group.Key)
            .Select(group => new DocumentPageMetrics(
                group.Key - 1,
                group.Max(block => block.BoundingBox.X2),
                group.Max(block => block.BoundingBox.Y2)))
            .ToArray();

    public IReadOnlyList<DocumentBlock> ReadingOrderBlocks =>
        Blocks
            .OrderBy(block => block.PageNumber)
            .ThenBy(block => block.BoundingBox.Y1)
            .ThenBy(block => block.BoundingBox.X1)
            .ThenBy(block => block.ReadingOrder)
            .ToArray();
}

public sealed record DocumentPageMetrics(int PageIndex, double Width, double Height);
