namespace DocumentManagement.Intelligence.Review;

public sealed record ReviewOverlayFocusState(
    string? RegionId,
    string? SemanticGroupId,
    string? EvidenceKey,
    string? SelectionKey,
    string? FieldName,
    OverlayHighlightType? HighlightType,
    int PageIndex)
{
    public static ReviewOverlayFocusState FromRegion(ReviewOverlayRegion region) =>
        new(
            region.Id,
            region.SemanticGroupId,
            ReviewOverlayIdentity.EvidenceKey(region),
            ReviewOverlayIdentity.SelectionKey(region),
            region.FieldName,
            region.HighlightType,
            region.PageIndex);
}
