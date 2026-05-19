using DocumentManagement.Intelligence.Results;

namespace DocumentManagement.Intelligence.Review;

public sealed record ReviewHealthDiagnostics(
    int StaleRefreshRejections,
    int ReplayInvalidations,
    int OverlayRemapCancellations,
    int FocusRestorationFallbacks,
    bool ResolverConsistencyVerified);

public sealed record ReviewConsistencyCheck(
    string Code,
    bool Passed,
    string Detail);

public sealed record ReviewOperationalSnapshot(
    IReadOnlyList<string> RuntimeRefreshState,
    IReadOnlyList<string> OverlayTraversalState,
    IReadOnlyList<string> FocusRestorationChain,
    IReadOnlyList<string> ReplayInvalidationChain,
    IReadOnlyList<string> SemanticConsistencyResults);

public sealed record ReviewProfilingSnapshot(
    int ResolverEvidenceCount,
    int OverlayRegionCount,
    int OverlayPageCount,
    int ReplayStepCount,
    int FocusRestorationPathCount,
    int OperationalDiagnosticCount);

public static class ReviewOperationalObservability
{
    // Invariant: operational observability is optional and bounded.
    // It verifies and summarizes runtime state without changing resolver or overlay behavior.
    public static ReviewHealthDiagnostics CreateHealthDiagnostics(
        ReviewRuntimeStateSnapshot runtime,
        IReadOnlyList<ReviewReplayStep>? replaySteps = null,
        IReadOnlyList<ReviewConsistencyCheck>? consistencyChecks = null)
    {
        var diagnostics = runtime.Diagnostics;
        var steps = replaySteps ?? Array.Empty<ReviewReplayStep>();
        var checks = consistencyChecks ?? Array.Empty<ReviewConsistencyCheck>();

        return new ReviewHealthDiagnostics(
            Count(diagnostics, "stale-refresh-rejected"),
            Count(diagnostics, "replay-invalidation"),
            Count(diagnostics, "overlay-remap-canceled"),
            steps.Count(IsFocusFallback),
            checks.Count > 0 && checks.All(check => check.Passed));
    }

    public static ReviewOperationalSnapshot CreateSnapshot(
        ReviewRuntimeStateSnapshot runtime,
        IReadOnlyList<ReviewPageOverlay> pages,
        IReadOnlyList<ReviewReplayStep>? replaySteps = null,
        IReadOnlyList<ReviewConsistencyCheck>? consistencyChecks = null)
    {
        var steps = replaySteps ?? Array.Empty<ReviewReplayStep>();
        var checks = consistencyChecks ?? Array.Empty<ReviewConsistencyCheck>();

        return new ReviewOperationalSnapshot(
            CreateRuntimeState(runtime),
            ReviewSemanticAudit.CreateOverlayTraversal(pages),
            ReviewSemanticAudit.CreateFocusRestoration(steps),
            CreateReplayInvalidation(runtime, steps),
            checks
                .OrderBy(check => check.Code, StringComparer.Ordinal)
                .Select(check => $"{check.Code}|passed={check.Passed}|detail={check.Detail}")
                .ToArray());
    }

    public static IReadOnlyList<ReviewConsistencyCheck> VerifyConsistency(
        IReadOnlyList<ReviewEvidenceRegion> resolverEvidence,
        IReadOnlyList<ReviewPageOverlay> pages,
        ReviewReplayResult? focusReplay = null,
        IReadOnlyList<ReviewPageOverlay>? previousPages = null)
    {
        var checks = new List<ReviewConsistencyCheck>(4)
        {
            VerifyOverlayOrderMatchesResolver(resolverEvidence, pages),
            VerifyTraversalOrderMatchesOverlay(pages),
            VerifyFocusRestorationPath(pages, focusReplay)
        };

        if (previousPages is not null)
        {
            checks.Add(VerifySemanticGroupStability(previousPages, pages));
        }

        return checks
            .OrderBy(check => check.Code, StringComparer.Ordinal)
            .ToArray();
    }

    public static IReadOnlyList<string> CreateFailureReasons(
        ReviewRuntimeStateSnapshot runtime,
        IReadOnlyList<ReviewReplayStep>? replaySteps = null)
    {
        var runtimeReasons = runtime.Diagnostics
            .Where(diagnostic => IsFailureCode(diagnostic.Code))
            .Select(diagnostic => $"{diagnostic.Code}|version={diagnostic.Version}|reason={diagnostic.Reason}");
        var replayReasons = (replaySteps ?? Array.Empty<ReviewReplayStep>())
            .Where(step => IsFailureCode(step.Code) || IsFocusFallback(step))
            .Select(step => $"{step.Code}|subject={step.Subject}|reason={step.Reason}");

        return runtimeReasons
            .Concat(replayReasons)
            .OrderBy(line => line, StringComparer.Ordinal)
            .ToArray();
    }

    public static ReviewProfilingSnapshot CreateProfilingSnapshot(
        IReadOnlyList<ReviewEvidenceRegion> resolverEvidence,
        IReadOnlyList<ReviewPageOverlay> pages,
        ReviewRuntimeStateSnapshot runtime,
        IReadOnlyList<ReviewReplayStep>? replaySteps = null)
    {
        var steps = replaySteps ?? Array.Empty<ReviewReplayStep>();
        return new ReviewProfilingSnapshot(
            resolverEvidence.Count,
            pages.Sum(page => page.Regions.Count),
            pages.Count,
            steps.Count,
            steps.Count(IsFocusRestorationStep),
            runtime.Diagnostics.Count);
    }

    private static IReadOnlyList<string> CreateRuntimeState(ReviewRuntimeStateSnapshot runtime)
    {
        var lines = new List<string>(runtime.Diagnostics.Count + 1)
        {
            $"version={runtime.CurrentVersion}|active={runtime.HasActiveRefresh}"
        };
        lines.AddRange(runtime.Diagnostics.Select(diagnostic => $"{diagnostic.Version:000}|{diagnostic.Code}|{diagnostic.Reason}"));
        return lines.ToArray();
    }

    private static IReadOnlyList<string> CreateReplayInvalidation(
        ReviewRuntimeStateSnapshot runtime,
        IReadOnlyList<ReviewReplayStep> replaySteps)
    {
        return runtime.Diagnostics
            .Where(diagnostic => diagnostic.Code.Contains("invalidation", StringComparison.Ordinal) ||
                diagnostic.Code.Contains("stale", StringComparison.Ordinal) ||
                diagnostic.Code.Contains("canceled", StringComparison.Ordinal))
            .Select(diagnostic => $"{diagnostic.Version:000}|{diagnostic.Code}|{diagnostic.Reason}")
            .Concat(replaySteps
                .Where(step => step.Code.Contains("stale", StringComparison.Ordinal) ||
                    step.Code.Contains("fallback", StringComparison.Ordinal) ||
                    IsFocusFallback(step))
                .Select(step => $"replay|{step.Code}|{step.Reason}"))
            .OrderBy(line => line, StringComparer.Ordinal)
            .ToArray();
    }

    private static ReviewConsistencyCheck VerifyOverlayOrderMatchesResolver(
        IReadOnlyList<ReviewEvidenceRegion> resolverEvidence,
        IReadOnlyList<ReviewPageOverlay> pages)
    {
        var expected = CreateResolverOverlayOrder(resolverEvidence, pages);
        var actual = WithOccurrences(OrderedPages(pages)
            .SelectMany(page => page.Regions)
            .Select(ReviewOverlayIdentity.SourceKey));
        var passed = expected.SequenceEqual(actual, StringComparer.Ordinal);

        return new ReviewConsistencyCheck(
            "overlay-order-matches-resolver",
            passed,
            $"mode=resolver-evidence-page-order|expected-count={expected.Length}|actual-count={actual.Length}|first-delta={FirstDelta(expected, actual)}");
    }

    private static string[] CreateResolverOverlayOrder(
        IReadOnlyList<ReviewEvidenceRegion> resolverEvidence,
        IReadOnlyList<ReviewPageOverlay> pages)
    {
        var resolverByPage = ReviewEvidenceResolver.OrderReviewEvidence(resolverEvidence)
            .Where(evidence => evidence.BoundingBox is not null)
            .GroupBy(evidence => Math.Max(0, evidence.PageIndex))
            .ToDictionary(
                group => group.Key,
                group => group.Select(ReviewOverlayIdentity.SourceKey).ToArray());

        var expected = new List<string>();
        foreach (var page in OrderedPages(pages))
        {
            if (resolverByPage.TryGetValue(page.PageIndex, out var pageKeys))
            {
                expected.AddRange(pageKeys);
            }
        }

        return WithOccurrences(expected);
    }

    private static IReadOnlyList<ReviewPageOverlay> OrderedPages(IReadOnlyList<ReviewPageOverlay> pages) =>
        pages
            .OrderBy(page => page.PageIndex)
            .ToArray();

    private static string[] WithOccurrences(IEnumerable<string> keys)
    {
        var counts = new Dictionary<string, int>(StringComparer.Ordinal);
        var result = new List<string>();
        foreach (var key in keys)
        {
            counts.TryGetValue(key, out var count);
            counts[key] = count + 1;
            result.Add($"{key}#{count:000}");
        }

        return result.ToArray();
    }

    private static string FirstDelta(IReadOnlyList<string> expected, IReadOnlyList<string> actual)
    {
        var count = Math.Min(expected.Count, actual.Count);
        for (var i = 0; i < count; i++)
        {
            if (!string.Equals(expected[i], actual[i], StringComparison.Ordinal))
            {
                return $"{i:000}|expected={expected[i]}|actual={actual[i]}";
            }
        }

        if (expected.Count != actual.Count)
        {
            return $"{count:000}|expected={(count < expected.Count ? expected[count] : "<missing>")}|actual={(count < actual.Count ? actual[count] : "<missing>")}";
        }

        return "none";
    }

    private static ReviewConsistencyCheck VerifyTraversalOrderMatchesOverlay(IReadOnlyList<ReviewPageOverlay> pages)
    {
        var expected = pages.SelectMany(page => page.Regions).Select(region => region.Id).ToArray();
        var navigator = new ReviewOverlayNavigator(pages);
        var actual = new List<string>(expected.Length);
        while (navigator.NavigateNextRegion())
        {
            if (navigator.ActiveRegionId is not null)
            {
                actual.Add(navigator.ActiveRegionId);
            }
        }

        return new ReviewConsistencyCheck(
            "traversal-order-matches-overlay",
            expected.SequenceEqual(actual, StringComparer.Ordinal),
            $"expected={string.Join(",", expected)}|actual={string.Join(",", actual)}");
    }

    private static ReviewConsistencyCheck VerifyFocusRestorationPath(
        IReadOnlyList<ReviewPageOverlay> pages,
        ReviewReplayResult? focusReplay)
    {
        if (focusReplay is null)
        {
            return new ReviewConsistencyCheck("focus-restoration-path-valid", true, "no-focus-replay");
        }

        var evidenceKeys = pages
            .SelectMany(page => page.Regions)
            .Select(ReviewOverlayIdentity.EvidenceKey)
            .ToHashSet(StringComparer.Ordinal);
        var valid = !focusReplay.Succeeded ||
            (focusReplay.Focus?.EvidenceKey is not null && evidenceKeys.Contains(focusReplay.Focus.EvidenceKey));

        return new ReviewConsistencyCheck(
            "focus-restoration-path-valid",
            valid,
            focusReplay.Succeeded ? focusReplay.Focus?.EvidenceKey ?? "missing-focus" : "not-restored");
    }

    private static ReviewConsistencyCheck VerifySemanticGroupStability(
        IReadOnlyList<ReviewPageOverlay> previousPages,
        IReadOnlyList<ReviewPageOverlay> currentPages)
    {
        var previous = StableGroupMap(previousPages);
        var current = StableGroupMap(currentPages);
        var mismatches = previous
            .Where(item => current.TryGetValue(item.Key, out var currentGroup) &&
                !string.Equals(item.Value, currentGroup, StringComparison.Ordinal))
            .Select(item => item.Key)
            .OrderBy(key => key, StringComparer.Ordinal)
            .ToArray();

        return new ReviewConsistencyCheck(
            "semantic-group-stable-after-remap",
            mismatches.Length == 0,
            mismatches.Length == 0 ? "stable" : string.Join(",", mismatches));
    }

    private static Dictionary<string, string> StableGroupMap(IReadOnlyList<ReviewPageOverlay> pages) =>
        pages
            .SelectMany(page => page.Regions)
            .Where(region => region.IsPrimary)
            .GroupBy(region => $"{region.FieldName}|{region.HighlightType}|{region.Provenance?.SemanticRole ?? string.Empty}|{ReviewOverlayIdentity.SourceKey(region)}", StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First().SemanticGroupId, StringComparer.Ordinal);

    private static bool IsFailureCode(string code) =>
        code.Contains("canceled", StringComparison.Ordinal) ||
        code.Contains("stale", StringComparison.Ordinal) ||
        code.Contains("invalidation", StringComparison.Ordinal) ||
        code.Contains("fallback", StringComparison.Ordinal);

    private static bool IsFocusFallback(ReviewReplayStep step) =>
        step.Code is "focus-restored-semantic-group-primary" or "focus-restored-field-primary";

    private static bool IsFocusRestorationStep(ReviewReplayStep step) =>
        step.Code.StartsWith("focus-", StringComparison.Ordinal);

    private static int Count(IReadOnlyList<ReviewRuntimeDiagnostic> diagnostics, string code) =>
        diagnostics.Count(diagnostic => diagnostic.Code == code);
}
