using DocumentManagement.Intelligence.Results;

namespace DocumentManagement.Intelligence.Review;

public sealed class ReviewOverlayNavigator
{
    // Invariant: overlay traversal consumes resolver-normalized overlay order.
    // It does not re-rank semantics and must not become an alternate ordering authority.
    private readonly IReadOnlyList<ReviewPageOverlay> _pages;
    private readonly IReadOnlyList<ReviewOverlayRegion> _orderedRegions;
    private readonly Dictionary<int, ReviewPageOverlay> _pageByIndex;
    private readonly Dictionary<string, ReviewOverlayRegion> _regionById;
    private readonly Dictionary<string, ReviewOverlayRegion> _regionByEvidenceKey;
    private readonly Dictionary<string, ReviewOverlayRegion> _regionBySelectionKey;

    public ReviewOverlayNavigator(IReadOnlyList<ReviewPageOverlay> pages)
    {
        _pages = pages.OrderBy(page => page.PageIndex).ToArray();
        _orderedRegions = _pages.SelectMany(page => page.Regions).ToArray();
        _pageByIndex = new Dictionary<int, ReviewPageOverlay>(_pages.Count);
        _regionById = new Dictionary<string, ReviewOverlayRegion>(StringComparer.Ordinal);
        _regionByEvidenceKey = new Dictionary<string, ReviewOverlayRegion>(StringComparer.Ordinal);
        _regionBySelectionKey = new Dictionary<string, ReviewOverlayRegion>(StringComparer.Ordinal);
        for (var pageIndex = 0; pageIndex < _pages.Count; pageIndex++)
        {
            var page = _pages[pageIndex];
            _pageByIndex[page.PageIndex] = page;
            for (var regionIndex = 0; regionIndex < page.Regions.Count; regionIndex++)
            {
                var region = page.Regions[regionIndex];
                _regionById.TryAdd(region.Id, region);
                _regionByEvidenceKey.TryAdd(ReviewOverlayIdentity.EvidenceKey(region), region);
                var selectionKey = ReviewOverlayIdentity.SelectionKey(region);
                if (region.IsPrimary)
                {
                    _regionBySelectionKey[selectionKey] = region;
                }
                else
                {
                    _regionBySelectionKey.TryAdd(selectionKey, region);
                }
            }
        }

        CurrentPageIndex = _pages.FirstOrDefault()?.PageIndex ?? 0;
    }

    public int CurrentPageIndex { get; private set; }

    public string? ActiveRegionId { get; private set; }

    public string? ActiveSemanticGroupId { get; private set; }

    public ReviewPageOverlay? CurrentPage =>
        _pageByIndex.GetValueOrDefault(CurrentPageIndex);

    public bool NavigateToRegion(string? regionId)
    {
        return NavigateToRegionWithReplay(regionId).Succeeded;
    }

    public ReviewReplayResult NavigateToRegionWithReplay(string? regionId)
    {
        if (string.IsNullOrWhiteSpace(regionId))
        {
            ActiveRegionId = null;
            ActiveSemanticGroupId = null;
            return Replay(false, null, new ReviewReplayStep(
                "selection-empty",
                string.Empty,
                "region selection was empty"));
        }

        if (!_regionById.TryGetValue(regionId, out var region))
        {
            ActiveRegionId = null;
            ActiveSemanticGroupId = null;
            return Replay(false, null, new ReviewReplayStep(
                "selection-stale",
                regionId,
                "region id was not present in resolver-ordered overlay"));
        }

        var before = CaptureFocus();
        Activate(region);
        return Replay(true, CaptureFocus(), new ReviewReplayStep(
            "selection-region-id",
            region.Id,
            "region selected from resolver-ordered overlay",
            FormatFocus(before),
            FormatFocus(CaptureFocus())));
    }

    public ReviewOverlayFocusState? CaptureFocus() =>
        ActiveRegionId is not null && _regionById.TryGetValue(ActiveRegionId, out var region)
            ? ReviewOverlayFocusState.FromRegion(region)
            : null;

    public bool RestoreFocus(ReviewOverlayFocusState? focus)
    {
        return RestoreFocusWithReplay(focus).Succeeded;
    }

    public ReviewReplayResult RestoreFocusWithReplay(ReviewOverlayFocusState? focus)
    {
        // Invariant: focus restoration prefers semantic identity before fallbacks.
        // It must never become coordinate-only; coordinates are only part of stable evidence identity.
        if (focus is null)
        {
            return Replay(false, null, new ReviewReplayStep(
                "focus-none",
                string.Empty,
                "no previous focus was available"));
        }

        var before = CaptureFocus();
        if (!string.IsNullOrWhiteSpace(focus.RegionId) &&
            _regionById.TryGetValue(focus.RegionId, out var byId))
        {
            Activate(byId);
            return Replay(true, CaptureFocus(), new ReviewReplayStep(
                "focus-restored-region-id",
                byId.Id,
                "focus restored by stable overlay region id",
                FormatFocus(before),
                FormatFocus(CaptureFocus())));
        }

        if (!string.IsNullOrWhiteSpace(focus.EvidenceKey) &&
            _regionByEvidenceKey.TryGetValue(focus.EvidenceKey, out var byEvidence))
        {
            Activate(byEvidence);
            return Replay(true, CaptureFocus(), new ReviewReplayStep(
                "focus-restored-evidence-key",
                byEvidence.Id,
                "focus restored by stable resolver evidence identity",
                FormatFocus(before),
                FormatFocus(CaptureFocus())));
        }

        if (!string.IsNullOrWhiteSpace(focus.SelectionKey) &&
            _regionBySelectionKey.TryGetValue(focus.SelectionKey, out var bySelection))
        {
            Activate(bySelection);
            return Replay(true, CaptureFocus(), new ReviewReplayStep(
                "focus-restored-selection-key",
                bySelection.Id,
                "focus restored by field/status/semantic selection identity",
                FormatFocus(before),
                FormatFocus(CaptureFocus())));
        }

        var fallback = _orderedRegions.FirstOrDefault(region =>
            string.Equals(region.SemanticGroupId, focus.SemanticGroupId, StringComparison.Ordinal) &&
            (string.IsNullOrWhiteSpace(focus.FieldName) || string.Equals(region.FieldName, focus.FieldName, StringComparison.Ordinal)) &&
            (focus.HighlightType is null || region.HighlightType == focus.HighlightType) &&
            region.IsPrimary);
        if (fallback is not null)
        {
            Activate(fallback);
            return Replay(true, CaptureFocus(), new ReviewReplayStep(
                "focus-restored-semantic-group-primary",
                fallback.Id,
                "focus restored to resolver primary in semantic group",
                FormatFocus(before),
                FormatFocus(CaptureFocus())));
        }

        fallback = _orderedRegions.FirstOrDefault(region =>
            (string.IsNullOrWhiteSpace(focus.FieldName) || string.Equals(region.FieldName, focus.FieldName, StringComparison.Ordinal)) &&
            (focus.HighlightType is null || region.HighlightType == focus.HighlightType) &&
            region.IsPrimary);
        if (fallback is not null)
        {
            Activate(fallback);
            return Replay(true, CaptureFocus(), new ReviewReplayStep(
                "focus-restored-field-primary",
                fallback.Id,
                "focus restored to resolver primary for matching field",
                FormatFocus(before),
                FormatFocus(CaptureFocus())));
        }

        ActiveRegionId = null;
        ActiveSemanticGroupId = null;
        return Replay(false, null, new ReviewReplayStep(
            "focus-stale-cleared",
            focus.RegionId ?? focus.EvidenceKey ?? string.Empty,
            "previous focus did not resolve in refreshed overlay",
            FormatFocus(before)));
    }

    public bool NavigateNextRegion() => NavigateRelativeRegion(1);

    public bool NavigatePreviousRegion() => NavigateRelativeRegion(-1);

    public ReviewReplayResult NavigateNextRegionWithReplay() => NavigateRelativeRegionWithReplay(1, "traversal-next-region");

    public ReviewReplayResult NavigatePreviousRegionWithReplay() => NavigateRelativeRegionWithReplay(-1, "traversal-previous-region");

    public bool NavigateNextSemanticGroup() => NavigateRelativeSemanticGroup(1);

    public bool NavigatePreviousSemanticGroup() => NavigateRelativeSemanticGroup(-1);

    public bool NavigateNextContextualEvidence() => NavigateRelativeRoleEvidence(1, EvidenceRole.Contextual);

    public bool NavigatePreviousContextualEvidence() => NavigateRelativeRoleEvidence(-1, EvidenceRole.Contextual);

    public bool NavigateToPage(int pageIndex)
    {
        if (!_pageByIndex.ContainsKey(pageIndex))
        {
            return false;
        }

        CurrentPageIndex = pageIndex;
        if (ActiveRegionId is not null && !CurrentPageContainsActiveRegion())
        {
            ActiveRegionId = null;
            ActiveSemanticGroupId = null;
        }

        return true;
    }

    private bool NavigateRelativeRegion(int delta)
    {
        return NavigateRelativeRegionWithReplay(delta, delta > 0 ? "traversal-next-region" : "traversal-previous-region").Succeeded;
    }

    private ReviewReplayResult NavigateRelativeRegionWithReplay(int delta, string code)
    {
        if (_orderedRegions.Count == 0)
        {
            return Replay(false, CaptureFocus(), new ReviewReplayStep(code, string.Empty, "overlay traversal has no regions"));
        }

        var before = CaptureFocus();
        var currentIndex = ActiveRegionId is null
            ? -1
            : IndexOfRegion(ActiveRegionId);
        var nextIndex = currentIndex < 0
            ? (delta > 0 ? 0 : _orderedRegions.Count - 1)
            : currentIndex + delta;
        if (nextIndex < 0 || nextIndex >= _orderedRegions.Count)
        {
            return Replay(false, before, new ReviewReplayStep(
                code,
                currentIndex.ToString(System.Globalization.CultureInfo.InvariantCulture),
                "resolver-ordered traversal boundary reached",
                FormatFocus(before)));
        }

        Activate(_orderedRegions[nextIndex]);
        return Replay(true, CaptureFocus(), new ReviewReplayStep(
            code,
            _orderedRegions[nextIndex].Id,
            "focus moved by resolver-ordered overlay traversal",
            FormatFocus(before),
            FormatFocus(CaptureFocus())));
    }

    private bool NavigateRelativeSemanticGroup(int delta)
    {
        if (_orderedRegions.Count == 0)
        {
            return false;
        }

        var groups = _orderedRegions
            .Where(region => !string.IsNullOrWhiteSpace(region.SemanticGroupId))
            .GroupBy(region => region.SemanticGroupId, StringComparer.Ordinal)
            .Select(group => group.FirstOrDefault(region => region.IsPrimary) ?? group.First())
            .ToArray();
        var currentGroup = ActiveSemanticGroupId;
        var currentIndex = string.IsNullOrWhiteSpace(currentGroup)
            ? -1
            : Array.FindIndex(groups, region => string.Equals(region.SemanticGroupId, currentGroup, StringComparison.Ordinal));
        var nextIndex = currentIndex < 0
            ? (delta > 0 ? 0 : groups.Length - 1)
            : currentIndex + delta;
        if (nextIndex < 0 || nextIndex >= groups.Length)
        {
            return false;
        }

        Activate(groups[nextIndex]);
        return true;
    }

    private bool NavigateRelativeRoleEvidence(int delta, EvidenceRole role)
    {
        var groupId = ActiveSemanticGroupId;
        if (string.IsNullOrWhiteSpace(groupId))
        {
            return false;
        }

        var groupRegions = _orderedRegions
            .Where(region => string.Equals(region.SemanticGroupId, groupId, StringComparison.Ordinal) && region.Role == role)
            .ToArray();
        if (groupRegions.Length == 0)
        {
            return false;
        }

        var currentIndex = ActiveRegionId is null
            ? -1
            : Array.FindIndex(groupRegions, region => string.Equals(region.Id, ActiveRegionId, StringComparison.Ordinal));
        var nextIndex = currentIndex < 0
            ? (delta > 0 ? 0 : groupRegions.Length - 1)
            : currentIndex + delta;
        if (nextIndex < 0 || nextIndex >= groupRegions.Length)
        {
            return false;
        }

        Activate(groupRegions[nextIndex]);
        return true;
    }

    private int IndexOfRegion(string regionId)
    {
        for (var i = 0; i < _orderedRegions.Count; i++)
        {
            if (string.Equals(_orderedRegions[i].Id, regionId, StringComparison.Ordinal))
            {
                return i;
            }
        }

        return -1;
    }

    private void Activate(ReviewOverlayRegion region)
    {
        CurrentPageIndex = region.PageIndex;
        ActiveRegionId = region.Id;
        ActiveSemanticGroupId = region.SemanticGroupId;
    }

    private bool CurrentPageContainsActiveRegion()
    {
        var regions = CurrentPage?.Regions;
        if (regions is null || ActiveRegionId is null)
        {
            return false;
        }

        for (var i = 0; i < regions.Count; i++)
        {
            if (string.Equals(regions[i].Id, ActiveRegionId, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static ReviewReplayResult Replay(bool succeeded, ReviewOverlayFocusState? focus, params ReviewReplayStep[] steps) =>
        new(succeeded, focus, steps);

    private static string FormatFocus(ReviewOverlayFocusState? focus) =>
        focus is null
            ? string.Empty
            : $"{focus.PageIndex}|{focus.FieldName}|{focus.HighlightType}|{focus.EvidenceKey}";
}
