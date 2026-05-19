using DocumentManagement.Intelligence.Models;
using DocumentManagement.Intelligence.Results;
using System.Globalization;
using System.Text;

namespace DocumentManagement.Intelligence.Review;

public static class ReviewOverlayMapper
{
    public static IReadOnlyList<ReviewPageOverlay> CreatePages(
        IReadOnlyList<ReviewField> fields,
        IReadOnlyList<ReviewCandidate> rejectedCandidates,
        IReadOnlyList<DocumentPageMetrics> pageMetrics)
    {
        var regions = OrderOverlayRegions(fields
            .Where(field => field.Status is ReviewStatus.Accepted or ReviewStatus.LowConfidence or ReviewStatus.Rejected)
            .SelectMany((field, index) => CreateFieldRegions(field, index))
            .Concat(rejectedCandidates.SelectMany((candidate, index) => CreateCandidateRegions(candidate, index)))
            .ToArray());

        var metricsByPage = pageMetrics.ToDictionary(page => page.PageIndex);
        var pageIndexes = regions
            .Select(region => region.PageIndex)
            .Concat(metricsByPage.Keys)
            .Distinct()
            .OrderBy(pageIndex => pageIndex)
            .ToArray();

        return pageIndexes
            .Select(pageIndex =>
            {
                var pageRegions = regions
                    .Where(region => region.PageIndex == pageIndex)
                    .ToArray();

                var metrics = ResolvePageMetrics(pageIndex, metricsByPage, pageRegions);
                return new ReviewPageOverlay(pageIndex, metrics.Width, metrics.Height, pageRegions);
            })
            .ToArray();
    }

    private static IReadOnlyList<ReviewOverlayRegion> OrderOverlayRegions(IReadOnlyList<ReviewOverlayRegion> regions)
    {
        var evidenceRegions = regions
            .Select(region => new ReviewEvidenceRegion(
                region.PageIndex,
                region.BoundingBox,
                region.SourceText,
                region.BlockType,
                0,
                region.IsPrimary,
                region.Role,
                region.Provenance,
                ResolverTieKey(region)))
            .ToArray();
        var regionByEvidence = new Dictionary<ReviewEvidenceRegion, ReviewOverlayRegion>(ReferenceEqualityComparer.Instance);
        for (var index = 0; index < evidenceRegions.Length; index++)
        {
            regionByEvidence[evidenceRegions[index]] = regions[index];
        }

        return ReviewEvidenceResolver.OrderReviewEvidence(evidenceRegions)
            .Select(evidence => regionByEvidence[evidence])
            .ToArray();
    }

    private static string ResolverTieKey(ReviewOverlayRegion region) =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"{ReviewOverlayIdentity.SourceKey(region)}|{region.SemanticGroupId}|{region.FieldName}|{region.HighlightType}|{region.Role}|{region.RejectReason}");

    public static ReviewPageOverlay ApplyFilter(ReviewPageOverlay page, ReviewOverlayFilter filter)
    {
        var regions = page.Regions
            .Where(region => IsVisible(region, filter))
            .ToArray();

        return page with { Regions = regions };
    }

    public static BoundingBox MapToViewport(
        BoundingBox source,
        double sourceWidth,
        double sourceHeight,
        double viewportWidth,
        double viewportHeight,
        double zoomFactor = 1.0)
    {
        if (sourceWidth <= 0 || sourceHeight <= 0 || viewportWidth <= 0 || viewportHeight <= 0)
        {
            return new BoundingBox(0, 0, 0, 0);
        }

        var sanitizedZoom = double.IsFinite(zoomFactor) && zoomFactor > 0
            ? Math.Min(4, Math.Max(0.25, zoomFactor))
            : 1.0;
        var scale = Math.Min(viewportWidth / sourceWidth, viewportHeight / sourceHeight) * sanitizedZoom;
        var renderedWidth = sourceWidth * scale;
        var renderedHeight = sourceHeight * scale;
        var offsetX = (viewportWidth - renderedWidth) / 2;
        var offsetY = (viewportHeight - renderedHeight) / 2;

        return new BoundingBox(
            offsetX + (source.X1 * scale),
            offsetY + (source.Y1 * scale),
            offsetX + (source.X2 * scale),
            offsetY + (source.Y2 * scale));
    }

    private static IEnumerable<ReviewOverlayRegion> CreateFieldRegions(ReviewField field, int index)
    {
        if (field.Status == ReviewStatus.Empty)
        {
            yield break;
        }

        var evidenceRegions = field.EvidenceRegions.Count == 0 && field.SourceBoundingBox is not null
            ? new[] { new ReviewEvidenceRegion(field.PageIndex, field.SourceBoundingBox, field.SourceText, field.SourceBlockType, 0, true) }
            : field.EvidenceRegions;

        var orderedEvidence = ReviewEvidenceResolver.OrderReviewEvidence(evidenceRegions).ToArray();
        var primaryEvidence = ReviewEvidenceResolver.ResolvePrimary(orderedEvidence);
        var groupId = ResolveGroupId(
            "field",
            field.Name,
            field.Value,
            field.Status.ToString());
        for (var evidenceIndex = 0; evidenceIndex < orderedEvidence.Length; evidenceIndex++)
        {
            var evidence = orderedEvidence[evidenceIndex];
            if (evidence.BoundingBox is null)
            {
                continue;
            }

            var role = evidence.Role;
            yield return new ReviewOverlayRegion(
                $"{groupId}:evidence:{evidenceIndex}:{ReviewOverlayIdentity.StableHash(EvidenceKey(evidence))}",
                field.Name,
                Math.Max(0, evidence.PageIndex),
                evidence.BoundingBox,
                field.Confidence,
                string.Join("; ", field.Reasoning),
                evidence.SourceText,
                evidence.BlockType,
                ToHighlightType(field.Status),
                groupId,
                ReviewEvidenceResolver.IsCanonicalPrimary(evidence, primaryEvidence),
                role,
                evidence.Provenance);
        }
    }

    private static IEnumerable<ReviewOverlayRegion> CreateCandidateRegions(ReviewCandidate candidate, int index)
    {
        if (candidate.Status != ReviewStatus.Rejected)
        {
            yield break;
        }

        var evidenceRegions = candidate.EvidenceRegions.Count == 0 && candidate.SourceBoundingBox is not null
            ? new[] { new ReviewEvidenceRegion(candidate.PageIndex, candidate.SourceBoundingBox, candidate.SourceText, candidate.SourceBlockType, 0, true) }
            : candidate.EvidenceRegions;

        var orderedEvidence = ReviewEvidenceResolver.OrderReviewEvidence(evidenceRegions).ToArray();
        var primaryEvidence = ReviewEvidenceResolver.ResolvePrimary(orderedEvidence);
        var groupId = ResolveGroupId(
            "candidate",
            candidate.FieldName,
            candidate.Value,
            string.Join("|", candidate.RejectReasons));
        for (var evidenceIndex = 0; evidenceIndex < orderedEvidence.Length; evidenceIndex++)
        {
            var evidence = orderedEvidence[evidenceIndex];
            if (evidence.BoundingBox is null)
            {
                continue;
            }

            var role = evidence.Role;
            yield return new ReviewOverlayRegion(
                $"{groupId}:evidence:{evidenceIndex}:{ReviewOverlayIdentity.StableHash(EvidenceKey(evidence))}",
                candidate.FieldName,
                Math.Max(0, evidence.PageIndex),
                evidence.BoundingBox,
                candidate.Confidence,
                string.Join("; ", candidate.RejectReasons),
                evidence.SourceText,
                evidence.BlockType,
                OverlayHighlightType.Rejected,
                groupId,
                ReviewEvidenceResolver.IsCanonicalPrimary(evidence, primaryEvidence),
                role,
                evidence.Provenance);
        }
    }

    private static string ResolveGroupId(
        string kind,
        string name,
        string value,
        string discriminator)
    {
        var basis = new StringBuilder()
            .Append(kind).Append('|')
            .Append(name).Append('|')
            .Append(value).Append('|')
            .Append(discriminator);

        return $"{kind}:{ReviewOverlayIdentity.StableHash(basis.ToString())}";
    }

    private static string EvidenceKey(ReviewEvidenceRegion evidence) =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"{Math.Max(0, evidence.PageIndex)}:{evidence.ReadingOrder}:{evidence.BoundingBox?.X1 ?? -1:0.###}:{evidence.BoundingBox?.Y1 ?? -1:0.###}:{evidence.BoundingBox?.X2 ?? -1:0.###}:{evidence.BoundingBox?.Y2 ?? -1:0.###}:{evidence.BlockType}:{evidence.SourceText}:{evidence.Role}");

    private static OverlayHighlightType ToHighlightType(ReviewStatus status) =>
        status switch
        {
            ReviewStatus.Accepted => OverlayHighlightType.Accepted,
            ReviewStatus.LowConfidence => OverlayHighlightType.LowConfidence,
            ReviewStatus.Rejected => OverlayHighlightType.Rejected,
            _ => OverlayHighlightType.LowConfidence
        };

    private static bool IsVisible(ReviewOverlayRegion region, ReviewOverlayFilter filter)
    {
        if (filter.ShowLowConfidenceOnly)
        {
            return region.HighlightType == OverlayHighlightType.LowConfidence;
        }

        return region.HighlightType switch
        {
            OverlayHighlightType.Accepted => filter.ShowAccepted,
            OverlayHighlightType.LowConfidence => filter.ShowAccepted,
            OverlayHighlightType.Rejected => filter.ShowRejected,
            _ => false
        };
    }

    private static DocumentPageMetrics ResolvePageMetrics(
        int pageIndex,
        IReadOnlyDictionary<int, DocumentPageMetrics> metricsByPage,
        IReadOnlyList<ReviewOverlayRegion> pageRegions)
    {
        if (metricsByPage.TryGetValue(pageIndex, out var metrics) && metrics.Width > 0 && metrics.Height > 0)
        {
            return metrics;
        }

        var width = pageRegions.Count == 0 ? 1000 : pageRegions.Max(region => region.BoundingBox.X2);
        var height = pageRegions.Count == 0 ? 1400 : pageRegions.Max(region => region.BoundingBox.Y2);
        return new DocumentPageMetrics(pageIndex, Math.Max(1, width), Math.Max(1, height));
    }
}
