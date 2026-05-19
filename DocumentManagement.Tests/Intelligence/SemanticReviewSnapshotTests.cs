using System.Diagnostics;
using DocumentManagement.Intelligence.Adapters;
using DocumentManagement.Intelligence.Models;
using DocumentManagement.Intelligence.Normalization;
using DocumentManagement.Intelligence.Review;
using DocumentManagement.Intelligence.Results;
using Xunit;

namespace DocumentManagement.Tests.Intelligence;

public sealed class SemanticReviewSnapshotTests
{
    private static readonly VietnameseTextNormalizer Normalizer = new();

    [Fact]
    public void SampleDecision_ReviewerVisibleSemanticsAreStable()
    {
        var review = LoadSampleReview();
        var signerCandidate = review.AcceptedCandidates.Single(candidate => candidate.FieldName == "Signer");

        AssertFieldEvidence(
            review,
            "Issuer",
            ReviewStatus.Accepted,
            "CONG TY CO PHAN NONG SAN PHU GIA",
            "Primary|issuer.name|CONG TY CO PHAN NONG SAN PHU GIA SO: 3AS./QD-PG|124,59,342,135");
        AssertFieldEvidence(
            review,
            "Document number",
            ReviewStatus.Accepted,
            "3AS./QD-PG",
            "Primary|document.number|CONG TY CO PHAN NONG SAN PHU GIA SO: 3AS./QD-PG|124,59,342,135");
        AssertFieldEvidence(
            review,
            "Issue date",
            ReviewStatus.Rejected,
            string.Empty);
        AssertFieldEvidence(
            review,
            "Signer",
            ReviewStatus.Accepted,
            "PHAM CHANH HA",
            "Primary|signer.name|PHAM CHANH HA|682,690,895,722",
            "Supporting|signer.role|CHU TICH HDQT|707,667,862,692");

        Assert.Equal(new[] { "Dieu 1", "Dieu 2", "Dieu 3", "Dieu 4" }, BodyFieldNames(review));
        AssertRejectedCandidate(review, "01/10/1979", "date.rejected-dob-context", "contains DOB context", "112,326,899,376");
        AssertRejectedCandidate(review, "LLF/9/2025", "document.issue-date", "date token contains uncertain OCR; no silent recovery", "504,108,895,131");
        AssertDiagnostics(
            review.Fields.Single(field => field.Name == "Signer").EvidenceRegions.Single(evidence => evidence.Provenance?.SemanticRole == "signer.name"),
            "primary-canonical",
            "zone-bottom-right-signer",
            "deterministic-tiebreak");
        AssertReasoningOrder(signerCandidate);
        Assert.Equal(
            new[] { "Semantic match", "Layout", "OCR degradation penalty", "Ambiguity penalty" },
            signerCandidate.ConfidenceBreakdown!.Select(item => item.Name).ToArray());
        AssertOverlayFocus(review, "Signer", "Primary|signer.name|682,690,895,722");
        AssertMissingBboxProvenanceIsVisible();
    }

    [Fact]
    public void SampleDecision_SemanticProjectionOrderingIsStable()
    {
        var ordered = OrderedSampleEvidence();

        Assert.Equal("issuer.name", ordered[0].SemanticRole);
        Assert.Contains("CONG TY CO PHAN NONG SAN PHU GIA", ToSearch(ordered[0].NormalizedText), StringComparison.Ordinal);
        AssertRoleAppearsBefore(ordered, "date.issue-header", "title.decision");
        AssertRoleAppearsBefore(ordered, "recipient.archive", "stamp.noise");
        Assert.Equal(new[] { 1, 2, 3, 4 }, ordered
            .Where(evidence => evidence.SemanticRole == "body.section")
            .Select(evidence => ParseDieuNumber(evidence.NormalizedText))
            .ToArray());

        var signer = ordered.Single(evidence => evidence.SemanticRole == "signer.name");
        Assert.Equal("682,690,895,722", Format(signer.BoundingBox));
        Assert.Equal("stamp.noise", ordered[^1].SemanticRole);
    }

    [Fact]
    public void ReviewProjectionDeterminism_IsStableAcrossRepeatedRuns()
    {
        var first = LoadSampleReview();
        var second = LoadSampleReview();

        Assert.Equal(ReviewSignature(first), ReviewSignature(second));
        Assert.Equal(ReasoningSignature(first, "Signer"), ReasoningSignature(second, "Signer"));
        Assert.Equal(OverlayFocusSignature(first, "Signer"), OverlayFocusSignature(second, "Signer"));
        Assert.Equal(DiagnosticSignature(first), DiagnosticSignature(second));
        var firstEvidence = OrderedSampleEvidence().Select(EvidenceSignature).ToArray();
        var secondEvidence = OrderedSampleEvidence().Select(EvidenceSignature).ToArray();

        Assert.Equal(firstEvidence, secondEvidence);
    }

    [Fact]
    public void NegativeSnapshot_StampLikeSignerRoleDoesNotBeatSignerName()
    {
        var stampLikeSigner = new ReviewEvidenceRegion(
            0,
            new BoundingBox(600, 600, 680, 620),
            "CONGTY",
            "paragraph",
            1,
            true,
            EvidenceRole.Primary,
            new EvidenceProvenance("test", string.Empty, "CONGTY", "CONGTY", "signer.role"));
        var signerName = new ReviewEvidenceRegion(
            0,
            new BoundingBox(682, 690, 895, 722),
            "Pham Chanh Ha",
            "paragraph",
            2,
            true,
            EvidenceRole.Primary,
            new EvidenceProvenance("test", string.Empty, "Pham Chanh Ha", "Pham Chanh Ha", "signer.name"));

        var ordered = ReviewEvidenceResolver.OrderReviewEvidence(new[] { stampLikeSigner, signerName });

        Assert.Same(signerName, ordered[0]);
    }

    [Fact]
    public void MultiPageEvidenceOrdering_IsDeterministicWhenPageIndexesDiffer()
    {
        var pageTwo = CreateReviewEvidence(1, 120, 100, "Dieu 2. Page two", "body.section");
        var pageOneLater = CreateReviewEvidence(0, 120, 300, "Dieu 1. Later page one block", "body.section");
        var pageOneEarlier = CreateReviewEvidence(0, 120, 200, "Dieu 1. Earlier page one block", "body.section");

        var ordered = ReviewEvidenceResolver.OrderReviewEvidence(new[] { pageTwo, pageOneLater, pageOneEarlier });

        Assert.Equal(new[] { pageOneEarlier, pageOneLater, pageTwo }, ordered);
    }

    [Fact]
    public void MultiPageEvidenceOrdering_KeepsEarlierBodyBeforeLaterSigner()
    {
        var laterSigner = CreateReviewEvidence(1, 680, 1_180, "Pham Chanh Ha", "signer.name");
        var pageOneBody = CreateReviewEvidence(0, 120, 700, "Dieu 1. Page one body", "body.section");
        var pageTwoBody = CreateReviewEvidence(1, 120, 220, "Dieu 2. Page two body", "body.section");

        var ordered = ReviewEvidenceResolver.OrderReviewEvidence(new[] { laterSigner, pageTwoBody, pageOneBody });

        Assert.Equal(new[] { pageOneBody, pageTwoBody, laterSigner }, ordered);
        Assert.Contains("later-page-signer-after-body", DiagnosticCodes(new[] { laterSigner, pageTwoBody, pageOneBody }, laterSigner));
        Assert.Contains("body-first-ordering", DiagnosticCodes(new[] { laterSigner, pageTwoBody, pageOneBody }, pageOneBody));
    }

    [Fact]
    public void RepeatedAdministrativeHeader_IsDemotedAfterFirstPage()
    {
        var firstPageHeader = CreateReviewEvidence(0, 80, 40, "UBND TINH THANH HOA", "issuer.name", "page_header");
        var secondPageHeader = CreateReviewEvidence(1, 80, 40, "UBND TINH THANH HOA", "issuer.name", "page_header");
        var secondPageBody = CreateReviewEvidence(1, 120, 240, "Dieu 2. Noi dung tiep theo", "body.section");

        var ordered = ReviewEvidenceResolver.OrderReviewEvidence(new[] { secondPageHeader, secondPageBody, firstPageHeader });

        Assert.Equal(new[] { firstPageHeader, secondPageBody, secondPageHeader }, ordered);
        Assert.Equal("UBND TINH THANH HOA", ordered[^1].SourceText);
        Assert.Contains("repeated-header-footer-demoted", DiagnosticCodes(new[] { secondPageHeader, secondPageBody, firstPageHeader }, secondPageHeader));
    }

    [Fact]
    public void RepeatedAdministrativeFooter_IsDemotedAfterFirstPage()
    {
        var firstPageFooter = CreateReviewEvidence(0, 90, 1_260, "Luu VT", "archive.reference", "page_footer");
        var secondPageFooter = CreateReviewEvidence(1, 90, 1_260, "Luu VT", "archive.reference", "page_footer");
        var secondPageBody = CreateReviewEvidence(1, 120, 240, "Dieu 2. Noi dung tiep theo", "body.section");

        var ordered = ReviewEvidenceResolver.OrderReviewEvidence(new[] { secondPageFooter, secondPageBody, firstPageFooter });

        Assert.Equal(new[] { firstPageFooter, secondPageBody, secondPageFooter }, ordered);
        Assert.Equal("Luu VT", ordered[^1].SourceText);
        Assert.Contains("repeated-header-footer-demoted", DiagnosticCodes(new[] { secondPageFooter, secondPageBody, firstPageFooter }, secondPageFooter));
    }

    [Fact]
    public void RepeatedFooterMatching_NormalizesWhitespacePunctuationAndPageSuffix()
    {
        var firstPageFooter = CreateReviewEvidence(0, 90, 1_260, "Noi nhan:\r\nLuu VT. 1", "archive.reference", "page_footer");
        var secondPageFooter = CreateReviewEvidence(1, 90, 1_260, "Noi nhan: Luu VT 2", "archive.reference", "page_footer");
        var secondPageBody = CreateReviewEvidence(1, 120, 240, "Dieu 2. Noi dung tiep theo", "body.section");

        var ordered = ReviewEvidenceResolver.OrderReviewEvidence(new[] { secondPageFooter, secondPageBody, firstPageFooter });

        Assert.Equal(new[] { firstPageFooter, secondPageBody, secondPageFooter }, ordered);
    }

    [Fact]
    public void MultiPageOverlayFocus_RemainsOnCanonicalPrimaryEvidence()
    {
        var firstPageHeader = CreateReviewEvidence(0, 80, 40, "UBND TINH THANH HOA", "issuer.name", "page_header");
        var secondPageHeader = CreateReviewEvidence(1, 80, 40, "UBND TINH THANH HOA", "issuer.name", "page_header");
        var secondPageBody = CreateReviewEvidence(1, 120, 240, "Dieu 2. Noi dung tiep theo", "body.section");
        var field = new ReviewField(
            "Issuer",
            "UBND TINH THANH HOA",
            0.9,
            ReviewStatus.Accepted,
            Array.Empty<string>(),
            firstPageHeader.SourceText,
            firstPageHeader.BlockType,
            firstPageHeader.BoundingBox,
            firstPageHeader.PageIndex,
            new[] { secondPageHeader, firstPageHeader },
            Array.Empty<ReviewCandidate>());
        var bodyField = new ReviewField(
            "Dieu 2",
            secondPageBody.SourceText,
            0.9,
            ReviewStatus.Accepted,
            Array.Empty<string>(),
            secondPageBody.SourceText,
            secondPageBody.BlockType,
            secondPageBody.BoundingBox,
            secondPageBody.PageIndex,
            new[] { secondPageBody },
            Array.Empty<ReviewCandidate>());

        var firstPages = ReviewOverlayMapper.CreatePages(new[] { field, bodyField }, Array.Empty<ReviewCandidate>(), MultiPageMetrics());
        var secondPages = ReviewOverlayMapper.CreatePages(new[] { field, bodyField }, Array.Empty<ReviewCandidate>(), MultiPageMetrics());
        var firstOverlay = firstPages
            .SelectMany(page => page.Regions)
            .Single(region => region.FieldName == "Issuer" && region.IsPrimary);
        var secondOverlay = secondPages
            .SelectMany(page => page.Regions)
            .Single(region => region.FieldName == "Issuer" && region.IsPrimary);

        Assert.Equal(0, firstOverlay.PageIndex);
        Assert.Equal(firstPageHeader.BoundingBox, firstOverlay.BoundingBox);
        Assert.Equal(firstOverlay.Id, secondOverlay.Id);
        Assert.Equal(firstOverlay.SemanticGroupId, secondOverlay.SemanticGroupId);
        Assert.Equal(new[] { "Dieu 2. Noi dung tiep theo", "UBND TINH THANH HOA" }, firstPages[1].Regions.Select(region => region.SourceText).ToArray());
    }

    [Fact]
    public void OverlayExplainability_PropagatesEvidenceDiagnostics()
    {
        var review = LoadSampleReview();

        var signerOverlay = review.PageOverlays
            .SelectMany(page => page.Regions)
            .Single(region => region.FieldName == "Signer" && region.IsPrimary);

        AssertDiagnostics(signerOverlay.Provenance!, "primary-canonical", "zone-bottom-right-signer", "deterministic-tiebreak");
    }

    [Fact]
    public void OverlayTraversal_IsStableWhenCandidateInsertionOrderDiffers()
    {
        var first = CreateCandidate("Issue date", "bad-a", "01/10/1979", new BoundingBox(120, 200, 240, 230));
        var second = CreateCandidate("Issue date", "bad-b", "01/10/1979", new BoundingBox(120, 200, 240, 230));

        var firstOrder = ReviewOverlayMapper.CreatePages(Array.Empty<ReviewField>(), new[] { first, second }, MultiPageMetrics());
        var secondOrder = ReviewOverlayMapper.CreatePages(Array.Empty<ReviewField>(), new[] { second, first }, MultiPageMetrics());

        Assert.Equal(
            firstOrder.SelectMany(page => page.Regions).Select(region => region.Id).ToArray(),
            secondOrder.SelectMany(page => page.Regions).Select(region => region.Id).ToArray());
    }

    [Fact]
    public void RepeatedRefreshFocusRestoration_PreservesResolverFocus()
    {
        var first = LoadSampleReview();
        var signerFocus = first.PageOverlays
            .SelectMany(page => page.Regions)
            .Single(region => region.FieldName == "Signer" && region.IsPrimary);
        var firstNavigator = new ReviewOverlayNavigator(first.PageOverlays);

        Assert.True(firstNavigator.NavigateToRegion(signerFocus.Id));
        var captured = firstNavigator.CaptureFocus();
        var second = LoadSampleReview();
        var secondNavigator = new ReviewOverlayNavigator(second.PageOverlays);

        Assert.True(secondNavigator.RestoreFocus(captured));

        var restored = second.PageOverlays.SelectMany(page => page.Regions).Single(region => region.Id == secondNavigator.ActiveRegionId);
        Assert.Equal("Signer", restored.FieldName);
        Assert.True(restored.IsPrimary);
        Assert.Equal("signer.name", restored.Provenance?.SemanticRole);
        Assert.Equal(captured!.EvidenceKey, ReviewOverlayIdentity.EvidenceKey(restored));
    }

    [Fact]
    public void KeyboardTraversal_RemainsResolverOrderedAcrossRegionsGroupsPagesAndContext()
    {
        var field = new ReviewField(
            "Dieu 1",
            "Dieu 1. Noi dung",
            0.8,
            ReviewStatus.Accepted,
            Array.Empty<string>(),
            "Dieu 1. Noi dung",
            "paragraph",
            new BoundingBox(120, 220, 620, 250),
            0,
            new[]
            {
                WithRole(CreateReviewEvidence(1, 120, 240, "Khoan 2. Context page two", "body.section-paragraph"), EvidenceRole.Contextual),
                CreateReviewEvidence(0, 120, 220, "Dieu 1. Noi dung", "body.section"),
                WithRole(CreateReviewEvidence(0, 140, 260, "Khoan 1. Context page one", "body.section-paragraph"), EvidenceRole.Contextual),
            },
            Array.Empty<ReviewCandidate>());
        var signerField = CreateReviewField("Signer", "PHAM CHANH HA", new[]
        {
            CreateReviewEvidence(1, 682, 690, "PHAM CHANH HA", "signer.name")
        });
        var pages = ReviewOverlayMapper.CreatePages(new[] { field, signerField }, Array.Empty<ReviewCandidate>(), MultiPageMetrics());
        var navigator = new ReviewOverlayNavigator(pages);

        Assert.True(navigator.NavigateNextRegion());
        Assert.Equal("Dieu 1. Noi dung", ActiveRegion(pages, navigator).SourceText);
        Assert.True(navigator.NavigateNextRegion());
        Assert.Equal("Khoan 1. Context page one", ActiveRegion(pages, navigator).SourceText);
        Assert.True(navigator.NavigateNextRegion());
        Assert.Equal("PHAM CHANH HA", ActiveRegion(pages, navigator).SourceText);
        Assert.True(navigator.NavigatePreviousRegion());
        Assert.Equal("Khoan 1. Context page one", ActiveRegion(pages, navigator).SourceText);
        Assert.True(navigator.NavigatePreviousRegion());
        Assert.Equal("Dieu 1. Noi dung", ActiveRegion(pages, navigator).SourceText);
        Assert.True(navigator.NavigateNextSemanticGroup());
        Assert.Equal("PHAM CHANH HA", ActiveRegion(pages, navigator).SourceText);
        Assert.True(navigator.NavigatePreviousSemanticGroup());
        Assert.Equal("Dieu 1. Noi dung", ActiveRegion(pages, navigator).SourceText);
        Assert.True(navigator.NavigateNextContextualEvidence());
        Assert.Equal("Khoan 1. Context page one", ActiveRegion(pages, navigator).SourceText);
        Assert.True(navigator.NavigateNextContextualEvidence());
        Assert.Equal("Khoan 2. Context page two", ActiveRegion(pages, navigator).SourceText);
        Assert.True(navigator.NavigateToPage(0));
        Assert.Null(navigator.ActiveRegionId);
    }

    [Fact]
    public void RepeatedRunOverlayContinuity_SurvivesContextualEvidenceInsertion()
    {
        var baseField = CreateReviewField("Issuer", "UBND TINH THANH HOA", new[]
        {
            CreateReviewEvidence(0, 80, 40, "UBND TINH THANH HOA", "issuer.name")
        });
        var expandedField = CreateReviewField("Issuer", "UBND TINH THANH HOA", new[]
        {
            WithRole(CreateReviewEvidence(0, 560, 40, "CONG HOA XA HOI CHU NGHIA VIET NAM", "issuer.context"), EvidenceRole.Contextual),
            CreateReviewEvidence(0, 80, 40, "UBND TINH THANH HOA", "issuer.name")
        });
        var firstPages = ReviewOverlayMapper.CreatePages(new[] { baseField }, Array.Empty<ReviewCandidate>(), MultiPageMetrics());
        var secondPages = ReviewOverlayMapper.CreatePages(new[] { expandedField }, Array.Empty<ReviewCandidate>(), MultiPageMetrics());
        var firstRegion = firstPages.SelectMany(page => page.Regions).Single(region => region.IsPrimary);
        var navigator = new ReviewOverlayNavigator(secondPages);

        Assert.True(navigator.RestoreFocus(ReviewOverlayFocusState.FromRegion(firstRegion)));

        var restored = ActiveRegion(secondPages, navigator);
        Assert.Equal(firstRegion.SemanticGroupId, restored.SemanticGroupId);
        Assert.Equal(ReviewOverlayIdentity.EvidenceKey(firstRegion), ReviewOverlayIdentity.EvidenceKey(restored));
        Assert.Equal("UBND TINH THANH HOA", restored.SourceText);
    }

    [Fact]
    public void ProvenanceDiffDiagnostics_AreLightweightDeterministicAndResolverDerived()
    {
        var groupId = "field:stable";
        var previousPrimary = CreateOverlayRegion("old-primary", groupId, "Signer", "Pham A", EvidenceRole.Primary, "signer.name", 0, 680, 690);
        var previousDemoted = CreateOverlayRegion("old-demoted", groupId, "Signer", "Role", EvidenceRole.Primary, "signer.role", 0, 700, 660);
        var currentPrimary = CreateOverlayRegion("new-primary", groupId, "Signer", "Pham B", EvidenceRole.Primary, "signer.name", 0, 680, 720);
        var currentDemoted = CreateOverlayRegion("new-demoted", groupId, "Signer", "Role", EvidenceRole.Supporting, "signer.role", 0, 700, 660);
        var previousPages = new[] { new ReviewPageOverlay(0, 1000, 1400, new[] { previousPrimary, previousDemoted }) };
        var currentPages = new[] { new ReviewPageOverlay(0, 1000, 1400, new[] { currentPrimary, currentDemoted }) };

        var first = ReviewProvenanceDiff.Compare(
            previousPages,
            currentPages,
            ReviewOverlayFocusState.FromRegion(previousPrimary),
            ReviewOverlayFocusState.FromRegion(currentPrimary));
        var second = ReviewProvenanceDiff.Compare(
            previousPages,
            currentPages,
            ReviewOverlayFocusState.FromRegion(previousPrimary),
            ReviewOverlayFocusState.FromRegion(currentPrimary));

        Assert.Equal(first.Select(item => item.Code).ToArray(), second.Select(item => item.Code).ToArray());
        Assert.Contains(first, item => item.Code == "provenance-diff-primary-changed");
        Assert.Contains(first, item => item.Code == "provenance-diff-ordering-changed");
        Assert.Contains(first, item => item.Code == "provenance-diff-evidence-demoted");
        Assert.Contains(first, item => item.Code == "provenance-diff-focus-moved");
    }

    [Fact]
    public void DeterministicSessionContinuity_PreventsHeaderAndSignerFocusJumps()
    {
        var firstPageHeader = CreateReviewEvidence(0, 80, 40, "UBND TINH THANH HOA", "issuer.name", "page_header");
        var secondPageHeader = CreateReviewEvidence(1, 80, 40, "UBND TINH THANH HOA", "issuer.name", "page_header");
        var signerRole = WithRole(CreateReviewEvidence(1, 707, 667, "CHU TICH HDQT", "signer.role"), EvidenceRole.Supporting);
        var signerName = CreateReviewEvidence(1, 682, 690, "PHAM CHANH HA", "signer.name");
        var issuerField = CreateReviewField("Issuer", "UBND TINH THANH HOA", new[] { secondPageHeader, firstPageHeader });
        var signerField = CreateReviewField("Signer", "PHAM CHANH HA", new[] { signerRole, signerName });
        var firstPages = ReviewOverlayMapper.CreatePages(new[] { issuerField, signerField }, Array.Empty<ReviewCandidate>(), MultiPageMetrics());
        var secondPages = ReviewOverlayMapper.CreatePages(new[] { signerField, issuerField }, Array.Empty<ReviewCandidate>(), MultiPageMetrics());
        var signerFocus = firstPages.SelectMany(page => page.Regions).Single(region => region.FieldName == "Signer" && region.IsPrimary);
        var navigator = new ReviewOverlayNavigator(secondPages);

        Assert.True(navigator.RestoreFocus(ReviewOverlayFocusState.FromRegion(signerFocus)));

        var restored = ActiveRegion(secondPages, navigator);
        Assert.Equal("Signer", restored.FieldName);
        Assert.Equal("signer.name", restored.Provenance?.SemanticRole);
        Assert.Equal("PHAM CHANH HA", restored.SourceText);
        Assert.Equal(1, restored.PageIndex);
    }

    [Fact]
    public void SemanticAuditReplayMetadata_IsDeterministicAndObservational()
    {
        var review = LoadSampleReview();
        var signer = review.PageOverlays.SelectMany(page => page.Regions).Single(region => region.FieldName == "Signer" && region.IsPrimary);
        var firstNavigator = new ReviewOverlayNavigator(review.PageOverlays);
        var secondNavigator = new ReviewOverlayNavigator(review.PageOverlays);

        var first = firstNavigator.NavigateToRegionWithReplay(signer.Id);
        var second = secondNavigator.NavigateToRegionWithReplay(signer.Id);

        Assert.True(first.Succeeded);
        Assert.Equal(ReplaySignature(first), ReplaySignature(second));
        Assert.Equal("selection-region-id", Assert.Single(first.Steps).Code);
        Assert.Equal(OverlayFocusSignature(review, "Signer"), OverlayFocusSignature(review, "Signer"));
    }

    [Fact]
    public void SemanticAuditSnapshot_IsStableAcrossRepeatedRuns()
    {
        var first = LoadSampleReview();
        var second = LoadSampleReview();
        var firstSnapshot = ReviewSemanticAudit.CreateSnapshot(ReviewEvidence(first), first.PageOverlays);
        var secondSnapshot = ReviewSemanticAudit.CreateSnapshot(ReviewEvidence(second), second.PageOverlays);

        Assert.Equal(AuditSignature(firstSnapshot), AuditSignature(secondSnapshot));
        Assert.Contains(firstSnapshot.ResolverOrdering, line => line.Contains("semantic=signer.name", StringComparison.Ordinal));
        Assert.Contains(firstSnapshot.SemanticGrouping, line => line.Contains("field=Signer", StringComparison.Ordinal));
        Assert.DoesNotContain(firstSnapshot.OverlayTraversal, line => line.Contains(DateTime.UtcNow.Year.ToString(), StringComparison.Ordinal));
    }

    [Fact]
    public void SemanticDiffing_IsDeterministicForOrderingPrimaryGroupAndFocusDeltas()
    {
        var previousField = CreateReviewField("Signer", "PHAM A", new[]
        {
            CreateReviewEvidence(0, 680, 690, "PHAM A", "signer.name"),
            WithRole(CreateReviewEvidence(0, 707, 667, "CHU TICH", "signer.role"), EvidenceRole.Supporting)
        });
        var currentField = CreateReviewField("Signer", "PHAM B", new[]
        {
            WithRole(CreateReviewEvidence(0, 707, 667, "CHU TICH", "signer.role"), EvidenceRole.Supporting),
            CreateReviewEvidence(0, 682, 720, "PHAM B", "signer.name")
        });
        var previousEvidence = previousField.EvidenceRegions;
        var currentEvidence = currentField.EvidenceRegions;
        var previousPages = ReviewOverlayMapper.CreatePages(new[] { previousField }, Array.Empty<ReviewCandidate>(), MultiPageMetrics());
        var currentPages = ReviewOverlayMapper.CreatePages(new[] { currentField }, Array.Empty<ReviewCandidate>(), MultiPageMetrics());
        var previousFocus = ReviewOverlayFocusState.FromRegion(previousPages.SelectMany(page => page.Regions).Single(region => region.IsPrimary));
        var restoredFocus = ReviewOverlayFocusState.FromRegion(currentPages.SelectMany(page => page.Regions).Single(region => region.IsPrimary));
        var previousSnapshot = ReviewSemanticAudit.CreateSnapshot(
            previousEvidence,
            previousPages,
            new[] { new ReviewReplayStep("focus-before", "Signer", "test", string.Empty, previousFocus.EvidenceKey ?? string.Empty) });
        var currentSnapshot = ReviewSemanticAudit.CreateSnapshot(
            currentEvidence,
            currentPages,
            new[] { new ReviewReplayStep("focus-after", "Signer", "test", previousFocus.EvidenceKey ?? string.Empty, restoredFocus.EvidenceKey ?? string.Empty) });

        var first = ReviewSemanticAudit.Compare(previousSnapshot, currentSnapshot);
        var second = ReviewSemanticAudit.Compare(previousSnapshot, currentSnapshot);

        Assert.Equal(SemanticDiffSignature(first), SemanticDiffSignature(second));
        Assert.NotEmpty(first.OrderingDeltas);
        Assert.Contains(first.PrimaryEvidenceDeltas, line => line.Contains("Signer", StringComparison.Ordinal));
        Assert.Contains(first.SemanticGroupDeltas, line => line.Contains("field=Signer", StringComparison.Ordinal));
        Assert.NotEmpty(first.FocusRestorationDeltas);
    }

    [Fact]
    public void FocusRestorationReplay_ExplainsFallbackPathWithoutChangingAuthority()
    {
        var firstField = CreateReviewField("Issuer", "UBND TINH THANH HOA", new[]
        {
            CreateReviewEvidence(0, 80, 40, "UBND TINH THANH HOA", "issuer.name")
        });
        var secondField = CreateReviewField("Issuer", "UBND TINH THANH HOA", new[]
        {
            CreateReviewEvidence(0, 82, 42, "UBND TINH THANH HOA", "issuer.name")
        });
        var firstPages = ReviewOverlayMapper.CreatePages(new[] { firstField }, Array.Empty<ReviewCandidate>(), MultiPageMetrics());
        var secondPages = ReviewOverlayMapper.CreatePages(new[] { secondField }, Array.Empty<ReviewCandidate>(), MultiPageMetrics());
        var captured = ReviewOverlayFocusState.FromRegion(firstPages.SelectMany(page => page.Regions).Single());
        var navigator = new ReviewOverlayNavigator(secondPages);

        var replay = navigator.RestoreFocusWithReplay(captured);

        Assert.True(replay.Succeeded);
        Assert.Equal("focus-restored-semantic-group-primary", Assert.Single(replay.Steps).Code);
        Assert.Equal("Issuer", ActiveRegion(secondPages, navigator).FieldName);
    }

    [Fact]
    public void OverlayTraversalReplay_IsResolverOrderedAndStable()
    {
        var review = LoadSampleReview();
        var firstNavigator = new ReviewOverlayNavigator(review.PageOverlays);
        var secondNavigator = new ReviewOverlayNavigator(review.PageOverlays);

        var first = new[] { firstNavigator.NavigateNextRegionWithReplay(), firstNavigator.NavigateNextRegionWithReplay() };
        var second = new[] { secondNavigator.NavigateNextRegionWithReplay(), secondNavigator.NavigateNextRegionWithReplay() };

        Assert.Equal(first.Select(ReplaySignature).ToArray(), second.Select(ReplaySignature).ToArray());
        Assert.All(first, replay => Assert.Equal("traversal-next-region", Assert.Single(replay.Steps).Code));
    }

    [Fact]
    public void SemanticAuditSnapshot_RecordsContextualAndSuppressionReasons()
    {
        var firstPageHeader = CreateReviewEvidence(0, 80, 40, "UBND TINH THANH HOA", "issuer.name", "page_header");
        var secondPageHeader = CreateReviewEvidence(1, 80, 40, "UBND TINH THANH HOA", "issuer.name", "page_header");
        var contextual = WithRole(CreateReviewEvidence(1, 120, 240, "Khoan 2. Context", "body.section-paragraph"), EvidenceRole.Contextual);
        var evidence = new[] { secondPageHeader, contextual, firstPageHeader };
        var field = CreateReviewField("Issuer", "UBND TINH THANH HOA", evidence);
        var pages = ReviewOverlayMapper.CreatePages(new[] { field }, Array.Empty<ReviewCandidate>(), MultiPageMetrics());

        var snapshot = ReviewSemanticAudit.CreateSnapshot(evidence, pages);

        Assert.Contains(snapshot.ResolverOrdering, line => line.Contains("role=Contextual", StringComparison.Ordinal));
        Assert.Contains(snapshot.ProvenanceDiagnostics, line => line.Contains("contextual-evidence", StringComparison.Ordinal));
        Assert.Contains(snapshot.ProvenanceDiagnostics, line => line.Contains("repeated-header-footer-demoted", StringComparison.Ordinal));
    }

    [Fact]
    public void RuntimeRefreshCoordinator_RejectsOverlappingStaleCompletionDeterministically()
    {
        using var coordinator = new ReviewRuntimeCoordinator();
        using var first = coordinator.BeginRefresh();
        using var second = coordinator.BeginRefresh();

        var stale = coordinator.RejectIfStale(first, "stale-refresh-rejected");
        var current = coordinator.Complete(second);

        Assert.False(stale.ShouldApply);
        Assert.True(current.ShouldApply);
        Assert.Equal(new[] { "refresh-started", "refresh-started", "stale-refresh-rejected", "refresh-applied" }, RuntimeDiagnosticCodes(coordinator));
    }

    [Fact]
    public void RuntimeRefreshCoordinator_CancelsPreviousRefreshAndCleanupIsDeterministic()
    {
        using var coordinator = new ReviewRuntimeCoordinator();
        using var first = coordinator.BeginRefresh();
        using var second = coordinator.BeginRefresh();

        Assert.True(first.Token.IsCancellationRequested);
        Assert.False(second.Token.IsCancellationRequested);
        coordinator.CancelRefresh();
        var canceled = coordinator.Complete(second, canceled: true);

        Assert.False(canceled.ShouldApply);
        Assert.True(canceled.WasCanceled);
        Assert.Equal(new[] { "refresh-started", "refresh-started", "overlay-remap-canceled", "refresh-canceled" }, RuntimeDiagnosticCodes(coordinator));
    }

    [Fact]
    public void RuntimeRefreshCoordinator_PreservesLatestRefreshAcrossRepeatedAsyncOrdering()
    {
        using var coordinator = new ReviewRuntimeCoordinator();
        var requests = new[] { coordinator.BeginRefresh(), coordinator.BeginRefresh(), coordinator.BeginRefresh() };
        try
        {
            var first = coordinator.RejectIfStale(requests[0], "stale-refresh-rejected");
            var second = coordinator.RejectIfStale(requests[1], "stale-refresh-rejected");
            var third = coordinator.Complete(requests[2]);

            Assert.False(first.ShouldApply);
            Assert.False(second.ShouldApply);
            Assert.True(third.ShouldApply);
            Assert.Equal(3, third.Version);
        }
        finally
        {
            foreach (var request in requests)
            {
                request.Dispose();
            }
        }
    }

    [Fact]
    public void RuntimeStaleFocusAndReplayInvalidation_AreObservableOnly()
    {
        using var coordinator = new ReviewRuntimeCoordinator();
        using var first = coordinator.BeginRefresh();
        using var second = coordinator.BeginRefresh();

        Assert.False(coordinator.RejectIfStale(first.Version, "replay-invalidation").ShouldApply);
        Assert.False(coordinator.RejectIfStale(first.Version, "focus-restoration-invalidation").ShouldApply);
        Assert.True(coordinator.RejectIfStale(second.Version, "focus-restoration-invalidation").ShouldApply);

        Assert.Contains("replay-invalidation", RuntimeDiagnosticCodes(coordinator));
        Assert.Contains("focus-restoration-invalidation", RuntimeDiagnosticCodes(coordinator));
    }

    [Fact]
    public void OverlayVirtualizationContinuity_RestoresFocusAcrossReusedSemanticIdentity()
    {
        var firstField = CreateReviewField("Issuer", "UBND TINH THANH HOA", new[]
        {
            CreateReviewEvidence(0, 80, 40, "UBND TINH THANH HOA", "issuer.name")
        });
        var remappedField = CreateReviewField("Issuer", "UBND TINH THANH HOA", new[]
        {
            WithRole(CreateReviewEvidence(0, 560, 40, "CONG HOA XA HOI CHU NGHIA VIET NAM", "issuer.context"), EvidenceRole.Contextual),
            CreateReviewEvidence(0, 80, 40, "UBND TINH THANH HOA", "issuer.name")
        });
        var firstPages = ReviewOverlayMapper.CreatePages(new[] { firstField }, Array.Empty<ReviewCandidate>(), MultiPageMetrics());
        var remappedPages = ReviewOverlayMapper.CreatePages(new[] { remappedField }, Array.Empty<ReviewCandidate>(), MultiPageMetrics());
        var captured = ReviewOverlayFocusState.FromRegion(firstPages.SelectMany(page => page.Regions).Single(region => region.IsPrimary));
        var navigator = new ReviewOverlayNavigator(remappedPages);

        Assert.True(navigator.RestoreFocus(captured));

        var restored = ActiveRegion(remappedPages, navigator);
        Assert.Equal("Issuer", restored.FieldName);
        Assert.True(restored.IsPrimary);
        Assert.Equal(captured.EvidenceKey, ReviewOverlayIdentity.EvidenceKey(restored));
    }

    [Fact]
    public void FocusStabilityUnderRefreshRaces_LatestOverlayWinsWithoutStateResurrection()
    {
        using var coordinator = new ReviewRuntimeCoordinator();
        using var firstRefresh = coordinator.BeginRefresh();
        var firstPages = ReviewOverlayMapper.CreatePages(new[] { CreateReviewField("Signer", "PHAM A", new[] { CreateReviewEvidence(0, 680, 690, "PHAM A", "signer.name") }) }, Array.Empty<ReviewCandidate>(), MultiPageMetrics());
        var captured = ReviewOverlayFocusState.FromRegion(firstPages.SelectMany(page => page.Regions).Single());
        using var secondRefresh = coordinator.BeginRefresh();
        var secondPages = ReviewOverlayMapper.CreatePages(new[] { CreateReviewField("Signer", "PHAM B", new[] { CreateReviewEvidence(0, 682, 720, "PHAM B", "signer.name") }) }, Array.Empty<ReviewCandidate>(), MultiPageMetrics());

        Assert.False(coordinator.RejectIfStale(firstRefresh, "stale-refresh-rejected").ShouldApply);
        Assert.True(coordinator.RejectIfStale(secondRefresh, "stale-refresh-rejected").ShouldApply);

        var navigator = new ReviewOverlayNavigator(secondPages);
        var replay = navigator.RestoreFocusWithReplay(captured);

        Assert.True(replay.Succeeded);
        Assert.Equal("focus-restored-field-primary", Assert.Single(replay.Steps).Code);
        Assert.Equal("PHAM B", ActiveRegion(secondPages, navigator).SourceText);
    }

    [Fact]
    public void RuntimeDiagnostics_AreCompactAndStableAcrossRepeatedRuns()
    {
        var first = RuntimeDiagnosticScenario();
        var second = RuntimeDiagnosticScenario();

        Assert.Equal(first, second);
        Assert.All(first, code => Assert.DoesNotContain(DateTime.UtcNow.Year.ToString(), code, StringComparison.Ordinal));
    }

    [Fact]
    public void ReviewHealthDiagnostics_CountBoundedRuntimeFailuresDeterministically()
    {
        using var coordinator = new ReviewRuntimeCoordinator();
        using var first = coordinator.BeginRefresh();
        using var second = coordinator.BeginRefresh();
        _ = coordinator.RejectIfStale(first, "stale-refresh-rejected");
        _ = coordinator.RejectIfStale(first.Version, "replay-invalidation");
        coordinator.CancelRefresh();
        var replay = new ReviewReplayStep(
            "focus-restored-field-primary",
            "Signer",
            "focus restored to resolver primary for matching field");

        var health = ReviewOperationalObservability.CreateHealthDiagnostics(
            coordinator.CreateSnapshot(),
            new[] { replay },
            new[] { new ReviewConsistencyCheck("resolver", true, "ok") });

        Assert.Equal(1, health.StaleRefreshRejections);
        Assert.Equal(1, health.ReplayInvalidations);
        Assert.Equal(1, health.OverlayRemapCancellations);
        Assert.Equal(1, health.FocusRestorationFallbacks);
        Assert.True(health.ResolverConsistencyVerified);
    }

    [Fact]
    public void FailureTransparency_ReasonsAreCompactAndDeterministic()
    {
        using var coordinator = new ReviewRuntimeCoordinator();
        using var first = coordinator.BeginRefresh();
        using var second = coordinator.BeginRefresh();
        _ = coordinator.RejectIfStale(first, "stale-refresh-rejected");
        coordinator.CancelRefresh();
        _ = coordinator.Complete(second, canceled: true);
        var replaySteps = new[]
        {
            new ReviewReplayStep("focus-restored-field-primary", "Signer", "focus restored to resolver primary for matching field")
        };

        var firstReasons = ReviewOperationalObservability.CreateFailureReasons(coordinator.CreateSnapshot(), replaySteps);
        var secondReasons = ReviewOperationalObservability.CreateFailureReasons(coordinator.CreateSnapshot(), replaySteps);

        Assert.Equal(firstReasons, secondReasons);
        Assert.Contains(firstReasons, line => line.Contains("stale-refresh-rejected", StringComparison.Ordinal));
        Assert.Contains(firstReasons, line => line.Contains("overlay-remap-canceled", StringComparison.Ordinal));
        Assert.Contains(firstReasons, line => line.Contains("focus-restored-field-primary", StringComparison.Ordinal));
        Assert.DoesNotContain(firstReasons, line => line.Contains("System.", StringComparison.Ordinal));
    }

    [Fact]
    public void RuntimeObservabilitySnapshot_IsDiffFriendlyAndStableAcrossRepeatedRuns()
    {
        var review = LoadSampleReview();
        var firstSnapshot = CreateOperationalSnapshotScenario(review);
        var secondSnapshot = CreateOperationalSnapshotScenario(review);

        Assert.Equal(OperationalSnapshotSignature(firstSnapshot), OperationalSnapshotSignature(secondSnapshot));
        Assert.Contains(firstSnapshot.RuntimeRefreshState, line => line.StartsWith("version=", StringComparison.Ordinal));
        Assert.Contains(firstSnapshot.SemanticConsistencyResults, line => line.Contains("overlay-order-matches-resolver", StringComparison.Ordinal));
        Assert.All(OperationalSnapshotSignature(firstSnapshot), line => Assert.DoesNotContain(DateTime.UtcNow.Year.ToString(), line, StringComparison.Ordinal));
    }

    [Fact]
    public void OverlayConsistencyVerification_ConfirmsResolverTraversalAndFocusPaths()
    {
        var review = LoadSampleReview();
        var signer = review.PageOverlays.SelectMany(page => page.Regions).Single(region => region.FieldName == "Signer" && region.IsPrimary);
        var navigator = new ReviewOverlayNavigator(review.PageOverlays);
        var replay = navigator.NavigateToRegionWithReplay(signer.Id);

        var checks = ReviewOperationalObservability.VerifyConsistency(
            ReviewEvidence(review),
            review.PageOverlays,
            replay);

        Assert.All(checks, check => Assert.True(check.Passed, check.Detail));
        Assert.Contains(checks, check => check.Code == "overlay-order-matches-resolver");
        Assert.Contains(checks, check => check.Code == "traversal-order-matches-overlay");
        Assert.Contains(checks, check => check.Code == "focus-restoration-path-valid");
    }

    [Fact]
    public void SemanticGroupConsistency_RemainsStableUnderContextualRemap()
    {
        var baseField = CreateReviewField("Issuer", "UBND TINH THANH HOA", new[]
        {
            CreateReviewEvidence(0, 80, 40, "UBND TINH THANH HOA", "issuer.name")
        });
        var remappedField = CreateReviewField("Issuer", "UBND TINH THANH HOA", new[]
        {
            WithRole(CreateReviewEvidence(0, 560, 40, "CONG HOA XA HOI CHU NGHIA VIET NAM", "issuer.context"), EvidenceRole.Contextual),
            CreateReviewEvidence(0, 80, 40, "UBND TINH THANH HOA", "issuer.name")
        });
        var previousPages = ReviewOverlayMapper.CreatePages(new[] { baseField }, Array.Empty<ReviewCandidate>(), MultiPageMetrics());
        var currentPages = ReviewOverlayMapper.CreatePages(new[] { remappedField }, Array.Empty<ReviewCandidate>(), MultiPageMetrics());

        var checks = ReviewOperationalObservability.VerifyConsistency(
            remappedField.EvidenceRegions,
            currentPages,
            previousPages: previousPages);

        Assert.Contains(checks, check => check.Code == "semantic-group-stable-after-remap" && check.Passed);
    }

    [Fact]
    public void OperationalConsistency_RepeatedRefreshSnapshotsRemainStable()
    {
        var first = RuntimeObservationScenarioSignature();
        var second = RuntimeObservationScenarioSignature();

        Assert.Equal(first, second);
        Assert.Contains(first, line => line.Contains("stale-refresh-rejected", StringComparison.Ordinal));
        Assert.Contains(first, line => line.Contains("refresh-canceled", StringComparison.Ordinal));
    }

    [Fact]
    public void Guardrail_DiagnosticsDoNotAffectResolverOrdering()
    {
        var evidence = new[]
        {
            CreateReviewEvidence(0, 700, 660, "CHU TICH HDQT", "signer.role"),
            CreateReviewEvidence(0, 682, 690, "PHAM CHANH HA", "signer.name"),
            WithRole(CreateReviewEvidence(0, 120, 300, "Khoan 1. Context", "body.section-paragraph"), EvidenceRole.Contextual)
        };
        var before = ReviewEvidenceResolver.OrderReviewEvidence(evidence).Select(EvidenceSignature).ToArray();

        _ = evidence.Select(item => ReviewEvidenceResolver.ExplainReviewEvidence(evidence, item).Select(diagnostic => diagnostic.Code).ToArray()).ToArray();
        var after = ReviewEvidenceResolver.OrderReviewEvidence(evidence).Select(EvidenceSignature).ToArray();

        Assert.Equal(before, after);
    }

    [Fact]
    public void Guardrail_AuditDiffReplayAndObservabilityDoNotAffectOrdering()
    {
        var review = LoadSampleReview();
        var evidence = ReviewEvidence(review);
        var before = ReviewEvidenceResolver.OrderReviewEvidence(evidence).Select(EvidenceSignature).ToArray();
        var signer = review.PageOverlays.SelectMany(page => page.Regions).Single(region => region.FieldName == "Signer" && region.IsPrimary);
        var replay = new ReviewOverlayNavigator(review.PageOverlays).NavigateToRegionWithReplay(signer.Id);
        var snapshot = ReviewSemanticAudit.CreateSnapshot(evidence, review.PageOverlays, replay.Steps);
        var diff = ReviewSemanticAudit.Compare(snapshot, snapshot);
        using var coordinator = new ReviewRuntimeCoordinator();
        using var refresh = coordinator.BeginRefresh();
        _ = ReviewOperationalObservability.CreateSnapshot(
            coordinator.CreateSnapshot(),
            review.PageOverlays,
            replay.Steps,
            ReviewOperationalObservability.VerifyConsistency(evidence, review.PageOverlays, replay));
        _ = diff.OrderingDeltas.Count + refresh.Version;

        var after = ReviewEvidenceResolver.OrderReviewEvidence(evidence).Select(EvidenceSignature).ToArray();

        Assert.Equal(before, after);
    }

    [Fact]
    public void Guardrail_OverlayTraversalMatchesResolverProjectionOrder()
    {
        var review = LoadSampleReview();
        var overlayRegions = review.PageOverlays.SelectMany(page => page.Regions).ToArray();
        var resolverProjection = overlayRegions
            .Select(region => new ReviewEvidenceRegion(
                region.PageIndex,
                region.BoundingBox,
                region.SourceText,
                region.BlockType,
                0,
                region.IsPrimary,
                region.Role,
                region.Provenance,
                ReviewOverlayIdentity.SourceKey(region)))
            .ToArray();
        var expected = ReviewEvidenceResolver.OrderReviewEvidence(resolverProjection)
            .Select(evidence => evidence.SemanticGroupId)
            .ToArray();
        var navigator = new ReviewOverlayNavigator(review.PageOverlays);
        var actual = new List<string>();
        while (navigator.NavigateNextRegion())
        {
            var active = review.PageOverlays
                .SelectMany(page => page.Regions)
                .Single(region => region.Id == navigator.ActiveRegionId);
            actual.Add(ReviewOverlayIdentity.SourceKey(active));
        }

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void Guardrail_RepeatedHeaderFooterEvidenceIsDemotedNotRemoved()
    {
        var firstPageHeader = CreateReviewEvidence(0, 80, 40, "UBND TINH THANH HOA", "issuer.name", "page_header");
        var secondPageHeader = CreateReviewEvidence(1, 80, 40, "UBND TINH THANH HOA", "issuer.name", "page_header");
        var secondPageBody = CreateReviewEvidence(1, 120, 240, "Dieu 2. Noi dung tiep theo", "body.section");

        var ordered = ReviewEvidenceResolver.OrderReviewEvidence(new[] { secondPageHeader, secondPageBody, firstPageHeader });

        Assert.Contains(firstPageHeader, ordered);
        Assert.Contains(secondPageHeader, ordered);
        Assert.True(Array.IndexOf(ordered.ToArray(), secondPageHeader) > Array.IndexOf(ordered.ToArray(), secondPageBody));
        Assert.Contains("repeated-header-footer-demoted", DiagnosticCodes(new[] { secondPageHeader, secondPageBody, firstPageHeader }, secondPageHeader));
    }

    [Fact]
    public void Guardrail_FocusRestorationUsesSemanticIdentityBeforeRawCoordinates()
    {
        var previousSigner = CreateReviewField("Signer", "PHAM CHANH HA", new[]
        {
            CreateReviewEvidence(0, 682, 690, "PHAM CHANH HA", "signer.name")
        });
        var previousPages = ReviewOverlayMapper.CreatePages(new[] { previousSigner }, Array.Empty<ReviewCandidate>(), MultiPageMetrics());
        var captured = ReviewOverlayFocusState.FromRegion(previousPages.SelectMany(page => page.Regions).Single());
        var coordinateCollision = CreateReviewField("Issuer", "PHAM CHANH HA", new[]
        {
            CreateReviewEvidence(0, 682, 690, "PHAM CHANH HA", "issuer.name")
        });
        var movedSigner = CreateReviewField("Signer", "PHAM CHANH HA", new[]
        {
            CreateReviewEvidence(0, 700, 720, "PHAM CHANH HA", "signer.name")
        });
        var currentPages = ReviewOverlayMapper.CreatePages(new[] { coordinateCollision, movedSigner }, Array.Empty<ReviewCandidate>(), MultiPageMetrics());
        var navigator = new ReviewOverlayNavigator(currentPages);

        var replay = navigator.RestoreFocusWithReplay(captured);

        Assert.True(replay.Succeeded);
        Assert.Contains(Assert.Single(replay.Steps).Code, new[] { "focus-restored-semantic-group-primary", "focus-restored-field-primary" });
        Assert.Equal("Signer", ActiveRegion(currentPages, navigator).FieldName);
        Assert.NotEqual("Issuer", ActiveRegion(currentPages, navigator).FieldName);
    }

    [Fact]
    public void Guardrail_SinglePageEvidenceOrderingIsStable()
    {
        var evidence = new[]
        {
            WithRole(CreateReviewEvidence(0, 140, 260, "Khoan 1. Context", "body.section-paragraph"), EvidenceRole.Contextual),
            CreateReviewEvidence(0, 700, 660, "CHU TICH HDQT", "signer.role"),
            CreateReviewEvidence(0, 120, 220, "Dieu 1. Noi dung", "body.section"),
            CreateReviewEvidence(0, 682, 690, "PHAM CHANH HA", "signer.name")
        };

        var ordered = ReviewEvidenceResolver.OrderReviewEvidence(evidence).Select(evidence => evidence.SourceText).ToArray();

        Assert.Equal(new[] { "Dieu 1. Noi dung", "PHAM CHANH HA", "CHU TICH HDQT", "Khoan 1. Context" }, ordered);
    }

    [Fact]
    public void Guardrail_DeterministicSnapshotOrdersRemainStable()
    {
        var review = LoadSampleReview();
        var signer = review.PageOverlays.SelectMany(page => page.Regions).Single(region => region.FieldName == "Signer" && region.IsPrimary);
        var replay = new ReviewOverlayNavigator(review.PageOverlays).NavigateToRegionWithReplay(signer.Id);
        var checks = ReviewOperationalObservability.VerifyConsistency(ReviewEvidence(review), review.PageOverlays, replay);
        var operational = ReviewOperationalObservability.CreateSnapshot(
            new ReviewRuntimeStateSnapshot(2, true, new[]
            {
                new ReviewRuntimeDiagnostic("refresh-started", 1, "new refresh version started"),
                new ReviewRuntimeDiagnostic("stale-refresh-rejected", 1, "stale refresh completion rejected"),
                new ReviewRuntimeDiagnostic("refresh-started", 2, "new refresh version started")
            }),
            review.PageOverlays,
            replay.Steps,
            checks);

        var diagnosticCodes = review.Fields
            .Single(field => field.Name == "Signer")
            .EvidenceRegions
            .Single(evidence => evidence.Provenance?.SemanticRole == "signer.name")
            .Provenance!
            .Diagnostics
            .Select(diagnostic => diagnostic.Code)
            .ToArray();

        Assert.Equal(new[] { "primary-canonical", "zone-bottom-right-signer", "deterministic-tiebreak" }, diagnosticCodes);
        Assert.Equal(new[] { "selection-region-id" }, replay.Steps.Select(step => step.Code).ToArray());
        Assert.Equal(checks.Select(check => check.Code).OrderBy(code => code, StringComparer.Ordinal).ToArray(), checks.Select(check => check.Code).ToArray());
        Assert.Equal(operational.SemanticConsistencyResults.OrderBy(line => line, StringComparer.Ordinal).ToArray(), operational.SemanticConsistencyResults.ToArray());
        AssertNoRuntimeValues(OperationalSnapshotSignature(operational));
    }

    [Fact]
    public void Stress_LargeMultiPageDocumentDeterminism_IsStableAcrossOrderingTraversalFocusAndAudit()
    {
        var evidence = CreateLargeStressEvidence(pageCount: 12, sectionsPerPage: 8, contextualPerSection: 4);
        var fields = CreateStressFields(evidence);
        var pages = ReviewOverlayMapper.CreatePages(fields, Array.Empty<ReviewCandidate>(), StressMetrics(12));
        var primary = pages.SelectMany(page => page.Regions).First(region => region.IsPrimary);
        var replay = new ReviewOverlayNavigator(pages).NavigateToRegionWithReplay(primary.Id);
        var firstAudit = ReviewSemanticAudit.CreateSnapshot(evidence, pages, replay.Steps);
        var secondAudit = ReviewSemanticAudit.CreateSnapshot(evidence, pages, replay.Steps);
        var firstTraversal = TraverseSignature(pages);
        var secondTraversal = TraverseSignature(pages);

        Assert.Equal(
            ReviewEvidenceResolver.OrderReviewEvidence(evidence).Select(EvidenceSignature).ToArray(),
            ReviewEvidenceResolver.OrderReviewEvidence(evidence).Select(EvidenceSignature).ToArray());
        Assert.Equal(firstTraversal, secondTraversal);
        Assert.True(replay.Succeeded);
        Assert.Equal(AuditSignature(firstAudit), AuditSignature(secondAudit));
        Assert.True(pages.SelectMany(page => page.Regions).Count() > 300);
    }

    [Fact]
    public void Stress_CorruptedOcrNoise_DoesNotCreateOrderingDriftOrTraversalCollapse()
    {
        var evidence = new[]
        {
            CreateReviewEvidence(0, 80, 40, " UBND   TINH   THANH HOA ", "issuer.name", "page_header"),
            CreateReviewEvidence(1, 80, 40, "UBND TINH THANH HOA", "issuer.name", "page_header"),
            CreateReviewEvidence(0, 120, 220, "Dieu 1.\tNoi dung bi loi   khoang    trang", "body.section"),
            WithRole(CreateReviewEvidence(0, 122, 260, "Dieu 1. Noi dung bi loi khoang trang", "body.section-paragraph"), EvidenceRole.Contextual),
            CreateReviewEvidence(0, 680, 690, "PHAM  CHANH   HA", "signer.name"),
            CreateReviewEvidence(0, 680, 690, "PHAM CHANH HA", "signer.name"),
            CreateReviewEvidence(1, 90, 1260, "Noi nhan:\r\nLuu VT. 2", "archive.reference", "page_footer"),
            CreateReviewEvidence(0, 90, 1260, "Noi nhan: Luu VT 1", "archive.reference", "page_footer"),
        };
        var first = ReviewEvidenceResolver.OrderReviewEvidence(evidence).Select(EvidenceSignature).ToArray();
        var second = ReviewEvidenceResolver.OrderReviewEvidence(evidence.Reverse().ToArray()).Select(EvidenceSignature).ToArray();
        var pages = ReviewOverlayMapper.CreatePages(CreateStressFields(evidence), Array.Empty<ReviewCandidate>(), StressMetrics(2));

        Assert.Equal(first, second);
        Assert.Equal(pages.SelectMany(page => page.Regions).Count(), TraverseSignature(pages).Length);
        Assert.Contains("repeated-header-footer-demoted", DiagnosticCodes(evidence, evidence[1]));
        Assert.Contains("repeated-header-footer-demoted", DiagnosticCodes(evidence, evidence[6]));
    }

    [Fact]
    public void Stress_RefreshCancellationStorm_KeepsLatestWinsAndDiagnosticsBounded()
    {
        using var coordinator = new ReviewRuntimeCoordinator();
        var requests = new List<ReviewRefreshRequest>();
        try
        {
            for (var i = 0; i < 40; i++)
            {
                requests.Add(coordinator.BeginRefresh());
                if (i % 3 == 0 && i < 39)
                {
                    coordinator.CancelRefresh();
                }
            }

            for (var i = 0; i < requests.Count - 1; i++)
            {
                _ = coordinator.RejectIfStale(requests[i], "stale-refresh-rejected");
            }

            var latest = coordinator.RejectIfStale(requests[^1], "stale-refresh-rejected");
            var snapshot = coordinator.CreateSnapshot();

            Assert.True(latest.ShouldApply);
            Assert.True(snapshot.Diagnostics.Count <= 16);
            Assert.Equal(40, snapshot.CurrentVersion);
            Assert.Contains(snapshot.Diagnostics, diagnostic => diagnostic.Code == "stale-refresh-rejected");
        }
        finally
        {
            foreach (var request in requests)
            {
                request.Dispose();
            }
        }
    }

    [Fact]
    public void Stress_RapidRemapsAndVirtualizedOverlayReuse_PreserveFocusContinuity()
    {
        var evidence = CreateLargeStressEvidence(pageCount: 4, sectionsPerPage: 4, contextualPerSection: 2);
        var basePages = ReviewOverlayMapper.CreatePages(CreateStressFields(evidence), Array.Empty<ReviewCandidate>(), StressMetrics(4));
        var selected = basePages.SelectMany(page => page.Regions).First(region => region.FieldName == "Dieu 1" && region.IsPrimary);
        var focus = ReviewOverlayFocusState.FromRegion(selected);

        for (var iteration = 0; iteration < 12; iteration++)
        {
            var remapped = iteration % 2 == 0
                ? evidence.Concat(new[] { WithRole(CreateReviewEvidence(0, 500 + iteration, 320, $"Context insert {iteration}", "body.section-paragraph"), EvidenceRole.Contextual) }).ToArray()
                : evidence.Reverse().ToArray();
            var pages = ReviewOverlayMapper.CreatePages(CreateStressFields(remapped), Array.Empty<ReviewCandidate>(), StressMetrics(4));
            var navigator = new ReviewOverlayNavigator(pages);

            Assert.True(navigator.RestoreFocus(focus));
            Assert.Equal("Dieu 1", ActiveRegion(pages, navigator).FieldName);
            Assert.Equal(focus.EvidenceKey, ReviewOverlayIdentity.EvidenceKey(ActiveRegion(pages, navigator)));
        }
    }

    [Fact]
    public void Stress_ProfilingSnapshot_IsBoundedDeterministicAndObservational()
    {
        var evidence = CreateLargeStressEvidence(pageCount: 6, sectionsPerPage: 5, contextualPerSection: 3);
        var pages = ReviewOverlayMapper.CreatePages(CreateStressFields(evidence), Array.Empty<ReviewCandidate>(), StressMetrics(6));
        using var coordinator = new ReviewRuntimeCoordinator();
        using var first = coordinator.BeginRefresh();
        using var second = coordinator.BeginRefresh();
        _ = coordinator.RejectIfStale(first, "stale-refresh-rejected");
        var replay = new ReviewOverlayNavigator(pages).NavigateNextRegionWithReplay();

        var firstProfile = ReviewOperationalObservability.CreateProfilingSnapshot(evidence, pages, coordinator.CreateSnapshot(), replay.Steps);
        var secondProfile = ReviewOperationalObservability.CreateProfilingSnapshot(evidence, pages, coordinator.CreateSnapshot(), replay.Steps);
        var before = ReviewEvidenceResolver.OrderReviewEvidence(evidence).Select(EvidenceSignature).ToArray();
        _ = firstProfile.ResolverEvidenceCount + firstProfile.OverlayRegionCount;
        var after = ReviewEvidenceResolver.OrderReviewEvidence(evidence).Select(EvidenceSignature).ToArray();

        Assert.Equal(firstProfile, secondProfile);
        Assert.Equal(evidence.Length, firstProfile.ResolverEvidenceCount);
        Assert.Equal(pages.SelectMany(page => page.Regions).Count(), firstProfile.OverlayRegionCount);
        Assert.Equal(before, after);
    }

    [Fact]
    public void Stress_GuardrailsHoldUnderLargeEvidenceSets()
    {
        var evidence = CreateLargeStressEvidence(pageCount: 8, sectionsPerPage: 6, contextualPerSection: 3);
        var pages = ReviewOverlayMapper.CreatePages(CreateStressFields(evidence), Array.Empty<ReviewCandidate>(), StressMetrics(8));
        var before = ReviewEvidenceResolver.OrderReviewEvidence(evidence).Select(EvidenceSignature).ToArray();
        var replay = new ReviewOverlayNavigator(pages).NavigateNextRegionWithReplay();
        _ = ReviewSemanticAudit.CreateSnapshot(evidence, pages, replay.Steps);
        _ = ReviewOperationalObservability.CreateSnapshot(
            new ReviewRuntimeStateSnapshot(1, false, Array.Empty<ReviewRuntimeDiagnostic>()),
            pages,
            replay.Steps,
            ReviewOperationalObservability.VerifyConsistency(evidence, pages, replay));
        var after = ReviewEvidenceResolver.OrderReviewEvidence(evidence).Select(EvidenceSignature).ToArray();

        var firstChecks = ReviewOperationalObservability.VerifyConsistency(evidence, pages, replay);
        var secondChecks = ReviewOperationalObservability.VerifyConsistency(evidence, pages, replay);

        Assert.Equal(before, after);
        Assert.Equal(
            firstChecks.Select(check => $"{check.Code}|{check.Passed}|{check.Detail}").ToArray(),
            secondChecks.Select(check => $"{check.Code}|{check.Passed}|{check.Detail}").ToArray());
        Assert.Contains(firstChecks, check => check.Code == "overlay-order-matches-resolver" && check.Passed);
        Assert.Contains(firstChecks, check => check.Code == "traversal-order-matches-overlay" && check.Passed);
        Assert.Contains(firstChecks, check => check.Code == "focus-restoration-path-valid" && check.Passed);
    }

    [Fact]
    public void Resolver_LargeEvidenceGroupOrderingIsDeterministicAndUiIndependent()
    {
        var evidence = Enumerable.Range(0, 2_000)
            .Select(index => new ReviewEvidenceRegion(
                0,
                new BoundingBox(index % 100, index / 100, (index % 100) + 10, (index / 100) + 10),
                $"Text {index}",
                "paragraph",
                2_000 - index,
                false,
                index % 5 == 0 ? EvidenceRole.Primary : EvidenceRole.Contextual,
                new EvidenceProvenance("perf", string.Empty, $"Text {index}", $"Text {index}", index % 5 == 0 ? "body.section-heading" : "body.section-paragraph")))
            .Reverse()
            .ToArray();

        var elapsed = Stopwatch.StartNew();
        var first = ReviewEvidenceResolver.OrderReviewEvidence(evidence);
        var second = ReviewEvidenceResolver.OrderReviewEvidence(evidence);
        elapsed.Stop();

        Assert.Equal(first.Select(EvidenceSignature).ToArray(), second.Select(EvidenceSignature).ToArray());
        Assert.True(elapsed.ElapsedMilliseconds < 2_000);
    }

    private static void AssertFieldEvidence(
        ReviewDocumentExtraction review,
        string fieldName,
        ReviewStatus expectedStatus,
        string expectedValue,
        params string[] expectedEvidence)
    {
        var field = review.Fields.Single(field => field.Name == fieldName);
        Assert.Equal(expectedStatus, field.Status);
        Assert.Equal(ToSearch(expectedValue), ToSearch(field.Value));

        var evidence = field.EvidenceRegions.Select(EvidenceSignature).ToArray();
        foreach (var expected in expectedEvidence)
        {
            Assert.Contains(expected, evidence);
        }
    }

    private static void AssertRejectedCandidate(
        ReviewDocumentExtraction review,
        string value,
        string semanticRole,
        string rejectReason,
        string bbox)
    {
        var candidate = review.RejectedCandidates.Single(candidate => candidate.Value == value);
        var evidence = ReviewEvidenceResolver.ResolvePrimary(candidate.EvidenceRegions);

        Assert.Equal(ReviewStatus.Rejected, candidate.Status);
        Assert.Equal(semanticRole, evidence?.Provenance?.SemanticRole);
        Assert.Contains(rejectReason, candidate.RejectReasons);
        Assert.Equal(bbox, Format(evidence?.BoundingBox));
    }

    private static void AssertReasoningOrder(ReviewCandidate candidate)
    {
        var reasoning = candidate.ReasoningChain!;
        var categories = reasoning.Select(step => step.Category).ToArray();

        Assert.Equal(
            new[]
            {
                "Accepted evidence",
                "Supporting evidence",
                "Scoring reason",
                "Scoring reason",
                "Scoring reason",
                "Scoring reason",
                "Final decision"
            },
            categories);
        Assert.Contains("PHAM CHANH HA", ToSearch(reasoning[0].Text), StringComparison.Ordinal);
        Assert.Contains("CHU TICH HDQT", ToSearch(reasoning[1].Text), StringComparison.Ordinal);
    }

    private static void AssertOverlayFocus(ReviewDocumentExtraction review, string fieldName, string expected)
    {
        Assert.Equal(expected, OverlayFocusSignature(review, fieldName));
    }

    private static void AssertDiagnostics(ReviewEvidenceRegion evidence, params string[] expectedCodes) =>
        AssertDiagnostics(evidence.Provenance!, expectedCodes);

    private static void AssertDiagnostics(EvidenceProvenance provenance, params string[] expectedCodes)
    {
        var codes = provenance.Diagnostics.Select(item => item.Code).ToArray();
        Assert.Equal(codes, codes.Distinct(StringComparer.Ordinal).ToArray());
        foreach (var expectedCode in expectedCodes)
        {
            Assert.Contains(expectedCode, codes);
        }
    }

    private static string[] DiagnosticCodes(IReadOnlyList<ReviewEvidenceRegion> evidence, ReviewEvidenceRegion target) =>
        ReviewEvidenceResolver.ExplainReviewEvidence(evidence, target)
            .Select(item => item.Code)
            .ToArray();

    private static void AssertMissingBboxProvenanceIsVisible()
    {
        var evidence = new ReviewEvidenceRegion(
            0,
            null,
            "missing",
            "paragraph",
            1,
            true,
            EvidenceRole.Primary,
            new EvidenceProvenance("test", string.Empty, "missing", "missing", "test.missing"));
        var provenance = ReviewEvidenceProvenanceFormatter.Format(evidence);

        Assert.Contains("Role: Primary", provenance, StringComparison.Ordinal);
        Assert.Contains("No bbox; evidence is retained for provenance but has no drawable overlay region from MinerU.", provenance, StringComparison.Ordinal);
    }

    private static void AssertRoleAppearsBefore(IReadOnlyList<BlockEvidence> ordered, string firstRole, string secondRole)
    {
        Assert.True(
            IndexOfRole(ordered, firstRole) < IndexOfRole(ordered, secondRole),
            $"{firstRole} should appear before {secondRole}.");
    }

    private static int IndexOfRole(IReadOnlyList<BlockEvidence> ordered, string semanticRole)
    {
        var index = ordered
            .Select((evidence, i) => (evidence, i))
            .Where(item => item.evidence.SemanticRole == semanticRole)
            .Select(item => item.i)
            .DefaultIfEmpty(-1)
            .First();

        Assert.NotEqual(-1, index);
        return index;
    }

    private static string[] BodyFieldNames(ReviewDocumentExtraction review) =>
        review.Fields
            .Where(field => field.Name.StartsWith("Dieu ", StringComparison.Ordinal))
            .Select(field => field.Name)
            .ToArray();

    private static IReadOnlyList<BlockEvidence> OrderedSampleEvidence()
    {
        var document = LoadSampleDocument();
        var sampleBlocks = document.Blocks
            .Where(IsSampleProjectionBlock)
            .Select(ToSnapshotEvidence)
            .ToArray();

        return ReviewEvidenceResolver.OrderBlockEvidence(sampleBlocks);
    }

    private static bool IsSampleProjectionBlock(DocumentBlock block)
    {
        var text = ToSearch(block.NormalizedText);
        return text.Contains("CONG TY CO PHAN NONG SAN PHU GIA", StringComparison.Ordinal) ||
            text.StartsWith("THANH HOA", StringComparison.Ordinal) ||
            text.StartsWith("QUYET DINH", StringComparison.Ordinal) ||
            text.StartsWith("DIEU ", StringComparison.Ordinal) ||
            IsContextualSnapshotText(text) ||
            text.Contains("PHAM CHANH HA", StringComparison.Ordinal);
    }

    private static BlockEvidence ToSnapshotEvidence(DocumentBlock block)
    {
        var text = ToSearch(block.NormalizedText);
        var role = IsContextualSnapshotText(text) ? EvidenceRole.Contextual : EvidenceRole.Primary;

        return BlockEvidence.FromBlock(block, role, "snapshot", semanticRole: ResolveSnapshotSemanticRole(text));
    }

    private static bool IsContextualSnapshotText(string text) =>
        text.Contains("NOI NHAN", StringComparison.Ordinal) ||
        text.Contains("LUU VT", StringComparison.Ordinal) ||
        text.Contains("CONGTYCONNS", StringComparison.Ordinal);

    private static string ResolveSnapshotSemanticRole(string text)
    {
        if (text.StartsWith("TONG GIAM DOC", StringComparison.Ordinal))
        {
            return "body.authority";
        }

        if (text.Contains("CONG TY CO PHAN NONG SAN PHU GIA", StringComparison.Ordinal))
        {
            return "issuer.name";
        }

        if (text.Contains("THANH HOA", StringComparison.Ordinal))
        {
            return "date.issue-header";
        }

        if (text.StartsWith("QUYET DINH", StringComparison.Ordinal))
        {
            return "title.decision";
        }

        if (text.StartsWith("DIEU ", StringComparison.Ordinal))
        {
            return "body.section";
        }

        if (text.Contains("PHAM CHANH HA", StringComparison.Ordinal))
        {
            return "signer.name";
        }

        if (text.Contains("NOI NHAN", StringComparison.Ordinal))
        {
            return "recipient.archive";
        }

        if (text.Contains("LUU VT", StringComparison.Ordinal))
        {
            return "archive.reference";
        }

        return "stamp.noise";
    }

    private static ReviewEvidenceRegion CreateReviewEvidence(
        int pageIndex,
        double x,
        double y,
        string text,
        string semanticRole,
        string blockType = "paragraph") =>
        new(
            pageIndex,
            new BoundingBox(x, y, x + 100, y + 20),
            text,
            blockType,
            0,
            true,
            EvidenceRole.Primary,
            new EvidenceProvenance("test", string.Empty, text, text, semanticRole));

    private static ReviewEvidenceRegion WithRole(ReviewEvidenceRegion evidence, EvidenceRole role) =>
        evidence with { Role = role, IsPrimary = role == EvidenceRole.Primary };

    private static ReviewField CreateReviewField(string name, string value, IReadOnlyList<ReviewEvidenceRegion> evidence)
    {
        var primary = ReviewEvidenceResolver.ResolvePrimary(evidence, requireDrawable: true) ?? evidence.First();
        return new ReviewField(
            name,
            value,
            0.9,
            ReviewStatus.Accepted,
            Array.Empty<string>(),
            primary.SourceText,
            primary.BlockType,
            primary.BoundingBox,
            primary.PageIndex,
            evidence,
            Array.Empty<ReviewCandidate>());
    }

    private static ReviewOverlayRegion CreateOverlayRegion(
        string id,
        string groupId,
        string fieldName,
        string sourceText,
        EvidenceRole role,
        string semanticRole,
        int pageIndex,
        double x,
        double y) =>
        new(
            id,
            fieldName,
            pageIndex,
            new BoundingBox(x, y, x + 100, y + 20),
            0.9,
            string.Empty,
            sourceText,
            "paragraph",
            OverlayHighlightType.Accepted,
            groupId,
            role == EvidenceRole.Primary,
            role,
            new EvidenceProvenance("test", string.Empty, sourceText, sourceText, semanticRole));

    private static ReviewOverlayRegion ActiveRegion(IReadOnlyList<ReviewPageOverlay> pages, ReviewOverlayNavigator navigator) =>
        pages.SelectMany(page => page.Regions).Single(region => region.Id == navigator.ActiveRegionId);

    private static ReviewEvidenceRegion[] CreateLargeStressEvidence(int pageCount, int sectionsPerPage, int contextualPerSection)
    {
        var evidence = new List<ReviewEvidenceRegion>(pageCount * sectionsPerPage * (contextualPerSection + 1) + pageCount * 3);
        for (var page = 0; page < pageCount; page++)
        {
            evidence.Add(CreateReviewEvidence(page, 80, 40, "UBND TINH THANH HOA", "issuer.name", "page_header"));
            evidence.Add(CreateReviewEvidence(page, 90, 1260, "Noi nhan: Luu VT", "archive.reference", "page_footer"));
            for (var section = 0; section < sectionsPerPage; section++)
            {
                var number = (page * sectionsPerPage) + section + 1;
                evidence.Add(CreateReviewEvidence(page, 120, 220 + (section * 90), $"Dieu {number}. Noi dung chinh", "body.section"));
                for (var context = 0; context < contextualPerSection; context++)
                {
                    evidence.Add(WithRole(
                        CreateReviewEvidence(page, 140 + (context * 8), 252 + (section * 90) + (context * 18), $"Khoan {context + 1}. Noi dung bo sung Dieu {number}", "body.section-paragraph"),
                        EvidenceRole.Contextual));
                }
            }

            evidence.Add(CreateReviewEvidence(page, 682, 1120, $"PHAM CHANH HA {page:00}", "signer.name"));
            evidence.Add(WithRole(CreateReviewEvidence(page, 707, 1096, "CHU TICH HDQT", "signer.role"), EvidenceRole.Supporting));
        }

        return evidence
            .OrderByDescending(item => item.PageIndex)
            .ThenByDescending(item => item.BoundingBox?.Y1 ?? 0)
            .ToArray();
    }

    private static ReviewField[] CreateStressFields(IReadOnlyList<ReviewEvidenceRegion> evidence) =>
        evidence
            .GroupBy(item => ResolveStressFieldName(item), StringComparer.Ordinal)
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .Select(group => CreateReviewField(group.Key, group.First().SourceText, group.ToArray()))
            .ToArray();

    private static string ResolveStressFieldName(ReviewEvidenceRegion evidence)
    {
        var role = evidence.Provenance?.SemanticRole ?? string.Empty;
        if (role.StartsWith("issuer.", StringComparison.Ordinal))
        {
            return "Issuer";
        }

        if (role.StartsWith("archive.", StringComparison.Ordinal))
        {
            return $"Archive {evidence.PageIndex + 1}";
        }

        if (role.StartsWith("signer.", StringComparison.Ordinal))
        {
            return $"Signer {evidence.PageIndex + 1}";
        }

        var text = evidence.SourceText;
        if (text.StartsWith("Dieu ", StringComparison.OrdinalIgnoreCase))
        {
            var dotIndex = text.IndexOf('.', StringComparison.Ordinal);
            return dotIndex > 0 ? text[..dotIndex] : text;
        }

        if (text.Contains("Dieu ", StringComparison.OrdinalIgnoreCase))
        {
            var index = text.LastIndexOf("Dieu ", StringComparison.OrdinalIgnoreCase);
            return text[index..];
        }

        return $"Context {evidence.PageIndex + 1}";
    }

    private static string[] TraverseSignature(IReadOnlyList<ReviewPageOverlay> pages)
    {
        var navigator = new ReviewOverlayNavigator(pages);
        var ids = new List<string>();
        while (navigator.NavigateNextRegion())
        {
            ids.Add(navigator.ActiveRegionId!);
        }

        return ids.ToArray();
    }

    private static DocumentPageMetrics[] MultiPageMetrics() =>
        new[]
        {
            new DocumentPageMetrics(0, 1_000, 1_400),
            new DocumentPageMetrics(1, 1_000, 1_400)
        };

    private static DocumentPageMetrics[] StressMetrics(int pageCount) =>
        Enumerable.Range(0, pageCount)
            .Select(page => new DocumentPageMetrics(page, 1_000, 1_400))
            .ToArray();

    private static ReviewCandidate CreateCandidate(string fieldName, string value, string sourceText, BoundingBox box) =>
        new(
            fieldName,
            value,
            0,
            ReviewStatus.Rejected,
            Array.Empty<string>(),
            new[] { "test rejection" },
            sourceText,
            "paragraph",
            box,
            0,
            new[]
            {
                new ReviewEvidenceRegion(
                    0,
                    box,
                    sourceText,
                    "paragraph",
                    0,
                    true,
                    EvidenceRole.Primary,
                    new EvidenceProvenance("test", "test rejection", sourceText, sourceText, "date.rejected-dob-context"))
            });

    private static string[] ReviewSignature(ReviewDocumentExtraction review) =>
        review.Fields.Select(field =>
            $"{field.Name}|{field.Status}|{ToSearch(field.Value)}|{string.Join(";", field.EvidenceRegions.Select(EvidenceSignature))}")
            .ToArray();

    private static string ReasoningSignature(ReviewDocumentExtraction review, string fieldName) =>
        string.Join(">", review.AcceptedCandidates.Single(candidate => candidate.FieldName == fieldName).ReasoningChain!
            .Select(step => $"{step.Category}:{ToSearch(step.Text)}"));

    private static string OverlayFocusSignature(ReviewDocumentExtraction review, string fieldName)
    {
        var overlay = review.PageOverlays.SelectMany(page => page.Regions).Single(region => region.FieldName == fieldName && region.IsPrimary);
        return $"{overlay.Role}|{overlay.Provenance?.SemanticRole}|{Format(overlay.BoundingBox)}";
    }

    private static string[] DiagnosticSignature(ReviewDocumentExtraction review) =>
        review.Fields
            .SelectMany(field => field.EvidenceRegions)
            .Select(evidence => $"{EvidenceSignature(evidence)}|{string.Join(",", evidence.Provenance?.Diagnostics.Select(item => item.Code) ?? Array.Empty<string>())}")
            .ToArray();

    private static IReadOnlyList<ReviewEvidenceRegion> ReviewEvidence(ReviewDocumentExtraction review) =>
        review.Fields
            .SelectMany(field => field.EvidenceRegions)
            .Concat(review.RejectedCandidates.SelectMany(candidate => candidate.EvidenceRegions))
            .ToArray();

    private static string ReplaySignature(ReviewReplayResult replay) =>
        $"{replay.Succeeded}|{replay.Focus?.FieldName}|{replay.Focus?.EvidenceKey}|{string.Join(";", replay.Steps.Select(step => $"{step.Code}:{step.Subject}:{step.Reason}:{step.Before}:{step.After}"))}";

    private static string[] AuditSignature(ReviewAuditSnapshot snapshot) =>
        snapshot.ResolverOrdering
            .Concat(snapshot.SemanticGrouping)
            .Concat(snapshot.OverlayTraversal)
            .Concat(snapshot.FocusRestoration)
            .Concat(snapshot.ProvenanceDiagnostics)
            .ToArray();

    private static string[] SemanticDiffSignature(ReviewSemanticDiff diff) =>
        diff.OrderingDeltas
            .Concat(diff.PrimaryEvidenceDeltas)
            .Concat(diff.SemanticGroupDeltas)
            .Concat(diff.FocusRestorationDeltas)
            .ToArray();

    private static string[] RuntimeDiagnosticCodes(ReviewRuntimeCoordinator coordinator) =>
        coordinator.Diagnostics.Select(diagnostic => diagnostic.Code).ToArray();

    private static void AssertNoRuntimeValues(IEnumerable<string> lines)
    {
        foreach (var line in lines)
        {
            Assert.DoesNotContain(DateTime.UtcNow.Year.ToString(), line, StringComparison.Ordinal);
            Assert.DoesNotContain("System.", line, StringComparison.Ordinal);
            Assert.DoesNotContain("DocumentManagement.", line, StringComparison.Ordinal);
            Assert.DoesNotContain("@", line, StringComparison.Ordinal);
        }
    }

    private static string[] RuntimeDiagnosticScenario()
    {
        using var coordinator = new ReviewRuntimeCoordinator();
        using var first = coordinator.BeginRefresh();
        using var second = coordinator.BeginRefresh();

        _ = coordinator.RejectIfStale(first, "stale-refresh-rejected");
        coordinator.CancelRefresh();
        _ = coordinator.Complete(second, canceled: true);

        return coordinator.Diagnostics
            .Select(diagnostic => $"{diagnostic.Code}|{diagnostic.Version}|{diagnostic.Reason}")
            .ToArray();
    }

    private static ReviewOperationalSnapshot CreateOperationalSnapshotScenario(ReviewDocumentExtraction review)
    {
        using var coordinator = new ReviewRuntimeCoordinator();
        using var first = coordinator.BeginRefresh();
        using var second = coordinator.BeginRefresh();
        _ = coordinator.RejectIfStale(first, "stale-refresh-rejected");
        var signer = review.PageOverlays.SelectMany(page => page.Regions).Single(region => region.FieldName == "Signer" && region.IsPrimary);
        var replay = new ReviewOverlayNavigator(review.PageOverlays).NavigateToRegionWithReplay(signer.Id);
        var checks = ReviewOperationalObservability.VerifyConsistency(
            ReviewEvidence(review),
            review.PageOverlays,
            replay);

        return ReviewOperationalObservability.CreateSnapshot(
            coordinator.CreateSnapshot(),
            review.PageOverlays,
            replay.Steps,
            checks);
    }

    private static string[] OperationalSnapshotSignature(ReviewOperationalSnapshot snapshot) =>
        snapshot.RuntimeRefreshState
            .Concat(snapshot.OverlayTraversalState)
            .Concat(snapshot.FocusRestorationChain)
            .Concat(snapshot.ReplayInvalidationChain)
            .Concat(snapshot.SemanticConsistencyResults)
            .ToArray();

    private static string[] RuntimeObservationScenarioSignature()
    {
        var review = LoadSampleReview();
        using var coordinator = new ReviewRuntimeCoordinator();
        using var first = coordinator.BeginRefresh();
        using var second = coordinator.BeginRefresh();
        _ = coordinator.RejectIfStale(first, "stale-refresh-rejected");
        coordinator.CancelRefresh();
        _ = coordinator.Complete(second, canceled: true);
        var checks = ReviewOperationalObservability.VerifyConsistency(ReviewEvidence(review), review.PageOverlays);
        var snapshot = ReviewOperationalObservability.CreateSnapshot(
            coordinator.CreateSnapshot(),
            review.PageOverlays,
            consistencyChecks: checks);

        return OperationalSnapshotSignature(snapshot);
    }

    private static string EvidenceSignature(BlockEvidence evidence) =>
        $"{evidence.Role}|{evidence.SemanticRole}|{ToSearch(evidence.NormalizedText)}|{Format(evidence.BoundingBox)}";

    private static string EvidenceSignature(ReviewEvidenceRegion evidence) =>
        $"{evidence.Role}|{evidence.Provenance?.SemanticRole}|{ToSearch(evidence.SourceText)}|{Format(evidence.BoundingBox)}";

    private static int ParseDieuNumber(string text)
    {
        var normalized = ToSearch(text);
        Assert.StartsWith("DIEU ", normalized, StringComparison.Ordinal);
        return int.Parse(normalized.AsSpan(5, 1));
    }

    private static ReviewDocumentExtraction LoadSampleReview() =>
        new IntelligenceReviewService().LoadMinerUExtraction(File.ReadAllText(SamplePath()));

    private static DocumentStructure LoadSampleDocument() =>
        new MinerUContentListV2Adapter(Normalizer).Read(File.ReadAllText(SamplePath()));

    private static string ToSearch(string? value) => Normalizer.ToSearchKey(value);

    private static string Format(BoundingBox? bbox) =>
        bbox is null ? "no-bbox" : $"{bbox.X1:0},{bbox.Y1:0},{bbox.X2:0},{bbox.Y2:0}";

    private static string SamplePath()
    {
        var current = AppContext.BaseDirectory;
        for (var i = 0; i < 6; i++)
        {
            var candidate = Path.GetFullPath(Path.Combine(current, "artifacts", "samples", "decision_content_list_v2.json"));
            if (File.Exists(candidate))
            {
                return candidate;
            }

            current = Path.GetFullPath(Path.Combine(current, ".."));
        }

        throw new FileNotFoundException("Could not find artifacts/samples/decision_content_list_v2.json.");
    }
}
