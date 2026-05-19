using DocumentManagement.Intelligence.Results;

namespace DocumentManagement.Intelligence.Review;

public static class ReviewProvenanceDiff
{
    public static IReadOnlyList<EvidenceDiagnostic> Compare(
        IReadOnlyList<ReviewPageOverlay> previousPages,
        IReadOnlyList<ReviewPageOverlay> currentPages,
        ReviewOverlayFocusState? previousFocus = null,
        ReviewOverlayFocusState? restoredFocus = null)
    {
        var diagnostics = new List<EvidenceDiagnostic>(6);
        var previousGroups = GroupRegions(previousPages);
        var currentGroups = GroupRegions(currentPages);

        foreach (var group in previousGroups.OrderBy(item => item.Key, StringComparer.Ordinal))
        {
            if (!currentGroups.TryGetValue(group.Key, out var current))
            {
                continue;
            }

            var previousPrimary = group.Value.FirstOrDefault(region => region.IsPrimary);
            var currentPrimary = current.FirstOrDefault(region => region.IsPrimary);
            if (previousPrimary is not null &&
                currentPrimary is not null &&
                !string.Equals(ReviewOverlayIdentity.EvidenceKey(previousPrimary), ReviewOverlayIdentity.EvidenceKey(currentPrimary), StringComparison.Ordinal))
            {
                diagnostics.Add(new EvidenceDiagnostic(
                    "provenance-diff-primary-changed",
                    $"primary evidence changed for {group.Key} by resolver ordering"));
            }

            var previousOrder = group.Value.Select(ReviewOverlayIdentity.EvidenceKey).ToArray();
            var currentOrder = current.Select(ReviewOverlayIdentity.EvidenceKey).ToArray();
            if (!previousOrder.SequenceEqual(currentOrder, StringComparer.Ordinal))
            {
                diagnostics.Add(new EvidenceDiagnostic(
                    "provenance-diff-ordering-changed",
                    $"evidence ordering changed for {group.Key} by resolver ordering"));
            }

            var currentByKey = current.ToDictionary(ReviewOverlayIdentity.SourceKey, StringComparer.Ordinal);
            foreach (var previous in group.Value)
            {
                if (currentByKey.TryGetValue(ReviewOverlayIdentity.SourceKey(previous), out var currentRegion) &&
                    RoleRank(currentRegion.Role) > RoleRank(previous.Role))
                {
                    diagnostics.Add(new EvidenceDiagnostic(
                        "provenance-diff-evidence-demoted",
                        $"evidence demoted for {group.Key} by resolver role"));
                }
            }
        }

        if (previousFocus is not null && restoredFocus is not null &&
            !string.Equals(previousFocus.EvidenceKey, restoredFocus.EvidenceKey, StringComparison.Ordinal))
        {
            diagnostics.Add(new EvidenceDiagnostic(
                "provenance-diff-focus-moved",
                "overlay focus moved to nearest resolver-ordered evidence"));
        }

        return diagnostics
            .GroupBy(item => item.Code, StringComparer.Ordinal)
            .Select(group => group.First())
            .OrderBy(item => item.Code, StringComparer.Ordinal)
            .ToArray();
    }

    private static Dictionary<string, IReadOnlyList<ReviewOverlayRegion>> GroupRegions(IReadOnlyList<ReviewPageOverlay> pages) =>
        pages
            .SelectMany(page => page.Regions)
            .GroupBy(region => region.SemanticGroupId, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<ReviewOverlayRegion>)group.ToArray(),
                StringComparer.Ordinal);

    private static int RoleRank(EvidenceRole role) =>
        role switch
        {
            EvidenceRole.Primary => 0,
            EvidenceRole.Supporting => 1,
            _ => 2
        };
}
