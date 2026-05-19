using DocumentManagement.Intelligence.Models;
using DocumentManagement.Intelligence.Results;
using System.Globalization;

namespace DocumentManagement.Intelligence.Review;

public sealed record ReviewReplayStep(
    string Code,
    string Subject,
    string Reason,
    string Before = "",
    string After = "");

public sealed record ReviewReplayResult(
    bool Succeeded,
    ReviewOverlayFocusState? Focus,
    IReadOnlyList<ReviewReplayStep> Steps);

public sealed record ReviewAuditSnapshot(
    IReadOnlyList<string> ResolverOrdering,
    IReadOnlyList<string> SemanticGrouping,
    IReadOnlyList<string> OverlayTraversal,
    IReadOnlyList<string> FocusRestoration,
    IReadOnlyList<string> ProvenanceDiagnostics);

public sealed record ReviewSemanticDiff(
    IReadOnlyList<string> OrderingDeltas,
    IReadOnlyList<string> PrimaryEvidenceDeltas,
    IReadOnlyList<string> SemanticGroupDeltas,
    IReadOnlyList<string> FocusRestorationDeltas);

public static class ReviewSemanticAudit
{
    // Invariant: audit, replay, and diff output is observational only.
    // These snapshots explain resolver/review state and must never influence semantic authority.
    public static ReviewAuditSnapshot CreateSnapshot(
        IReadOnlyList<ReviewEvidenceRegion> resolverEvidence,
        IReadOnlyList<ReviewPageOverlay> pages,
        IReadOnlyList<ReviewReplayStep>? focusRestoration = null)
    {
        return new ReviewAuditSnapshot(
            CreateResolverOrdering(resolverEvidence),
            CreateSemanticGrouping(pages),
            CreateOverlayTraversal(pages),
            CreateFocusRestoration(focusRestoration ?? Array.Empty<ReviewReplayStep>()),
            CreateProvenanceDiagnostics(resolverEvidence, pages));
    }

    public static IReadOnlyList<string> CreateResolverOrdering(IReadOnlyList<ReviewEvidenceRegion> evidence)
    {
        var ordered = ReviewEvidenceResolver.OrderReviewEvidence(evidence).ToArray();
        return ordered
            .Select((item, index) => string.Create(
                CultureInfo.InvariantCulture,
                $"{index:000}|page={Math.Max(0, item.PageIndex)}|role={item.Role}|semantic={item.Provenance?.SemanticRole ?? ""}|bbox={Format(item.BoundingBox)}|source={Compact(item.SourceText)}|diagnostics={DiagnosticCodes(evidence, item)}"))
            .ToArray();
    }

    public static IReadOnlyList<string> CreateSemanticGrouping(IReadOnlyList<ReviewPageOverlay> pages) =>
        pages
            .SelectMany(page => page.Regions)
            .GroupBy(region => region.SemanticGroupId, StringComparer.Ordinal)
            .OrderBy(group => FirstRegionIndex(pages, group.First().Id))
            .Select((group, index) =>
            {
                var primary = group.FirstOrDefault(region => region.IsPrimary) ?? group.First();
                var members = group
                    .OrderBy(region => FirstRegionIndex(pages, region.Id))
                    .Select(region => $"{region.Role}:{region.Provenance?.SemanticRole ?? ""}:{Format(region.BoundingBox)}")
                    .ToArray();
                return $"{index:000}|group={index:000}|field={primary.FieldName}|primary={primary.Provenance?.SemanticRole ?? ""}:{Format(primary.BoundingBox)}|members={string.Join(",", members)}";
            })
            .ToArray();

    public static IReadOnlyList<string> CreateOverlayTraversal(IReadOnlyList<ReviewPageOverlay> pages) =>
        pages
            .SelectMany(page => page.Regions)
            .Select((region, index) => string.Create(
                CultureInfo.InvariantCulture,
                $"{index:000}|page={region.PageIndex}|field={region.FieldName}|highlight={region.HighlightType}|role={region.Role}|semantic={region.Provenance?.SemanticRole ?? ""}|primary={region.IsPrimary}|bbox={Format(region.BoundingBox)}"))
            .ToArray();

    public static IReadOnlyList<string> CreateFocusRestoration(IReadOnlyList<ReviewReplayStep> steps) =>
        steps
            .Select((step, index) => $"{index:000}|{step.Code}|subject={step.Subject}|reason={step.Reason}|before={step.Before}|after={step.After}")
            .ToArray();

    public static IReadOnlyList<string> CreateProvenanceDiagnostics(
        IReadOnlyList<ReviewEvidenceRegion> resolverEvidence,
        IReadOnlyList<ReviewPageOverlay> pages)
    {
        var resolverLines = ReviewEvidenceResolver.OrderReviewEvidence(resolverEvidence)
            .Select((evidence, index) => $"{index:000}|resolver|semantic={evidence.Provenance?.SemanticRole ?? ""}|codes={DiagnosticCodes(resolverEvidence, evidence)}");
        var overlayLines = pages
            .SelectMany(page => page.Regions)
            .Select((region, index) => $"{index:000}|overlay|semantic={region.Provenance?.SemanticRole ?? ""}|codes={string.Join(",", (region.Provenance?.Diagnostics.Select(item => item.Code) ?? Array.Empty<string>()).OrderBy(code => code, StringComparer.Ordinal))}");

        return resolverLines
            .Concat(overlayLines)
            .Where(line => !line.EndsWith("codes=", StringComparison.Ordinal))
            .OrderBy(line => line, StringComparer.Ordinal)
            .ToArray();
    }

    public static ReviewSemanticDiff Compare(ReviewAuditSnapshot previous, ReviewAuditSnapshot current)
    {
        return new ReviewSemanticDiff(
            CompareLines("ordering", previous.ResolverOrdering, current.ResolverOrdering),
            ComparePrimaryEvidence(previous.SemanticGrouping, current.SemanticGrouping),
            CompareLines("group", previous.SemanticGrouping, current.SemanticGrouping),
            CompareLines("focus", previous.FocusRestoration, current.FocusRestoration));
    }

    private static IReadOnlyList<string> CompareLines(string kind, IReadOnlyList<string> previous, IReadOnlyList<string> current)
    {
        var deltas = new List<string>();
        var count = Math.Max(previous.Count, current.Count);
        for (var i = 0; i < count; i++)
        {
            var before = i < previous.Count ? previous[i] : "<missing>";
            var after = i < current.Count ? current[i] : "<missing>";
            if (!string.Equals(before, after, StringComparison.Ordinal))
            {
                deltas.Add($"{kind}:{i:000}|before={before}|after={after}");
            }
        }

        return deltas.ToArray();
    }

    private static IReadOnlyList<string> ComparePrimaryEvidence(IReadOnlyList<string> previousGroups, IReadOnlyList<string> currentGroups)
    {
        var deltas = new List<string>();
        var currentByField = currentGroups
            .GroupBy(ExtractField, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
        foreach (var previous in previousGroups.OrderBy(ExtractField, StringComparer.Ordinal))
        {
            var field = ExtractField(previous);
            if (!currentByField.TryGetValue(field, out var current))
            {
                continue;
            }

            var previousPrimary = ExtractPart(previous, "primary=");
            var currentPrimary = ExtractPart(current, "primary=");
            if (!string.Equals(previousPrimary, currentPrimary, StringComparison.Ordinal))
            {
                deltas.Add($"primary:{field}|before={previousPrimary}|after={currentPrimary}");
            }
        }

        return deltas.ToArray();
    }

    private static string DiagnosticCodes(IReadOnlyList<ReviewEvidenceRegion> evidence, ReviewEvidenceRegion target) =>
        string.Join(
            ",",
            ReviewEvidenceResolver.ExplainReviewEvidence(evidence, target)
                .Select(item => item.Code)
                .OrderBy(code => code, StringComparer.Ordinal));

    private static int FirstRegionIndex(IReadOnlyList<ReviewPageOverlay> pages, string regionId)
    {
        var index = 0;
        foreach (var page in pages)
        {
            for (var i = 0; i < page.Regions.Count; i++)
            {
                if (string.Equals(page.Regions[i].Id, regionId, StringComparison.Ordinal))
                {
                    return index;
                }

                index++;
            }
        }

        return int.MaxValue;
    }

    private static string ExtractField(string value) => ExtractPart(value, "field=");

    private static string ExtractPart(string value, string marker)
    {
        var start = value.IndexOf(marker, StringComparison.Ordinal);
        if (start < 0)
        {
            return string.Empty;
        }

        start += marker.Length;
        var end = value.IndexOf('|', start);
        return end < 0 ? value[start..] : value[start..end];
    }

    private static string Format(BoundingBox? bbox) =>
        bbox is null
            ? "no-bbox"
            : string.Create(CultureInfo.InvariantCulture, $"{bbox.X1:0},{bbox.Y1:0},{bbox.X2:0},{bbox.Y2:0}");

    private static string Compact(string value)
    {
        var compact = string.Join(" ", value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        return compact.Length <= 48 ? compact : compact[..48];
    }
}
