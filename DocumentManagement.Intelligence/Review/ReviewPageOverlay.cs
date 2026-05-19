namespace DocumentManagement.Intelligence.Review;

public sealed record ReviewPageOverlay(
    int PageIndex,
    double SourceWidth,
    double SourceHeight,
    IReadOnlyList<ReviewOverlayRegion> Regions);
