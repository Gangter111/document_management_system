using DocumentManagement.Intelligence.Models;
using DocumentManagement.Intelligence.Results;

namespace DocumentManagement.Intelligence.Review;

public static class ReviewEvidenceResolver
{
    // Invariant: this resolver is the single semantic ordering authority for review evidence.
    // Diagnostics below explain the chosen order but must never feed back into the ordering keys.
    public static IReadOnlyList<BlockEvidence> OrderBlockEvidence(IReadOnlyList<BlockEvidence> evidence)
    {
        var pageBounds = BuildPageBounds(evidence);
        var repeated = BuildRepeatedHeaderFooterSet(evidence, pageBounds);
        return evidence
            .Select((item, index) => (Item: item, Index: index))
            .Select(item => (item.Item, item.Index, Page: ResolvePageBounds(pageBounds, item.Item.PageNumber - 1), Repeated: repeated))
            .OrderBy(item => RoleRank(item.Item.Role))
            .ThenBy(item => StampNoiseRank(item.Item.NormalizedText, item.Item.SemanticRole, item.Item.Role, item.Item.BoundingBox, item.Page.Width, item.Page.Height))
            .ThenBy(item => SemanticRoleRank(item.Item.SemanticRole, item.Item.NormalizedText))
            .ThenBy(item => Math.Max(0, item.Item.PageNumber - 1))
            .ThenBy(item => RepeatedHeaderFooterRank(item.Repeated, Math.Max(0, item.Item.PageNumber - 1), item.Item.BoundingBox, item.Item.BlockType, item.Item.NormalizedText, item.Item.SemanticRole, item.Page.Width, item.Page.Height))
            .ThenBy(item => ZoneRank(ClassifyZone(item.Item.BoundingBox, item.Item.BlockType, item.Item.NormalizedText, item.Item.SemanticRole, item.Item.Role, item.Page.Width, item.Page.Height)))
            .ThenBy(item => VisualLine(item.Item.BoundingBox))
            .ThenBy(item => VisualColumn(item.Item.BoundingBox))
            .ThenBy(item => item.Item.ReadingOrder)
            .ThenBy(item => EvidenceTextKey(item.Item.BlockType, item.Item.NormalizedText, item.Item.RawText))
            .ThenBy(item => item.Index)
            .Select(item => item.Item)
            .ToArray();
    }

    public static IReadOnlyList<ReviewEvidenceRegion> OrderReviewEvidence(IReadOnlyList<ReviewEvidenceRegion> evidence)
    {
        var pageBounds = BuildPageBounds(evidence);
        var repeated = BuildRepeatedHeaderFooterSet(evidence, pageBounds);
        return evidence
            .Select((item, index) => (Item: item, Index: index))
            .Select(item => (item.Item, item.Index, Page: ResolvePageBounds(pageBounds, item.Item.PageIndex), Repeated: repeated))
            .OrderBy(item => RoleRank(item.Item.Role))
            .ThenBy(item => StampNoiseRank(item.Item.SourceText, item.Item.Provenance?.SemanticRole ?? string.Empty, item.Item.Role, item.Item.BoundingBox, item.Page.Width, item.Page.Height))
            .ThenBy(item => SemanticRoleRank(item.Item.Provenance?.SemanticRole ?? string.Empty, item.Item.SourceText))
            .ThenBy(item => Math.Max(0, item.Item.PageIndex))
            .ThenBy(item => RepeatedHeaderFooterRank(item.Repeated, Math.Max(0, item.Item.PageIndex), item.Item.BoundingBox, item.Item.BlockType, item.Item.SourceText, item.Item.Provenance?.SemanticRole ?? string.Empty, item.Page.Width, item.Page.Height))
            .ThenBy(item => ZoneRank(ClassifyZone(item.Item.BoundingBox, item.Item.BlockType, item.Item.SourceText, item.Item.Provenance?.SemanticRole ?? string.Empty, item.Item.Role, item.Page.Width, item.Page.Height)))
            .ThenBy(item => VisualLine(item.Item.BoundingBox))
            .ThenBy(item => VisualColumn(item.Item.BoundingBox))
            .ThenBy(item => item.Item.ReadingOrder)
            .ThenBy(item => EvidenceTextKey(item.Item.BlockType, item.Item.SourceText, item.Item.Provenance?.RawText ?? string.Empty))
            .ThenBy(item => item.Item.SemanticGroupId, StringComparer.Ordinal)
            .ThenBy(item => item.Index)
            .Select(item => item.Item)
            .ToArray();
    }

    public static BlockEvidence? ResolvePrimary(IReadOnlyList<BlockEvidence> evidence) =>
        OrderBlockEvidence(evidence).FirstOrDefault();

    public static ReviewEvidenceRegion? ResolvePrimary(IReadOnlyList<ReviewEvidenceRegion> evidence, bool requireDrawable = false)
    {
        var ordered = OrderReviewEvidence(evidence);
        return requireDrawable
            ? ordered.FirstOrDefault(item => item.BoundingBox is not null)
            : ordered.FirstOrDefault();
    }

    public static bool IsCanonicalPrimary(ReviewEvidenceRegion evidence, ReviewEvidenceRegion? primary) =>
        primary is not null && ReferenceEquals(evidence, primary);

    public static IReadOnlyList<EvidenceDiagnostic> ExplainBlockEvidence(
        IReadOnlyList<BlockEvidence> evidence,
        BlockEvidence target)
    {
        var ordered = OrderBlockEvidence(evidence).ToArray();
        var diagnostics = ExplainOrderedBlockEvidence(ordered);
        var index = Array.IndexOf(ordered, target);
        return index >= 0 ? diagnostics[index] : Array.Empty<EvidenceDiagnostic>();
    }

    public static IReadOnlyList<IReadOnlyList<EvidenceDiagnostic>> ExplainOrderedBlockEvidence(
        IReadOnlyList<BlockEvidence> orderedEvidence)
    {
        var pageBounds = BuildPageBounds(orderedEvidence);
        var repeated = BuildRepeatedHeaderFooterSet(orderedEvidence, pageBounds);
        var primary = orderedEvidence.FirstOrDefault();
        return orderedEvidence
            .Select(item =>
            {
                var pageIndex = Math.Max(0, item.PageNumber - 1);
                var page = ResolvePageBounds(pageBounds, pageIndex);
                return BuildDiagnostics(
                    item.Role,
                    item.BoundingBox,
                    item.BlockType,
                    item.NormalizedText,
                    item.SemanticRole,
                    pageIndex,
                    page.Width,
                    page.Height,
                    repeated,
                    ReferenceEquals(item, primary));
            })
            .ToArray();
    }

    public static IReadOnlyList<EvidenceDiagnostic> ExplainReviewEvidence(
        IReadOnlyList<ReviewEvidenceRegion> evidence,
        ReviewEvidenceRegion target)
    {
        var pageBounds = BuildPageBounds(evidence);
        var repeated = BuildRepeatedHeaderFooterSet(evidence, pageBounds);
        var pageIndex = Math.Max(0, target.PageIndex);
        var page = ResolvePageBounds(pageBounds, pageIndex);
        var primary = ResolvePrimary(evidence);

        return BuildDiagnostics(
            target.Role,
            target.BoundingBox,
            target.BlockType,
            target.SourceText,
            target.Provenance?.SemanticRole ?? string.Empty,
            pageIndex,
            page.Width,
            page.Height,
            repeated,
            ReferenceEquals(target, primary));
    }

    private static int RoleRank(EvidenceRole role) =>
        role switch
        {
            EvidenceRole.Primary => 0,
            EvidenceRole.Supporting => 1,
            _ => 2
        };

    private static int StampNoiseRank(
        string text,
        string semanticRole,
        EvidenceRole role,
        BoundingBox? box,
        double pageWidth,
        double pageHeight) =>
        IsStampOrSignatureNoise(text, semanticRole, role, box, pageWidth, pageHeight) ? 1 : 0;

    private static int RepeatedHeaderFooterRank(
        IReadOnlyDictionary<string, int> repeatedFirstPages,
        int pageIndex,
        BoundingBox? box,
        string blockType,
        string text,
        string semanticRole,
        double pageWidth,
        double pageHeight)
    {
        var key = HeaderFooterKey(text, blockType);
        if (key.Length == 0 ||
            !repeatedFirstPages.TryGetValue(key, out var firstPageIndex) ||
            pageIndex <= firstPageIndex)
        {
            return 0;
        }

        return IsHeaderFooterCandidate(box, blockType, text, semanticRole, pageWidth, pageHeight) ? 1 : 0;
    }

    private static int SemanticRoleRank(string semanticRole, string text)
    {
        if (semanticRole.Equals("signer.name", StringComparison.OrdinalIgnoreCase))
        {
            return 0;
        }

        if (semanticRole.Equals("signer.role", StringComparison.OrdinalIgnoreCase))
        {
            return IsPersonLikeSignerRoleText(text) ? 1 : 3;
        }

        if (semanticRole.StartsWith("signer.", StringComparison.OrdinalIgnoreCase))
        {
            return 2;
        }

        return 0;
    }

    private static IReadOnlyList<EvidenceDiagnostic> BuildDiagnostics(
        EvidenceRole role,
        BoundingBox? box,
        string blockType,
        string text,
        string semanticRole,
        int pageIndex,
        double pageWidth,
        double pageHeight,
        IReadOnlyDictionary<string, int> repeatedFirstPages,
        bool isCanonicalPrimary)
    {
        var diagnostics = new List<EvidenceDiagnostic>(6)
        {
            RoleDiagnostic(role, isCanonicalPrimary)
        };

        diagnostics.Add(ZoneDiagnostic(ClassifyZone(box, blockType, text, semanticRole, role, pageWidth, pageHeight)));

        if (RepeatedHeaderFooterRank(repeatedFirstPages, pageIndex, box, blockType, text, semanticRole, pageWidth, pageHeight) > 0)
        {
            diagnostics.Add(new EvidenceDiagnostic("repeated-header-footer-demoted", "repeated header/footer demoted"));
        }

        if (semanticRole.StartsWith("body.", StringComparison.OrdinalIgnoreCase))
        {
            diagnostics.Add(new EvidenceDiagnostic("body-first-ordering", "body evidence preserves page reading order"));
        }

        if (semanticRole.StartsWith("signer.", StringComparison.OrdinalIgnoreCase) && pageIndex > 0)
        {
            diagnostics.Add(new EvidenceDiagnostic("later-page-signer-after-body", "later-page signer follows earlier page evidence"));
        }

        if (box is null)
        {
            diagnostics.Add(new EvidenceDiagnostic("missing-bbox-non-drawable", "missing bbox retained for provenance only"));
        }

        diagnostics.Add(new EvidenceDiagnostic("deterministic-tiebreak", "stable tie-breakers applied"));
        return diagnostics;
    }

    private static EvidenceDiagnostic RoleDiagnostic(EvidenceRole role, bool isCanonicalPrimary) =>
        role switch
        {
            EvidenceRole.Primary when isCanonicalPrimary => new EvidenceDiagnostic("primary-canonical", "canonical primary evidence"),
            EvidenceRole.Primary => new EvidenceDiagnostic("primary-ordered", "primary evidence ordered by resolver"),
            EvidenceRole.Supporting => new EvidenceDiagnostic("supporting-evidence", "supporting evidence"),
            _ => new EvidenceDiagnostic("contextual-evidence", "contextual evidence")
        };

    private static EvidenceDiagnostic ZoneDiagnostic(ReviewSemanticZone zone) =>
        zone switch
        {
            ReviewSemanticZone.TopLeftAdministrative => new EvidenceDiagnostic("zone-top-left-admin", "top-left issuer/document heuristic"),
            ReviewSemanticZone.TopRightAdministrative => new EvidenceDiagnostic("zone-top-right-admin", "top-right national/date heuristic"),
            ReviewSemanticZone.CenterTitle => new EvidenceDiagnostic("zone-centered-title", "centered title heuristic"),
            ReviewSemanticZone.Body => new EvidenceDiagnostic("zone-body", "body section heuristic"),
            ReviewSemanticZone.BottomRightSigner => new EvidenceDiagnostic("zone-bottom-right-signer", "bottom-right signer heuristic"),
            ReviewSemanticZone.BottomLeftRecipients => new EvidenceDiagnostic("zone-bottom-left-recipient", "bottom-left recipients/archive heuristic"),
            ReviewSemanticZone.StampOrSignatureNoise => new EvidenceDiagnostic("zone-stamp-noise", "stamp/signature noise demoted"),
            _ => new EvidenceDiagnostic("zone-unknown", "no drawable semantic zone")
        };

    private static int ZoneRank(ReviewSemanticZone zone) =>
        zone switch
        {
            ReviewSemanticZone.TopLeftAdministrative => 0,
            ReviewSemanticZone.TopRightAdministrative => 1,
            ReviewSemanticZone.CenterTitle => 2,
            ReviewSemanticZone.Body => 3,
            ReviewSemanticZone.BottomRightSigner => 4,
            ReviewSemanticZone.BottomLeftRecipients => 5,
            ReviewSemanticZone.StampOrSignatureNoise => 6,
            _ => 7
        };

    private static ReviewSemanticZone ClassifyZone(
        BoundingBox? box,
        string blockType,
        string text,
        string semanticRole,
        EvidenceRole role,
        double pageWidth,
        double pageHeight)
    {
        if (semanticRole.StartsWith("signer.", StringComparison.OrdinalIgnoreCase))
        {
            return ReviewSemanticZone.BottomRightSigner;
        }

        if (semanticRole.StartsWith("date.", StringComparison.OrdinalIgnoreCase) ||
            semanticRole.Contains("issue-date", StringComparison.OrdinalIgnoreCase))
        {
            return ReviewSemanticZone.TopRightAdministrative;
        }

        if (semanticRole.StartsWith("issuer.", StringComparison.OrdinalIgnoreCase) ||
            semanticRole.StartsWith("document.", StringComparison.OrdinalIgnoreCase))
        {
            return ReviewSemanticZone.TopLeftAdministrative;
        }

        if (semanticRole.StartsWith("body.", StringComparison.OrdinalIgnoreCase))
        {
            return ReviewSemanticZone.Body;
        }

        if (semanticRole.StartsWith("recipient.", StringComparison.OrdinalIgnoreCase) ||
            semanticRole.StartsWith("archive.", StringComparison.OrdinalIgnoreCase))
        {
            return ReviewSemanticZone.BottomLeftRecipients;
        }

        if (box is null || pageWidth <= 0 || pageHeight <= 0)
        {
            return IsStampOrSignatureNoise(text, semanticRole, role, box, pageWidth, pageHeight)
                ? ReviewSemanticZone.StampOrSignatureNoise
                : ReviewSemanticZone.Unknown;
        }

        var centerX = box.CenterX / pageWidth;
        var centerY = box.CenterY / pageHeight;
        var upper = centerY <= 0.22;
        var lower = centerY >= 0.72;

        var coordinateZone = ReviewSemanticZone.Body;

        if (upper && centerX <= 0.45)
        {
            coordinateZone = ReviewSemanticZone.TopLeftAdministrative;
        }
        else if (upper && centerX >= 0.45)
        {
            coordinateZone = ReviewSemanticZone.TopRightAdministrative;
        }
        else if ((blockType.Equals("title", StringComparison.OrdinalIgnoreCase) || centerX is >= 0.35 and <= 0.65) &&
            centerY is > 0.18 and <= 0.42)
        {
            coordinateZone = ReviewSemanticZone.CenterTitle;
        }
        else if (lower && centerX >= 0.52)
        {
            coordinateZone = ReviewSemanticZone.BottomRightSigner;
        }
        else if (lower && centerX < 0.52)
        {
            coordinateZone = ReviewSemanticZone.BottomLeftRecipients;
        }

        return IsStampOrSignatureNoise(text, semanticRole, role, box, pageWidth, pageHeight)
            ? ReviewSemanticZone.StampOrSignatureNoise
            : coordinateZone;
    }

    private static bool IsStampOrSignatureNoise(
        string text,
        string semanticRole,
        EvidenceRole role,
        BoundingBox? box,
        double pageWidth,
        double pageHeight)
    {
        if (semanticRole.StartsWith("signer.", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (semanticRole.StartsWith("issuer.", StringComparison.OrdinalIgnoreCase) ||
            semanticRole.StartsWith("document.", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var compact = new string(text.Where(char.IsLetterOrDigit).Select(char.ToUpperInvariant).ToArray());
        if (compact.Length == 0)
        {
            return false;
        }

        var obviousStampArtifact = compact.Contains("CONGTYCON", StringComparison.Ordinal) ||
            compact.Contains("CONGTYCONNS", StringComparison.Ordinal) ||
            compact.Contains("0190270", StringComparison.Ordinal);
        var shortStampFragment = compact is "CONGTY" or "COPHAN" or "NONGSAN" or "PHUGIA";
        if (!obviousStampArtifact && !shortStampFragment)
        {
            return false;
        }

        if (box is null || pageWidth <= 0 || pageHeight <= 0)
        {
            return obviousStampArtifact || role != EvidenceRole.Primary;
        }

        var centerX = box.CenterX / pageWidth;
        var centerY = box.CenterY / pageHeight;
        var inStampArea = centerY >= 0.58 && centerX >= 0.45;
        var inTopLeftHeader = centerY <= 0.25 && centerX <= 0.45;

        return !inTopLeftHeader && inStampArea;
    }

    private static IReadOnlyDictionary<string, int> BuildRepeatedHeaderFooterSet(
        IReadOnlyList<BlockEvidence> evidence,
        IReadOnlyDictionary<int, (double Width, double Height)> pageBounds)
    {
        // Repeated header/footer suppression is scoped to this resolver invocation.
        // Matching evidence is demoted by rank and diagnostic; it is not removed.
        var candidates = evidence
            .Select(item =>
            {
                var pageIndex = Math.Max(0, item.PageNumber - 1);
                var page = ResolvePageBounds(pageBounds, pageIndex);
                return new
                {
                    Key = HeaderFooterKey(item.NormalizedText, item.BlockType),
                    PageIndex = pageIndex,
                    IsCandidate = IsHeaderFooterCandidate(item.BoundingBox, item.BlockType, item.NormalizedText, item.SemanticRole, page.Width, page.Height)
                };
            })
            .Where(item => item.IsCandidate && item.Key.Length > 0)
            .ToArray();

        return candidates
            .GroupBy(item => item.Key, StringComparer.Ordinal)
            .Where(group => group.Select(item => item.PageIndex).Distinct().Count() > 1)
            .ToDictionary(group => group.Key, group => group.Min(item => item.PageIndex), StringComparer.Ordinal);
    }

    private static IReadOnlyDictionary<string, int> BuildRepeatedHeaderFooterSet(
        IReadOnlyList<ReviewEvidenceRegion> evidence,
        IReadOnlyDictionary<int, (double Width, double Height)> pageBounds)
    {
        // Repeated header/footer suppression is scoped to this resolver invocation.
        // Matching evidence is demoted by rank and diagnostic; it is not removed.
        var candidates = evidence
            .Select(item =>
            {
                var pageIndex = Math.Max(0, item.PageIndex);
                var page = ResolvePageBounds(pageBounds, pageIndex);
                return new
                {
                    Key = HeaderFooterKey(item.SourceText, item.BlockType),
                    PageIndex = pageIndex,
                    IsCandidate = IsHeaderFooterCandidate(item.BoundingBox, item.BlockType, item.SourceText, item.Provenance?.SemanticRole ?? string.Empty, page.Width, page.Height)
                };
            })
            .Where(item => item.IsCandidate && item.Key.Length > 0)
            .ToArray();

        return candidates
            .GroupBy(item => item.Key, StringComparer.Ordinal)
            .Where(group => group.Select(item => item.PageIndex).Distinct().Count() > 1)
            .ToDictionary(group => group.Key, group => group.Min(item => item.PageIndex), StringComparer.Ordinal);
    }

    private static bool IsHeaderFooterCandidate(
        BoundingBox? box,
        string blockType,
        string text,
        string semanticRole,
        double pageWidth,
        double pageHeight)
    {
        if (box is null || pageWidth <= 0 || pageHeight <= 0)
        {
            return false;
        }

        var centerY = box.CenterY / pageHeight;
        var isHeaderFooterBlock = blockType.Contains("header", StringComparison.OrdinalIgnoreCase) ||
            blockType.Contains("footer", StringComparison.OrdinalIgnoreCase);
        var isAdministrativeRole = semanticRole.StartsWith("issuer.", StringComparison.OrdinalIgnoreCase) ||
            semanticRole.StartsWith("document.", StringComparison.OrdinalIgnoreCase) ||
            semanticRole.StartsWith("date.", StringComparison.OrdinalIgnoreCase) ||
            semanticRole.StartsWith("recipient.", StringComparison.OrdinalIgnoreCase) ||
            semanticRole.StartsWith("archive.", StringComparison.OrdinalIgnoreCase) ||
            semanticRole.Contains("issue-date", StringComparison.OrdinalIgnoreCase);

        if (!isHeaderFooterBlock && !isAdministrativeRole)
        {
            return false;
        }

        return centerY <= 0.22 || centerY >= 0.78;
    }

    private static string HeaderFooterKey(string text, string blockType)
    {
        var tokens = new List<string>();
        var token = new List<char>();
        foreach (var character in text)
        {
            if (char.IsLetterOrDigit(character))
            {
                token.Add(char.ToUpperInvariant(character));
                continue;
            }

            FlushToken(tokens, token);
        }

        FlushToken(tokens, token);
        TrimPageNumberSuffix(tokens, blockType);

        var compact = string.Concat(tokens);
        return compact.Length < 4 ? string.Empty : compact;
    }

    private static void FlushToken(ICollection<string> tokens, ICollection<char> token)
    {
        if (token.Count == 0)
        {
            return;
        }

        tokens.Add(new string(token.ToArray()));
        token.Clear();
    }

    private static void TrimPageNumberSuffix(IList<string> tokens, string blockType)
    {
        var isFooter = blockType.Contains("footer", StringComparison.OrdinalIgnoreCase);
        while (tokens.Count > 1 && tokens[^1].All(char.IsDigit))
        {
            if (isFooter)
            {
                tokens.RemoveAt(tokens.Count - 1);
                continue;
            }

            var previous = tokens[^2];
            if (previous.Equals("PAGE", StringComparison.Ordinal) ||
                previous.Equals("TRANG", StringComparison.Ordinal))
            {
                tokens.RemoveAt(tokens.Count - 1);
                tokens.RemoveAt(tokens.Count - 1);
                continue;
            }

            break;
        }
    }

    private static bool IsPersonLikeSignerRoleText(string text)
    {
        var compact = new string(text.Where(character => !char.IsWhiteSpace(character)).ToArray());
        return compact.Contains("CHUTICH", StringComparison.OrdinalIgnoreCase) ||
            compact.Contains("GIAMDOC", StringComparison.OrdinalIgnoreCase) ||
            compact.Contains("KT.", StringComparison.OrdinalIgnoreCase) ||
            compact.Contains("TM.", StringComparison.OrdinalIgnoreCase);
    }

    private static IReadOnlyDictionary<int, (double Width, double Height)> BuildPageBounds(IReadOnlyList<BlockEvidence> evidence) =>
        evidence
            .GroupBy(item => Math.Max(0, item.PageNumber - 1))
            .ToDictionary(
                group => group.Key,
                group => (Width: group.Max(item => item.BoundingBox.X2), Height: group.Max(item => item.BoundingBox.Y2)));

    private static IReadOnlyDictionary<int, (double Width, double Height)> BuildPageBounds(IReadOnlyList<ReviewEvidenceRegion> evidence) =>
        evidence
            .Where(item => item.BoundingBox is not null)
            .GroupBy(item => Math.Max(0, item.PageIndex))
            .ToDictionary(
                group => group.Key,
                group =>
                {
                    var boxes = group.Select(item => item.BoundingBox!).ToArray();
                    return (Width: boxes.Max(box => box.X2), Height: boxes.Max(box => box.Y2));
                });

    private static (double Width, double Height) ResolvePageBounds(
        IReadOnlyDictionary<int, (double Width, double Height)> pageBounds,
        int pageIndex) =>
        pageBounds.TryGetValue(Math.Max(0, pageIndex), out var bounds) ? bounds : (0, 0);

    private static double VisualLine(BoundingBox? box) =>
        box is null ? double.MaxValue : Math.Round(box.Y1, 1, MidpointRounding.AwayFromZero);

    private static double VisualColumn(BoundingBox? box) =>
        box is null ? double.MaxValue : Math.Round(box.X1, 1, MidpointRounding.AwayFromZero);

    private static string EvidenceTextKey(string blockType, string normalizedText, string rawText) =>
        string.Join("|", blockType, normalizedText, rawText).Trim();

    private enum ReviewSemanticZone
    {
        TopLeftAdministrative,
        TopRightAdministrative,
        CenterTitle,
        Body,
        BottomRightSigner,
        BottomLeftRecipients,
        StampOrSignatureNoise,
        Unknown
    }
}
