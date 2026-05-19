using DocumentManagement.Intelligence;
using DocumentManagement.Intelligence.Adapters;
using DocumentManagement.Intelligence.Confidence;
using DocumentManagement.Intelligence.Extractors;
using DocumentManagement.Intelligence.Models;
using DocumentManagement.Intelligence.Normalization;
using DocumentManagement.Intelligence.Review;
using DocumentManagement.Intelligence.Results;
using Xunit;

namespace DocumentManagement.Tests.Intelligence;

public sealed class IntelligencePipelineTests
{
    private static readonly VietnameseTextNormalizer Normalizer = new();

    [Fact]
    public void MinerUContentListV2Adapter_PreservesBlocksAndBoundingBoxesWithoutMergingHeaders()
    {
        var document = LoadSampleDocument();

        var headers = document.Blocks.Where(block => block.BlockType == "page_header").ToArray();

        Assert.Equal(3, headers.Length);
        Assert.Contains(headers, block => block.NormalizedText.Contains("CONG TY CO PHAN NONG SAN PHU GIA", StringComparison.Ordinal));
        Assert.Contains(headers, block => block.NormalizedText.Contains("CONG HOA XA HOI CHU NGHIA VIET NAM", StringComparison.Ordinal));
        Assert.Contains(headers, block => Normalizer.ToSearchKey(block.NormalizedText).Contains("THANH HOA", StringComparison.Ordinal));
        Assert.All(headers, block => Assert.True(block.BoundingBox.Width > 0));
        Assert.DoesNotContain(document.Blocks, block =>
            block.NormalizedText.Contains("CONG TY CO PHAN NONG SAN PHU GIA", StringComparison.Ordinal) &&
            block.NormalizedText.Contains("CONG HOA XA HOI CHU NGHIA VIET NAM", StringComparison.Ordinal));
    }

    [Fact]
    public void MinerUContentListV2Adapter_ReadsOptionalMetricsLinesSpansAndMetadata()
    {
        var document = ReadSyntheticDocument("""
        [
          {
            "width": 1200,
            "height": 1600,
            "blocks": [
              {
                "type":"paragraph",
                "content":{"paragraph_content":[{"type":"text","content":"Dieu 1. Noi dung."}]},
                "bbox":[100,200,700,240],
                "lines":[{"text":"Dieu 1. Noi dung.","bbox":[100,200,700,240]}],
                "spans":[{"text":"Dieu","bbox":[100,200,150,220],"confidence":0.92}],
                "metadata":{"region_type":"text"}
              }
            ]
          }
        ]
        """);

        var block = Assert.Single(document.Blocks);
        var page = Assert.Single(document.Pages);

        Assert.Equal(1200, page.Width);
        Assert.Equal(1600, page.Height);
        Assert.NotNull(block.Lines);
        Assert.NotNull(block.Spans);
        Assert.Equal("text", block.NonTextMetadata?["region_type"]);
    }

    [Fact]
    public void VietnameseTextNormalizer_AppliesDictionaryCorrectionsAndSearchKeys()
    {
        var normalizer = new VietnameseTextNormalizer();
        normalizer.AddDictionaryEntry("PHUGIA", "PHU GIA");

        var normalized = normalizer.Normalize("ToNG GIAm DoC CONGTY PHUGIA ngay 01 thÃ¡ng 10 nÇŽm 2025");
        var searchKey = normalizer.ToSearchKey(normalized);

        Assert.Contains("TONG GIAM", normalized, StringComparison.Ordinal);
        Assert.Contains("PHU GIA", normalized, StringComparison.Ordinal);
        Assert.Contains("NGAY 01 THANG 10 NAM 2025", searchKey, StringComparison.Ordinal);
    }

    [Fact]
    public void IssueDateExtractor_RejectsDobDateAndDoesNotAcceptNoisyHeaderDate()
    {
        var result = RunSamplePipeline();

        Assert.False(result.IssueDate.HasValue);

        var rejectedDob = Assert.Single(result.IssueDate.Candidates, candidate => candidate.CandidateText == "01/10/1979");
        Assert.Equal(0, rejectedDob.Confidence.Value);
        Assert.Contains(rejectedDob.RejectReasons, reason => reason.Code == "date.dob-context" && reason.IsHardReject);

        var noisyHeaderDate = Assert.Single(result.IssueDate.Candidates, candidate => candidate.CandidateText == "LLF/9/2025");
        Assert.Equal(0, noisyHeaderDate.Confidence.Value);
        Assert.Contains(noisyHeaderDate.RejectReasons, reason => reason.Code == "date.uncertain-ocr");
    }

    [Fact]
    public void IssueDateExtractor_AcceptsCleanTopRightIssueDate()
    {
        var document = ReadSyntheticDocument("""
        [[
          {"type":"page_header","content":{"page_header_content":[{"type":"text","content":"UBND TINH THANH HOA So: 12/QD-UBND"}]},"bbox":[80,50,360,120]},
          {"type":"page_header","content":{"page_header_content":[{"type":"text","content":"Thanh Hoa, ngay 14 thang 9 nam 2025"}]},"bbox":[560,90,910,125]},
          {"type":"paragraph","content":{"paragraph_content":[{"type":"text","content":"Dieu 1. Thi hanh quyet dinh."}]},"bbox":[120,320,820,360]}
        ]]
        """);

        var field = new IssueDateExtractor(Normalizer).Extract(document);

        Assert.True(field.HasValue);
        Assert.Equal(new DateOnly(2025, 9, 14), field.Value);
        Assert.True(field.Confidence >= 0.65);
        Assert.Empty(field.RejectReasons);
    }

    [Fact]
    public void IssueDateExtractor_HardRejectsDobInsideBodyEvenWhenDateIsClean()
    {
        var document = ReadSyntheticDocument("""
        [[
          {"type":"paragraph","content":{"paragraph_content":[{"type":"text","content":"Dieu 1. Ong Nguyen Van A, sinh ngay 01 thang 10 nam 1979 duoc bo nhiem."}]},"bbox":[120,320,820,360]}
        ]]
        """);

        var field = new IssueDateExtractor(Normalizer).Extract(document);

        Assert.False(field.HasValue);
        var rejected = Assert.Single(field.Candidates);
        Assert.Equal("01/10/1979", rejected.CandidateText);
        Assert.Equal(0, rejected.Confidence.Value);
        Assert.Contains(rejected.RejectReasons, reason => reason.Code == "date.dob-context" && reason.IsHardReject);
    }

    [Fact]
    public void IssuerExtractor_ExtractsIssuerFromTopLeftPageHeader()
    {
        var field = new IssuerExtractor(Normalizer).Extract(LoadSampleDocument());

        Assert.True(field.HasValue);
        Assert.Equal("CONG TY CO PHAN NONG SAN PHU GIA", field.Value);
        Assert.Contains(field.Evidence, evidence => evidence.BlockType == "page_header" && evidence.BoundingBox.X1 < 200);
    }

    [Fact]
    public void IssuerExtractor_ReturnsEmptyWhenOrganizationKeywordIsMissing()
    {
        var document = ReadSyntheticDocument("""
        [[
          {"type":"page_header","content":{"page_header_content":[{"type":"text","content":"PHONG HANH CHINH So: 12/QD-HC"}]},"bbox":[80,50,360,120]}
        ]]
        """);

        var field = new IssuerExtractor(Normalizer).Extract(document);

        Assert.False(field.HasValue);
        Assert.Equal(0, field.Confidence);
    }

    [Fact]
    public void DocumentNumberExtractor_ExtractsNumberFromTopLeftHeader()
    {
        var field = new DocumentNumberExtractor(Normalizer).Extract(LoadSampleDocument());

        Assert.True(field.HasValue);
        Assert.Equal("3AS./QD-PG", field.Value);
        Assert.Contains(field.Evidence, evidence => evidence.BlockType == "page_header");
    }

    [Fact]
    public void DocumentNumberExtractor_NormalizesVietnameseDecisionNumbersWithWhitespace()
    {
        var document = ReadSyntheticDocument("""
        [{
          "width":1000,
          "height":1400,
          "blocks":[
            {"type":"page_header","content":{"page_header_content":[{"type":"text","content":"UBND TINH THANH HOA Số: 12 / QĐ - UBND"}]},"bbox":[80,50,390,120]}
          ]
        }]
        """);

        var field = new DocumentNumberExtractor(Normalizer).Extract(document);

        Assert.True(field.HasValue);
        Assert.Equal("12/QD-UBND", field.Value);
    }

    [Fact]
    public void TitleExtractor_PromotesRealDecisionTitleWithProvenance()
    {
        var result = RunSamplePipeline();

        Assert.True(result.Title.HasValue);
        Assert.Contains("QUYET DINH", result.Title.Value, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(result.Title.Evidence, evidence => evidence.Provenance.SemanticRole == "title.document");
    }

    [Fact]
    public void RecipientExtractor_PromotesFooterRecipientsInReadingOrder()
    {
        var result = RunSamplePipeline();

        Assert.True(result.Recipients.HasValue);
        Assert.Contains("NOI NHAN", Normalizer.ToSearchKey(result.Recipients.Value), StringComparison.Ordinal);
        Assert.Contains("LUU VT", Normalizer.ToSearchKey(result.Recipients.Value), StringComparison.Ordinal);
        Assert.Equal(
            new[] { "recipient.heading", "recipient.entry", "recipient.entry" },
            result.Recipients.Evidence.Select(evidence => evidence.Provenance.SemanticRole).ToArray());
    }

    [Fact]
    public void SignerExtractor_DetectsRoleAndNameBelowRoleWithOcrQualityPenalty()
    {
        var field = new SignerExtractor(Normalizer).Extract(LoadSampleDocument());

        Assert.True(field.HasValue);
        Assert.Equal("Pham Chanh Ha", field.Value);
        Assert.InRange(field.Confidence, 0.6, 0.95);
        Assert.Contains(field.Evidence, evidence => Normalizer.ToSearchKey(evidence.NormalizedText).Contains("CHU TICH HDQT", StringComparison.Ordinal));
        var accepted = Assert.Single(field.Candidates.Where(candidate => candidate.DisplayValue == "Pham Chanh Ha"));
        Assert.True(accepted.Confidence.Factors["ocrQuality"] < 0.18);
    }

    [Fact]
    public void SignerExtractor_DetectsExpandedRolesWithStampOverlapNearby()
    {
        var document = ReadSyntheticDocument("""
        [{
          "width":1000,
          "height":1400,
          "blocks":[
            {"type":"paragraph","content":{"paragraph_content":[{"type":"text","content":"CONGTY"}]},"bbox":[610,1005,705,1025]},
            {"type":"paragraph","content":{"paragraph_content":[{"type":"text","content":"KT. GIAM DOC"}]},"bbox":[700,1030,860,1055]},
            {"type":"paragraph","content":{"paragraph_content":[{"type":"text","content":"PHO GIAM DOC"}]},"bbox":[700,1056,860,1078]},
            {"type":"paragraph","content":{"paragraph_content":[{"type":"text","content":"Tran Van Binh"}]},"bbox":[675,1084,900,1114]}
          ]
        }]
        """);

        var field = new SignerExtractor(Normalizer).Extract(document);

        Assert.True(field.HasValue);
        Assert.Equal("Tran Van Binh", field.Value);
        Assert.Contains(field.Evidence, evidence => evidence.Provenance.SemanticRole == "signer.role");
        Assert.DoesNotContain(field.Evidence, evidence => evidence.NormalizedText == "CONGTY");
    }

    [Fact]
    public void SignerExtractor_ReturnsEmptyWhenRoleHasNoNearbyName()
    {
        var document = ReadSyntheticDocument("""
        [[
          {"type":"paragraph","content":{"paragraph_content":[{"type":"text","content":"CHU TICH HDQT"}]},"bbox":[700,660,870,690]},
          {"type":"paragraph","content":{"paragraph_content":[{"type":"text","content":"Noi nhan:"}]},"bbox":[110,580,210,600]}
        ]]
        """);

        var field = new SignerExtractor(Normalizer).Extract(document);

        Assert.False(field.HasValue);
        Assert.Equal(0, field.Confidence);
        var rejected = Assert.Single(field.Candidates);
        Assert.Equal(0, rejected.Confidence.Value);
        Assert.Contains(rejected.RejectReasons, reason => reason.Code == "signer.missing-name");
    }

    [Fact]
    public void BodyExtractor_ReturnsArticlesOneThroughFourInReadingOrder()
    {
        var sections = new BodyExtractor(Normalizer).Extract(LoadSampleDocument());

        Assert.Equal(new[] { 1, 2, 3, 4 }, sections.Select(section => section.Number).ToArray());
        Assert.All(sections, section => Assert.StartsWith($"DIEU {section.Number}", Normalizer.ToSearchKey(section.Text), StringComparison.Ordinal));
    }

    [Fact]
    public void ConfidenceScorer_UsesRegionBlockTypeKeywordOcrQualityAndSemanticConsistency()
    {
        var document = ReadSyntheticDocument("""
        [[
          {"type":"page_header","content":{"page_header_content":[{"type":"text","content":"CONG TY CO PHAN PHU GIA So: 12/QD-PG"}]},"bbox":[80,50,360,120]},
          {"type":"paragraph","content":{"paragraph_content":[{"type":"text","content":"CONG TY CO PHAN PHU GIA"}]},"bbox":[640,500,900,530]}
        ]]
        """);
        var scorer = new ConfidenceScorer(Normalizer);

        var headerScore = scorer.Score(document.Blocks[0], document, "top-left", new[] { "CONG TY" }, 0.22, "page_header");
        var misplacedScore = scorer.Score(document.Blocks[1], document, "top-left", new[] { "CONG TY" }, 0.22, "page_header");

        Assert.True(headerScore.Value > misplacedScore.Value);
        Assert.True(headerScore.Factors["layoutRegion"] > misplacedScore.Factors["layoutRegion"]);
        Assert.True(headerScore.Factors.ContainsKey("ocrQuality"));
        Assert.Contains(headerScore.Reasoning, reason => reason.Contains("semantic consistency", StringComparison.Ordinal));
    }

    [Fact]
    public void ExtractionDebugReport_ListsAcceptedAndRejectedCandidatesWithReasons()
    {
        var result = RunSamplePipeline();
        var report = Assert.IsType<ExtractionDebugReport>(result.DebugReport);

        Assert.Contains(report.AcceptedCandidates, candidate => candidate.FieldName == "Issuer" && candidate.DisplayValue == "CONG TY CO PHAN NONG SAN PHU GIA");
        Assert.Contains(report.AcceptedCandidates, candidate => candidate.FieldName == "Signer" && candidate.DisplayValue == "Pham Chanh Ha");
        Assert.Contains(report.AcceptedCandidates, candidate => candidate.FieldName == "Title" && candidate.DisplayValue.Contains("QUYET DINH", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(report.AcceptedCandidates, candidate => candidate.FieldName == "Recipients" && Normalizer.ToSearchKey(candidate.DisplayValue).Contains("NOI NHAN", StringComparison.Ordinal));

        var rejectedDob = Assert.Single(report.RejectedCandidates, candidate => candidate.CandidateText == "01/10/1979");
        Assert.Equal(0, rejectedDob.Confidence.Value);
        Assert.Contains(rejectedDob.RejectReasons, reason => reason.Message == "contains DOB context");

        var consoleText = report.ToConsoleText();
        Assert.Contains("=== ACCEPTED ===", consoleText, StringComparison.Ordinal);
        Assert.Contains("=== REJECTED ===", consoleText, StringComparison.Ordinal);
        Assert.Contains("01/10/1979", consoleText, StringComparison.Ordinal);
        Assert.Contains("contains DOB context", consoleText, StringComparison.Ordinal);
    }

    [Fact]
    public void IntelligenceReviewService_MapsExtractionIntoHumanReviewDtosWithoutAutoAcceptingRejectedDates()
    {
        var json = File.ReadAllText(SamplePath());
        var review = new IntelligenceReviewService().LoadMinerUExtraction(json);

        var issuer = Assert.Single(review.Fields, field => field.Name == "Issuer");
        Assert.Equal("CONG TY CO PHAN NONG SAN PHU GIA", issuer.Value);
        Assert.Equal(ReviewStatus.Accepted, issuer.Status);
        Assert.True(issuer.Confidence >= 0.85);
        Assert.NotNull(issuer.SourceBoundingBox);
        Assert.Equal(0, issuer.PageIndex);

        var issueDate = Assert.Single(review.Fields, field => field.Name == "Issue date");
        Assert.Equal(string.Empty, issueDate.Value);
        Assert.Equal(ReviewStatus.Rejected, issueDate.Status);

        var rejectedDob = Assert.Single(review.RejectedCandidates, candidate => candidate.Value == "01/10/1979");
        Assert.Equal(ReviewStatus.Rejected, rejectedDob.Status);
        Assert.Equal(0, rejectedDob.Confidence);
        Assert.Contains("contains DOB context", rejectedDob.RejectReasons);

        Assert.Contains("=== ACCEPTED ===", review.DebugReport, StringComparison.Ordinal);
        Assert.Contains("=== REJECTED ===", review.DebugReport, StringComparison.Ordinal);

        var title = Assert.Single(review.Fields, field => field.Name == "Title");
        Assert.Equal(ReviewStatus.Accepted, title.Status);
        Assert.Contains(title.EvidenceRegions, evidence => evidence.Provenance?.SemanticRole == "title.document");

        var recipients = Assert.Single(review.Fields, field => field.Name == "Recipients");
        Assert.Equal(ReviewStatus.Accepted, recipients.Status);
        Assert.Contains(recipients.EvidenceRegions, evidence => evidence.Provenance?.SemanticRole == "recipient.entry");
    }

    [Fact]
    public void IntelligenceReviewService_CreatesPageOverlaysFromReviewMetadata()
    {
        var review = new IntelligenceReviewService().LoadMinerUExtraction(File.ReadAllText(SamplePath()));

        var issuer = Assert.Single(review.Fields, field => field.Name == "Issuer");
        var issuerOverlay = review.PageOverlays
            .SelectMany(page => page.Regions)
            .Single(region => region.FieldName == "Issuer" && region.HighlightType == OverlayHighlightType.Accepted);

        Assert.Equal(issuer.PageIndex, issuerOverlay.PageIndex);
        Assert.Equal(issuer.SourceBoundingBox, issuerOverlay.BoundingBox);
        Assert.Equal(issuer.Confidence, issuerOverlay.Confidence);
        Assert.Equal(issuer.SourceText, issuerOverlay.SourceText);
        Assert.Equal(issuer.SourceBlockType, issuerOverlay.BlockType);
        Assert.True(review.PageOverlays.All(page => page.SourceWidth > 0 && page.SourceHeight > 0));
    }

    [Fact]
    public void IntelligenceReviewService_PreservesMultiRegionSignerEvidence()
    {
        var review = new IntelligenceReviewService().LoadMinerUExtraction(File.ReadAllText(SamplePath()));

        var signer = Assert.Single(review.Fields, field => field.Name == "Signer");

        Assert.Equal(2, signer.EvidenceRegions.Count);
        Assert.Contains(signer.EvidenceRegions, evidence => evidence.SourceText.Contains("CHU TICH HDQT", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(signer.EvidenceRegions, evidence => evidence.SourceText.Contains("Pham Chanh Ha", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(signer.SourceBoundingBox, signer.EvidenceRegions[0].BoundingBox);
        Assert.True(signer.EvidenceRegions[0].IsPrimary);
        Assert.Equal(EvidenceRole.Primary, signer.EvidenceRegions[0].Role);
        Assert.Contains("Pham Chanh Ha", signer.EvidenceRegions[0].SourceText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(signer.EvidenceRegions, evidence => evidence.Role == EvidenceRole.Supporting && evidence.Provenance?.SemanticRole == "signer.role");
    }

    [Fact]
    public void IntelligenceReviewService_MapsEvidenceProvenanceForReviewerDisplay()
    {
        var review = new IntelligenceReviewService().LoadMinerUExtraction(File.ReadAllText(SamplePath()));

        var signer = Assert.Single(review.Fields, field => field.Name == "Signer");
        var primary = Assert.Single(signer.EvidenceRegions, evidence => evidence.Role == EvidenceRole.Primary);
        var supporting = Assert.Single(signer.EvidenceRegions, evidence => evidence.Role == EvidenceRole.Supporting);

        Assert.Contains("Pham Chanh Ha", primary.Provenance?.RawText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Pham Chanh Ha", primary.Provenance?.NormalizedText, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("SignerExtractor.name-below-role", primary.Provenance?.ExtractionSource);
        Assert.Equal("signer.name", primary.Provenance?.SemanticRole);
        Assert.Equal("SignerExtractor.role-keyword", supporting.Provenance?.ExtractionSource);
        Assert.Equal("signer.role", supporting.Provenance?.SemanticRole);
    }

    [Fact]
    public void ReviewEvidenceProvenanceFormatter_PreservesMissingBboxAndRawOcr()
    {
        var evidence = new ReviewEvidenceRegion(
            0,
            null,
            "NGAY 01 THANG 10 NAM 1979",
            "paragraph",
            7,
            true,
            EvidenceRole.Primary,
            new EvidenceProvenance(
                "IssueDateExtractor.date-context",
                "date.dob-context",
                "Ong A, sinh ngay 01 thang 10 nam 1979",
                "ONG A, SINH NGAY 01 THANG 10 NAM 1979",
                "date.rejected-dob-context"));

        var text = ReviewEvidenceProvenanceFormatter.Format(evidence);

        Assert.Contains("Raw OCR: Ong A, sinh ngay 01 thang 10 nam 1979", text, StringComparison.Ordinal);
        Assert.Contains("Normalized: ONG A, SINH NGAY 01 THANG 10 NAM 1979", text, StringComparison.Ordinal);
        Assert.Contains("Extraction: IssueDateExtractor.date-context", text, StringComparison.Ordinal);
        Assert.Contains("Reject: date.dob-context", text, StringComparison.Ordinal);
        Assert.Contains("Region: No bbox; evidence is retained for provenance but has no drawable overlay region from MinerU.", text, StringComparison.Ordinal);
    }

    [Fact]
    public void IntelligenceReviewService_MapsReviewerReasoningChainForAcceptedCandidate()
    {
        var review = new IntelligenceReviewService().LoadMinerUExtraction(File.ReadAllText(SamplePath()));

        var signer = Assert.Single(review.AcceptedCandidates, candidate => candidate.FieldName == "Signer");
        Assert.NotNull(signer.ReasoningChain);
        var chain = signer.ReasoningChain!;

        Assert.Contains(chain, step => step.Category == "Accepted evidence" && step.Text.Contains("Pham Chanh Ha", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(chain, step => step.Category == "Supporting evidence" && step.Text.Contains("CHU TICH HDQT", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(chain, step => step.Category == "Scoring reason" && step.Text.Contains("semantic consistency", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(chain, step => step.Category == "Final decision" && step.Text.Contains("Accepted", StringComparison.Ordinal));
    }

    [Fact]
    public void IntelligenceReviewService_MapsConfidenceExplanationForReviewer()
    {
        var review = new IntelligenceReviewService().LoadMinerUExtraction(File.ReadAllText(SamplePath()));

        var signer = Assert.Single(review.AcceptedCandidates, candidate => candidate.FieldName == "Signer");
        Assert.NotNull(signer.ConfidenceBreakdown);
        var breakdown = signer.ConfidenceBreakdown!;

        Assert.Contains(breakdown, item => item.Name == "Semantic match" && item.Value > 0);
        Assert.Contains(breakdown, item => item.Name == "Layout" && item.Value > 0);
        Assert.Contains(breakdown, item => item.Name == "OCR degradation penalty");
        Assert.Contains(breakdown, item => item.Name == "Ambiguity penalty");
    }

    [Fact]
    public void IntelligenceReviewService_KeepsRejectedEvidenceVisibleInReasoning()
    {
        var review = new IntelligenceReviewService().LoadMinerUExtraction(File.ReadAllText(SamplePath()));

        var rejectedDob = Assert.Single(review.RejectedCandidates, candidate => candidate.Value == "01/10/1979");
        Assert.NotNull(rejectedDob.ReasoningChain);
        var chain = rejectedDob.ReasoningChain!;

        Assert.NotEmpty(rejectedDob.EvidenceRegions);
        Assert.Contains(rejectedDob.EvidenceRegions, evidence => evidence.Provenance?.RawText.Contains("sinh", StringComparison.OrdinalIgnoreCase) == true);
        Assert.Contains(chain, step => step.Category == "Rejected evidence" && step.Text.Contains("DOB", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(chain, step => step.Category == "Final decision" && step.Text.Contains("Rejected", StringComparison.Ordinal));
    }

    [Fact]
    public void ReviewCandidate_RemainsBackwardCompatibleWithoutExplanationMetadata()
    {
        var candidate = new ReviewCandidate(
            "Issuer",
            "UBND",
            0.9,
            ReviewStatus.Accepted,
            Array.Empty<string>(),
            Array.Empty<string>(),
            "UBND",
            "page_header",
            new BoundingBox(10, 10, 100, 40),
            0,
            Array.Empty<ReviewEvidenceRegion>());

        Assert.Null(candidate.ReasoningChain);
        Assert.Null(candidate.ConfidenceBreakdown);
    }

    [Fact]
    public void ReviewEvidenceResolver_SelectsCanonicalPrimaryByRoleAndVisualOrder()
    {
        var support = CreateEvidence("support", EvidenceRole.Supporting, 0, 100, 100, readingOrder: 1);
        var laterPrimary = CreateEvidence("later", EvidenceRole.Primary, 0, 240, 100, readingOrder: 2);
        var earlierPrimary = CreateEvidence("earlier", EvidenceRole.Primary, 0, 120, 100, readingOrder: 99);

        var primary = ReviewEvidenceResolver.ResolvePrimary(new[] { support, laterPrimary, earlierPrimary });

        Assert.Same(earlierPrimary, primary);
    }

    [Fact]
    public void ReviewEvidenceResolver_NormalizesVisualReadingOrderBeforeJsonOrder()
    {
        var secondVisual = CreateEvidence("second", EvidenceRole.Primary, 0, 300, 80, readingOrder: 1);
        var firstVisual = CreateEvidence("first", EvidenceRole.Primary, 0, 100, 80, readingOrder: 2);
        var sameLineLeft = CreateEvidence("left", EvidenceRole.Primary, 0, 200, 70, readingOrder: 99);
        var sameLineRight = CreateEvidence("right", EvidenceRole.Primary, 0, 200, 240, readingOrder: 1);

        var ordered = ReviewEvidenceResolver.OrderBlockEvidence(new[] { secondVisual, firstVisual, sameLineRight, sameLineLeft });

        Assert.Equal(new[] { "first", "left", "right", "second" }, ordered.Select(evidence => evidence.NormalizedText).ToArray());
    }

    [Fact]
    public void IntelligenceReviewService_ReasoningOrderingIsStableWhenEvidenceInputOrderChanges()
    {
        var first = new ExtractionCandidate<string>(
            "Signer",
            "Nguyen Van A",
            "role/name",
            ConfidenceScore.FromFactors(new Dictionary<string, double>
            {
                ["layoutRegion"] = 0.22,
                ["blockType"] = 0.12,
                ["keywordProximity"] = 0.22,
                ["ocrQuality"] = 0.18,
                ["semanticConsistency"] = 0.22
            }, "semantic consistency: 0.22"),
            Array.Empty<string>(),
            Array.Empty<RejectReason>(),
            new[]
            {
                CreateEvidence("GIAM DOC", EvidenceRole.Supporting, 0, 500, 700, 1),
                CreateEvidence("Nguyen Van A", EvidenceRole.Primary, 0, 540, 710, 2)
            });
        var second = first with { Evidence = first.Evidence.Reverse().ToArray() };

        var firstReview = IntelligenceReviewService.MapCandidateForReview(first);
        var secondReview = IntelligenceReviewService.MapCandidateForReview(second);

        Assert.Equal(
            firstReview.ReasoningChain!.Select(step => $"{step.Category}:{step.Text}").ToArray(),
            secondReview.ReasoningChain!.Select(step => $"{step.Category}:{step.Text}").ToArray());
    }

    [Fact]
    public void ReviewOverlayMapper_FocusesCanonicalPrimaryRegionForGroup()
    {
        var canonical = new BoundingBox(10, 100, 110, 130);
        var field = new ReviewField(
            "Signer",
            "Nguyen Van A",
            0.9,
            ReviewStatus.Accepted,
            Array.Empty<string>(),
            "Nguyen Van A",
            "paragraph",
            canonical,
            0,
            new[]
            {
                new ReviewEvidenceRegion(0, new BoundingBox(20, 160, 120, 190), "Later primary", "paragraph", 1, false, EvidenceRole.Primary),
                new ReviewEvidenceRegion(0, canonical, "Nguyen Van A", "paragraph", 99, true, EvidenceRole.Primary),
                new ReviewEvidenceRegion(0, new BoundingBox(10, 70, 110, 95), "GIAM DOC", "paragraph", 0, false, EvidenceRole.Supporting)
            },
            Array.Empty<ReviewCandidate>());

        var regions = Assert.Single(ReviewOverlayMapper.CreatePages(new[] { field }, Array.Empty<ReviewCandidate>(), Array.Empty<DocumentPageMetrics>())).Regions;

        Assert.Single(regions, region => region.IsPrimary);
        Assert.Equal(canonical, Assert.Single(regions, region => region.IsPrimary).BoundingBox);
        Assert.Contains(regions, region => region.Role == EvidenceRole.Supporting && !region.IsPrimary);
    }

    [Fact]
    public void ReviewOverlayMapper_SingleRegionEvidenceRemainsCanonicalPrimary()
    {
        var field = new ReviewField(
            "Issuer",
            "UBND",
            0.9,
            ReviewStatus.Accepted,
            Array.Empty<string>(),
            "UBND",
            "page_header",
            new BoundingBox(10, 10, 100, 40),
            0,
            Array.Empty<ReviewEvidenceRegion>(),
            Array.Empty<ReviewCandidate>());

        var region = Assert.Single(Assert.Single(ReviewOverlayMapper.CreatePages(new[] { field }, Array.Empty<ReviewCandidate>(), Array.Empty<DocumentPageMetrics>())).Regions);

        Assert.True(region.IsPrimary);
        Assert.Equal(EvidenceRole.Primary, region.Role);
    }

    [Fact]
    public void SampleReview_SignerIsolationKeepsNameAsCanonicalPrimary()
    {
        var review = new IntelligenceReviewService().LoadMinerUExtraction(File.ReadAllText(SamplePath()));

        var signer = Assert.Single(review.Fields, field => field.Name == "Signer");
        var primaryEvidence = ReviewEvidenceResolver.ResolvePrimary(signer.EvidenceRegions);
        var overlayRegion = review.PageOverlays
            .SelectMany(page => page.Regions)
            .Single(region => region.FieldName == "Signer" && region.IsPrimary);

        Assert.NotNull(primaryEvidence);
        Assert.Contains("Pham Chanh Ha", primaryEvidence!.SourceText, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(primaryEvidence.BoundingBox, overlayRegion.BoundingBox);
        Assert.DoesNotContain(signer.EvidenceRegions, evidence => evidence.SourceText.Contains("CONGTYCONNS", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void SampleReview_IssueDateIsolationKeepsHeaderAndDobRejectionsSeparate()
    {
        var review = new IntelligenceReviewService().LoadMinerUExtraction(File.ReadAllText(SamplePath()));

        var headerDate = Assert.Single(review.RejectedCandidates, candidate => candidate.Value == "LLF/9/2025");
        var dobDate = Assert.Single(review.RejectedCandidates, candidate => candidate.Value == "01/10/1979");

        Assert.Contains(headerDate.EvidenceRegions, evidence => evidence.Provenance?.SemanticRole == "document.issue-date");
        Assert.Contains(dobDate.EvidenceRegions, evidence => evidence.Provenance?.SemanticRole == "date.rejected-dob-context");
        Assert.False(Equals(headerDate.SourceBoundingBox, dobDate.SourceBoundingBox));
    }

    [Fact]
    public void SampleReview_BodySectionsRemainInLegalReadingOrder()
    {
        var review = new IntelligenceReviewService().LoadMinerUExtraction(File.ReadAllText(SamplePath()));

        var bodyFields = review.Fields
            .Where(field => field.Name.StartsWith("Dieu ", StringComparison.Ordinal))
            .ToArray();

        Assert.Equal(new[] { "Dieu 1", "Dieu 2", "Dieu 3", "Dieu 4" }, bodyFields.Select(field => field.Name).ToArray());
        Assert.All(bodyFields, field => Assert.Contains(
            Normalizer.ToSearchKey(field.Name),
            Normalizer.ToSearchKey(field.Value),
            StringComparison.Ordinal));
    }

    [Fact]
    public void SampleReview_StampNoiseDoesNotDominateSemanticOrdering()
    {
        var document = LoadSampleDocument();
        var bottomEvidence = document.Blocks
            .Where(block => block.BoundingBox.Y1 >= 530)
            .Select(block => BlockEvidence.FromBlock(block, EvidenceRole.Primary, "sample", semanticRole: "sample.bottom"))
            .ToArray();

        var ordered = ReviewEvidenceResolver.OrderBlockEvidence(bottomEvidence);
        var stampIndex = Array.FindIndex(ordered.ToArray(), evidence => evidence.NormalizedText.Contains("CONGTYCONNS", StringComparison.OrdinalIgnoreCase));
        var signerNameIndex = Array.FindIndex(ordered.ToArray(), evidence => evidence.NormalizedText.Contains("Pham Chanh Ha", StringComparison.OrdinalIgnoreCase));

        Assert.True(stampIndex > signerNameIndex);
    }

    [Fact]
    public void ReviewEvidenceResolver_DoesNotSuppressExplicitSignerEvidenceAsStampNoise()
    {
        var stampLikeSigner = new BlockEvidence(
            1,
            1,
            "paragraph",
            "CONGTY",
            "CONGTY",
            new BoundingBox(600, 600, 680, 620),
            EvidenceRole.Primary,
            "test",
            string.Empty,
            "signer.name");
        var regularBody = CreateEvidence("Dieu 1", EvidenceRole.Supporting, 0, 300, 100, 2);

        var primary = ReviewEvidenceResolver.ResolvePrimary(new[] { regularBody, stampLikeSigner });

        Assert.Same(stampLikeSigner, primary);
    }

    [Fact]
    public void ReviewEvidenceResolver_DoesNotSuppressTopLeftIssuerTextContainingCompanyFragments()
    {
        var issuer = new BlockEvidence(
            1,
            5,
            "page_header",
            "CONG TY CO PHAN NONG SAN PHU GIA",
            "CONG TY CO PHAN NONG SAN PHU GIA",
            new BoundingBox(80, 40, 360, 100),
            EvidenceRole.Primary,
            "IssuerExtractor.top-left-organization-keyword",
            string.Empty,
            "issuer.name");
        var topLeftFragment = new BlockEvidence(
            1,
            6,
            "page_header",
            "CONGTY",
            "CONGTY",
            new BoundingBox(90, 110, 160, 130),
            EvidenceRole.Primary,
            "IssuerExtractor.top-left-header-continuation",
            string.Empty,
            "issuer.header-line");
        var body = CreateEvidence("Dieu 1", EvidenceRole.Primary, 0, 300, 100, 1);

        var ordered = ReviewEvidenceResolver.OrderBlockEvidence(new[] { body, topLeftFragment, issuer });

        Assert.Equal(new[] { issuer, topLeftFragment, body }, ordered);
    }

    [Fact]
    public void ReviewEvidenceResolver_DemotesBottomRightStampFragmentsUnlessSignerEvidence()
    {
        var stamp = new BlockEvidence(
            1,
            1,
            "paragraph",
            "CONGTY",
            "CONGTY",
            new BoundingBox(600, 600, 680, 620),
            EvidenceRole.Primary,
            "sample",
            string.Empty,
            "sample.bottom");
        var signer = stamp with
        {
            RawText = "CONGTY",
            NormalizedText = "CONGTY",
            SemanticRole = "signer.name"
        };

        var stampOnly = ReviewEvidenceResolver.OrderBlockEvidence(new[] { stamp, CreateEvidence("Dieu 1", EvidenceRole.Primary, 0, 300, 100, 2) });
        var signerFirst = ReviewEvidenceResolver.OrderBlockEvidence(new[] { stamp, signer });

        Assert.NotSame(stamp, stampOnly[0]);
        Assert.Same(signer, signerFirst[0]);
    }

    [Fact]
    public void ReviewEvidenceResolver_FallsBackDeterministicallyWhenPageSizeOrBboxIsMissing()
    {
        var missingBoxPrimary = new ReviewEvidenceRegion(
            0,
            null,
            "CONGTY",
            "paragraph",
            2,
            true,
            EvidenceRole.Primary,
            new EvidenceProvenance("test", string.Empty, "CONGTY", "CONGTY", "sample.unknown"));
        var missingBoxSupport = new ReviewEvidenceRegion(
            0,
            null,
            "SUPPORT",
            "paragraph",
            1,
            false,
            EvidenceRole.Supporting,
            new EvidenceProvenance("test", string.Empty, "SUPPORT", "SUPPORT", "sample.unknown"));

        var ordered = ReviewEvidenceResolver.OrderReviewEvidence(new[] { missingBoxSupport, missingBoxPrimary });
        var primary = ReviewEvidenceResolver.ResolvePrimary(ordered);
        var drawable = ReviewEvidenceResolver.ResolvePrimary(ordered, requireDrawable: true);
        var provenance = ReviewEvidenceProvenanceFormatter.Format(missingBoxPrimary);

        Assert.Equal(new[] { missingBoxPrimary, missingBoxSupport }, ordered);
        Assert.Same(missingBoxPrimary, primary);
        Assert.Null(drawable);
        Assert.Contains("No bbox; evidence is retained for provenance but has no drawable overlay region from MinerU.", provenance, StringComparison.Ordinal);
    }

    [Fact]
    public void SampleReview_OverlayFocusIsDeterministicForSigner()
    {
        var first = new IntelligenceReviewService().LoadMinerUExtraction(File.ReadAllText(SamplePath()));
        var second = new IntelligenceReviewService().LoadMinerUExtraction(File.ReadAllText(SamplePath()));

        var firstSigner = first.PageOverlays.SelectMany(page => page.Regions).Single(region => region.FieldName == "Signer" && region.IsPrimary);
        var secondSigner = second.PageOverlays.SelectMany(page => page.Regions).Single(region => region.FieldName == "Signer" && region.IsPrimary);

        Assert.Equal(firstSigner.Id, secondSigner.Id);
        Assert.Equal(firstSigner.SemanticGroupId, secondSigner.SemanticGroupId);
        Assert.Equal(firstSigner.BoundingBox, secondSigner.BoundingBox);
    }

    [Fact]
    public void IssuerExtractor_PreservesNearbyMultiLineHeaderAsSupportingEvidence()
    {
        var document = ReadSyntheticDocument("""
        [[
          {"type":"page_header","content":{"page_header_content":[{"type":"text","content":"UBND TINH THANH HOA"}]},"bbox":[80,50,360,80]},
          {"type":"page_header","content":{"page_header_content":[{"type":"text","content":"VAN PHONG UBND TINH"}]},"bbox":[80,84,360,112]},
          {"type":"page_header","content":{"page_header_content":[{"type":"text","content":"CONG HOA XA HOI CHU NGHIA VIET NAM"}]},"bbox":[560,50,910,80]},
          {"type":"paragraph","content":{"paragraph_content":[{"type":"text","content":"Dieu 1. Noi dung."}]},"bbox":[120,500,820,540]}
        ]]
        """);

        var field = new IssuerExtractor(Normalizer).Extract(document);

        Assert.True(field.HasValue);
        Assert.Equal(EvidenceRole.Primary, field.Evidence[0].Role);
        Assert.Contains(field.Evidence, evidence => evidence.Role == EvidenceRole.Supporting && evidence.NormalizedText.Contains("VAN PHONG", StringComparison.Ordinal));
        Assert.DoesNotContain(field.Evidence, evidence => evidence.NormalizedText.Contains("CONG HOA", StringComparison.Ordinal));
    }

    [Fact]
    public void IssuerExtractor_PromotesOrganizationLineWhenHeaderIsMissing()
    {
        var document = ReadSyntheticDocument("""
        [{
          "width":1000,
          "height":1400,
          "blocks":[
            {"type":"title","content":{"title_content":[{"type":"text","content":"QUYET DINH"}]},"bbox":[410,120,620,150]},
            {"type":"paragraph","content":{"paragraph_content":[{"type":"text","content":"Tong Giam doc Cong ty Co phan Nong san Phu Gia"}]},"bbox":[210,185,820,215]},
            {"type":"paragraph","content":{"paragraph_content":[{"type":"text","content":"Can cu vao nhu cau to chuc cua Cong ty;"}]},"bbox":[140,230,820,255]}
          ]
        }]
        """);

        var field = new IssuerExtractor(Normalizer).Extract(document);

        Assert.True(field.HasValue);
        Assert.Equal("CONG TY CO PHAN NONG SAN PHU GIA", Normalizer.ToSearchKey(field.Value));
        Assert.Contains("issuer organization line found near document opening", field.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void TitleAndRecipientExtractors_HandleOfficialNoticeWithSubjectAndKinhGui()
    {
        var document = ReadSyntheticDocument("""
        [{
          "width":1000,
          "height":1400,
          "blocks":[
            {"type":"page_header","content":{"page_header_content":[{"type":"text","content":"UBND TINH THANH HOA"}]},"bbox":[80,50,360,80]},
            {"type":"title","content":{"title_content":[{"type":"text","content":"THONG BAO"}]},"bbox":[430,150,610,180]},
            {"type":"paragraph","content":{"paragraph_content":[{"type":"text","content":"V/v trien khai lich tiep cong dan"}]},"bbox":[330,185,720,210]},
            {"type":"paragraph","content":{"paragraph_content":[{"type":"text","content":"Kinh gui: Cac phong ban truc thuoc"}]},"bbox":[250,250,760,280]}
          ]
        }]
        """);

        var title = new TitleExtractor(Normalizer).Extract(document);
        var recipients = new RecipientExtractor(Normalizer).Extract(document);

        Assert.True(title.HasValue);
        Assert.Contains("THONG BAO", title.Value, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("V/v trien khai", title.Value, StringComparison.OrdinalIgnoreCase);
        Assert.True(recipients.HasValue);
        Assert.Contains("Kinh gui", recipients.Value, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RealWorldCoverage_PromotesCorporateDecisionWithNoisyNumberAndEffectiveDateWording()
    {
        var document = ReadSyntheticDocument("""
        [{
          "width":1000,
          "height":1400,
          "blocks":[
            {"type":"page_header","content":{"page_header_content":[{"type":"text","content":"CONG TY TNHH DICH VU TAN PHAT    S o :  15 / QD - TP ngay 12 thang 5 nam 2026"}]},"bbox":[70,48,430,112]},
            {"type":"title","content":{"title_content":[{"type":"text","content":"QUYET DINH"}]},"bbox":[410,155,610,185]},
            {"type":"paragraph","content":{"paragraph_content":[{"type":"text","content":"Ve viec bo nhiem Truong phong Kinh doanh"}]},"bbox":[310,188,720,216]},
            {"type":"paragraph","content":{"paragraph_content":[{"type":"text","content":"Dieu 3. Quyet dinh nay co hieu luc ke tu ngay ky."}]},"bbox":[120,520,850,552]},
            {"type":"paragraph","content":{"paragraph_content":[{"type":"text","content":"KT. GIAM DOC"}]},"bbox":[710,980,880,1008]},
            {"type":"paragraph","content":{"paragraph_content":[{"type":"text","content":"PHO GIAM DOC"}]},"bbox":[710,1008,880,1032]},
            {"type":"paragraph","content":{"paragraph_content":[{"type":"text","content":"Le Minh Hoang"}]},"bbox":[690,1050,890,1082]}
          ]
        }]
        """);

        var result = new DocumentExtractionResult(
            new IssuerExtractor(Normalizer).Extract(document),
            new DocumentNumberExtractor(Normalizer).Extract(document),
            new IssueDateExtractor(Normalizer).Extract(document),
            new TitleExtractor(Normalizer).Extract(document),
            new SignerExtractor(Normalizer).Extract(document),
            new RecipientExtractor(Normalizer).Extract(document),
            new BodyExtractor(Normalizer).Extract(document));

        Assert.Equal("CONG TY TNHH DICH VU TAN PHAT", result.Issuer.Value);
        Assert.Equal("15/QD-TP", result.DocumentNumber.Value);
        Assert.True(result.Title.HasValue);
        Assert.Contains("Ve viec bo nhiem", result.Title.Value, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("Le Minh Hoang", result.Signer.Value);
        Assert.Contains(result.BodySections.Single().Evidence, evidence => Normalizer.ToSearchKey(evidence.NormalizedText).Contains("HIEU LUC KE TU NGAY KY", StringComparison.Ordinal));
    }

    [Fact]
    public void RealWorldCoverage_PromotesOfficialNoticeWithLeftAlignedKinhGuiAndFooterRecipients()
    {
        var document = ReadSyntheticDocument("""
        [{
          "width":1000,
          "height":1400,
          "blocks":[
            {"type":"page_header","content":{"page_header_content":[{"type":"text","content":"UBND HUYEN DONG SON"}]},"bbox":[80,50,330,80]},
            {"type":"page_header","content":{"page_header_content":[{"type":"text","content":"VAN PHONG HDND VA UBND"}]},"bbox":[80,82,360,112]},
            {"type":"title","content":{"title_content":[{"type":"text","content":"THONG BAO"}]},"bbox":[430,150,600,180]},
            {"type":"paragraph","content":{"paragraph_content":[{"type":"text","content":"Trich yeu: ve lich tiep cong dan thang 5"}]},"bbox":[260,185,760,212]},
            {"type":"paragraph","content":{"paragraph_content":[{"type":"text","content":"Kinh gui: Cac phong ban chuyen mon; UBND cac xa"}]},"bbox":[125,260,790,290]},
            {"type":"paragraph","content":{"paragraph_content":[{"type":"text","content":"Noi nhan:"}]},"bbox":[95,1130,205,1152]},
            {"type":"paragraph","content":{"paragraph_content":[{"type":"text","content":"- Nhu tren;"}]},"bbox":[95,1154,240,1176]},
            {"type":"paragraph","content":{"paragraph_content":[{"type":"text","content":"- Luu: VT, HC."}]},"bbox":[95,1178,260,1200]}
          ]
        }]
        """);

        var issuer = new IssuerExtractor(Normalizer).Extract(document);
        var title = new TitleExtractor(Normalizer).Extract(document);
        var recipients = new RecipientExtractor(Normalizer).Extract(document);

        Assert.True(issuer.HasValue);
        Assert.Contains("VAN PHONG", string.Join(" ", issuer.Evidence.Select(evidence => evidence.NormalizedText)), StringComparison.OrdinalIgnoreCase);
        Assert.True(title.HasValue);
        Assert.Contains("Trich yeu", title.Value, StringComparison.OrdinalIgnoreCase);
        Assert.True(recipients.HasValue);
        Assert.Contains("Kinh gui", recipients.Value, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RealWorldCoverage_PreservesFalsePositiveGuardsForIssuerAndSignerCollisions()
    {
        var document = ReadSyntheticDocument("""
        [{
          "width":1000,
          "height":1400,
          "blocks":[
            {"type":"paragraph","content":{"paragraph_content":[{"type":"text","content":"PHONG HANH CHINH"}]},"bbox":[80,60,330,88]},
            {"type":"paragraph","content":{"paragraph_content":[{"type":"text","content":"CONG TY"}]},"bbox":[610,920,700,940]},
            {"type":"paragraph","content":{"paragraph_content":[{"type":"text","content":"CO PHAN"}]},"bbox":[610,942,700,960]},
            {"type":"paragraph","content":{"paragraph_content":[{"type":"text","content":"GIAM DOC"}]},"bbox":[710,985,840,1010]}
          ]
        }]
        """);

        var issuer = new IssuerExtractor(Normalizer).Extract(document);
        var signer = new SignerExtractor(Normalizer).Extract(document);

        Assert.False(issuer.HasValue);
        Assert.False(signer.HasValue);
    }

    [Fact]
    public void BodyExtractor_PreservesOrderedMultiBlockSectionEvidence()
    {
        var document = ReadSyntheticDocument("""
        [[
          {"type":"paragraph","content":{"paragraph_content":[{"type":"text","content":"Khoan 1. Noi dung thuc hien."}]},"bbox":[140,350,760,380]},
          {"type":"paragraph","content":{"paragraph_content":[{"type":"text","content":"Dieu 2. Hieu luc thi hanh."}]},"bbox":[120,460,820,490]},
          {"type":"paragraph","content":{"paragraph_content":[{"type":"text","content":"Dieu 1. Giao nhiem vu."}]},"bbox":[120,300,820,330]}
        ]]
        """);

        var sections = new BodyExtractor(Normalizer).Extract(document);

        Assert.Equal(new[] { 1, 2 }, sections.Select(section => section.Number).ToArray());
        Assert.Equal(new[] { EvidenceRole.Primary, EvidenceRole.Contextual }, sections[0].Evidence.Select(evidence => evidence.Role).ToArray());
        Assert.Contains("Khoan 1", sections[0].Text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ReviewOverlayMapper_CreatesSemanticClusterForMultiRegionField()
    {
        var field = new ReviewField(
            "Signer",
            "Pham Chanh Ha",
            0.93,
            ReviewStatus.Accepted,
            new[] { "person-like name found below signer role" },
            "CHU TICH HDQT",
            "paragraph",
            new BoundingBox(700, 660, 860, 690),
            0,
            new[]
            {
                new ReviewEvidenceRegion(0, new BoundingBox(700, 660, 860, 690), "CHU TICH HDQT", "paragraph", 1, false, EvidenceRole.Supporting),
                new ReviewEvidenceRegion(0, new BoundingBox(680, 690, 895, 722), "Pham Chanh Ha", "paragraph", 2, true, EvidenceRole.Primary),
            },
            Array.Empty<ReviewCandidate>());

        var page = Assert.Single(ReviewOverlayMapper.CreatePages(new[] { field }, Array.Empty<ReviewCandidate>(), new[] { new DocumentPageMetrics(0, 1000, 1400) }));
        var regions = page.Regions.Where(region => region.FieldName == "Signer").ToArray();

        Assert.Equal(2, regions.Length);
        Assert.Single(regions, region => region.IsPrimary);
        Assert.Single(regions.Select(region => region.SemanticGroupId).Distinct(StringComparer.Ordinal));
        Assert.Contains(regions, region => region.SourceText == "CHU TICH HDQT" && !region.IsPrimary && region.Role == EvidenceRole.Supporting);
        Assert.Contains(regions, region => region.SourceText == "Pham Chanh Ha" && region.IsPrimary);
    }

    [Fact]
    public void ReviewOverlayMapper_GeneratesStableSemanticGroupIdAcrossEvidenceOrder()
    {
        var first = CreateSignerField(new[]
        {
            new ReviewEvidenceRegion(0, new BoundingBox(700, 660, 860, 690), "CHU TICH HDQT", "paragraph", 10, false, EvidenceRole.Supporting),
            new ReviewEvidenceRegion(0, new BoundingBox(680, 690, 895, 722), "Pham Chanh Ha", "paragraph", 11, true, EvidenceRole.Primary),
        });
        var second = CreateSignerField(new[]
        {
            new ReviewEvidenceRegion(0, new BoundingBox(680, 690, 895, 722), "Pham Chanh Ha", "paragraph", 11, true, EvidenceRole.Primary),
            new ReviewEvidenceRegion(0, new BoundingBox(700, 660, 860, 690), "CHU TICH HDQT", "paragraph", 10, false, EvidenceRole.Supporting),
        });

        var firstGroup = ReviewOverlayMapper.CreatePages(new[] { first }, Array.Empty<ReviewCandidate>(), Array.Empty<DocumentPageMetrics>())
            .SelectMany(page => page.Regions)
            .Select(region => region.SemanticGroupId)
            .Distinct(StringComparer.Ordinal)
            .Single();
        var secondGroup = ReviewOverlayMapper.CreatePages(new[] { second }, Array.Empty<ReviewCandidate>(), Array.Empty<DocumentPageMetrics>())
            .SelectMany(page => page.Regions)
            .Select(region => region.SemanticGroupId)
            .Distinct(StringComparer.Ordinal)
            .Single();

        Assert.Equal(firstGroup, secondGroup);
    }

    [Fact]
    public void ReviewOverlayMapper_DoesNotCollapseDuplicateEvidenceRegions()
    {
        var bbox = new BoundingBox(10, 10, 100, 40);
        var field = new ReviewField(
            "Issuer",
            "UBND",
            0.9,
            ReviewStatus.Accepted,
            Array.Empty<string>(),
            "UBND",
            "page_header",
            bbox,
            0,
            new[]
            {
                new ReviewEvidenceRegion(0, bbox, "UBND", "page_header", 1, true, EvidenceRole.Primary),
                new ReviewEvidenceRegion(0, bbox, "UBND", "page_header", 1, true, EvidenceRole.Primary),
            },
            Array.Empty<ReviewCandidate>());

        var regions = Assert.Single(ReviewOverlayMapper.CreatePages(new[] { field }, Array.Empty<ReviewCandidate>(), Array.Empty<DocumentPageMetrics>())).Regions;

        Assert.Equal(2, regions.Count);
        Assert.Equal(2, regions.Select(region => region.Id).Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void ReviewOverlayMapper_KeepsSingleRegionFieldBackwardCompatible()
    {
        var field = new ReviewField(
            "Issuer",
            "CONG TY",
            0.9,
            ReviewStatus.Accepted,
            Array.Empty<string>(),
            "CONG TY",
            "page_header",
            new BoundingBox(10, 10, 100, 40),
            0,
            Array.Empty<ReviewEvidenceRegion>(),
            Array.Empty<ReviewCandidate>());

        var page = Assert.Single(ReviewOverlayMapper.CreatePages(new[] { field }, Array.Empty<ReviewCandidate>(), Array.Empty<DocumentPageMetrics>()));
        var region = Assert.Single(page.Regions);

        Assert.True(region.IsPrimary);
        Assert.False(string.IsNullOrWhiteSpace(region.SemanticGroupId));
        Assert.Equal(field.SourceBoundingBox, region.BoundingBox);
    }

    [Fact]
    public void ReviewOverlayMapper_AllowsCrossPageEvidenceWithoutCrashing()
    {
        var field = new ReviewField(
            "Body section",
            "Cross-page text",
            0.7,
            ReviewStatus.LowConfidence,
            Array.Empty<string>(),
            "Page 1",
            "paragraph",
            new BoundingBox(10, 10, 100, 40),
            0,
            new[]
            {
                new ReviewEvidenceRegion(0, new BoundingBox(10, 10, 100, 40), "Page 1", "paragraph", 1, true),
                new ReviewEvidenceRegion(1, new BoundingBox(15, 20, 120, 60), "Page 2", "paragraph", 2, false),
            },
            Array.Empty<ReviewCandidate>());

        var pages = ReviewOverlayMapper.CreatePages(
            new[] { field },
            Array.Empty<ReviewCandidate>(),
            new[] { new DocumentPageMetrics(0, 1000, 1400), new DocumentPageMetrics(1, 1000, 1400) });

        Assert.Equal(new[] { 0, 1 }, pages.Select(page => page.PageIndex).ToArray());
        Assert.Single(pages[0].Regions);
        Assert.Single(pages[1].Regions);
        Assert.Equal(pages[0].Regions[0].SemanticGroupId, pages[1].Regions[0].SemanticGroupId);
    }

    [Fact]
    public void ReviewOverlayMapper_PropagatesRejectedCandidateReasonAndLabel()
    {
        var review = new IntelligenceReviewService().LoadMinerUExtraction(File.ReadAllText(SamplePath()));

        var rejectedDob = Assert.Single(review.RejectedCandidates, candidate => candidate.Value == "01/10/1979");
        var rejectedOverlay = review.PageOverlays
            .SelectMany(page => page.Regions)
            .Single(region => region.FieldName == rejectedDob.FieldName && region.HighlightType == OverlayHighlightType.Rejected && region.SourceText.Contains("01", StringComparison.Ordinal));

        Assert.Equal(OverlayHighlightType.Rejected, rejectedOverlay.HighlightType);
        Assert.Contains("contains DOB context", rejectedOverlay.RejectReason, StringComparison.Ordinal);
        Assert.Contains("contains DOB context", rejectedOverlay.Label, StringComparison.Ordinal);
        Assert.Equal(rejectedDob.SourceBoundingBox, rejectedOverlay.BoundingBox);
    }

    [Fact]
    public void ReviewOverlayMapper_FiltersAcceptedRejectedAndLowConfidenceRegions()
    {
        var page = new ReviewPageOverlay(
            0,
            1000,
            1400,
            new[]
            {
                new ReviewOverlayRegion("a", "Issuer", 0, new BoundingBox(10, 10, 100, 40), 0.9, string.Empty, "issuer", "page_header", OverlayHighlightType.Accepted),
                new ReviewOverlayRegion("l", "Signer", 0, new BoundingBox(10, 50, 100, 80), 0.7, string.Empty, "signer", "paragraph", OverlayHighlightType.LowConfidence),
                new ReviewOverlayRegion("r", "Issue date", 0, new BoundingBox(10, 90, 100, 120), 0, "contains DOB context", "01/10/1979", "paragraph", OverlayHighlightType.Rejected),
            });

        var withoutRejected = ReviewOverlayMapper.ApplyFilter(page, new ReviewOverlayFilter(ShowAccepted: true, ShowRejected: false, ShowLowConfidenceOnly: false));
        Assert.DoesNotContain(withoutRejected.Regions, region => region.HighlightType == OverlayHighlightType.Rejected);
        var lowConfidenceOnly = ReviewOverlayMapper.ApplyFilter(page, new ReviewOverlayFilter(ShowAccepted: true, ShowRejected: true, ShowLowConfidenceOnly: true));

        Assert.Equal(new[] { "a", "l" }, withoutRejected.Regions.Select(region => region.Id).ToArray());
        Assert.Equal(new[] { "l" }, lowConfidenceOnly.Regions.Select(region => region.Id).ToArray());
    }

    [Fact]
    public void ReviewOverlayMapper_ScalesSourceBoundingBoxesIntoViewport()
    {
        var mapped = ReviewOverlayMapper.MapToViewport(
            new BoundingBox(100, 200, 300, 400),
            sourceWidth: 1000,
            sourceHeight: 2000,
            viewportWidth: 500,
            viewportHeight: 500);

        Assert.Equal(150, mapped.X1);
        Assert.Equal(50, mapped.Y1);
        Assert.Equal(200, mapped.X2);
        Assert.Equal(100, mapped.Y2);
    }

    [Fact]
    public void ReviewOverlayMapper_KeepsCoordinatesStableAcrossResizeAndZoom()
    {
        var source = new BoundingBox(100, 200, 300, 400);
        var fitted = ReviewOverlayMapper.MapToViewport(source, 1000, 2000, 500, 500);
        var doubledViewport = ReviewOverlayMapper.MapToViewport(source, 1000, 2000, 1000, 1000);
        var zoomed = ReviewOverlayMapper.MapToViewport(source, 1000, 2000, 500, 500, zoomFactor: 2);

        Assert.Equal(fitted.X1 * 2, doubledViewport.X1);
        Assert.Equal(fitted.Y1 * 2, doubledViewport.Y1);
        Assert.Equal(fitted.Width * 2, doubledViewport.Width);
        Assert.Equal(fitted.Height * 2, doubledViewport.Height);
        Assert.Equal(50, zoomed.X1);
        Assert.Equal(-150, zoomed.Y1);
        Assert.Equal(100, zoomed.Width);
        Assert.Equal(100, zoomed.Height);
    }

    [Fact]
    public void ReviewOverlayMapper_FiltersLargeOverlaySetWithoutChangingPageMetadata()
    {
        var regions = Enumerable.Range(0, 5_000)
            .Select(index => new ReviewOverlayRegion(
                $"r{index}",
                $"Field {index}",
                0,
                new BoundingBox(index % 100, index / 100, (index % 100) + 20, (index / 100) + 10),
                index % 3 == 0 ? 0.95 : index % 3 == 1 ? 0.65 : 0,
                index % 3 == 2 ? "rejected" : string.Empty,
                "source",
                "paragraph",
                index % 3 == 0 ? OverlayHighlightType.Accepted : index % 3 == 1 ? OverlayHighlightType.LowConfidence : OverlayHighlightType.Rejected))
            .ToArray();
        var page = new ReviewPageOverlay(0, 1000, 1400, regions);

        var lowConfidenceOnly = ReviewOverlayMapper.ApplyFilter(page, new ReviewOverlayFilter(ShowAccepted: true, ShowRejected: true, ShowLowConfidenceOnly: true));
        var noRejected = ReviewOverlayMapper.ApplyFilter(page, new ReviewOverlayFilter(ShowAccepted: true, ShowRejected: false, ShowLowConfidenceOnly: false));

        Assert.Equal(0, lowConfidenceOnly.PageIndex);
        Assert.Equal(1000, lowConfidenceOnly.SourceWidth);
        Assert.Equal(1400, lowConfidenceOnly.SourceHeight);
        Assert.Equal(regions.Count(region => region.HighlightType == OverlayHighlightType.LowConfidence), lowConfidenceOnly.Regions.Count);
        Assert.DoesNotContain(noRejected.Regions, region => region.HighlightType == OverlayHighlightType.Rejected);
    }

    [Fact]
    public void ReviewOverlayNavigator_SelectingRegionNavigatesToSourcePage()
    {
        var pages = new[]
        {
            new ReviewPageOverlay(0, 1000, 1400, Array.Empty<ReviewOverlayRegion>()),
            new ReviewPageOverlay(1, 1000, 1400, new[]
            {
                new ReviewOverlayRegion("target", "Signer", 1, new BoundingBox(20, 30, 80, 90), 0.72, string.Empty, "source", "paragraph", OverlayHighlightType.LowConfidence)
            }),
        };
        var navigator = new ReviewOverlayNavigator(pages);

        var navigated = navigator.NavigateToRegion("target");

        Assert.True(navigated);
        Assert.Equal(1, navigator.CurrentPageIndex);
        Assert.Equal("target", navigator.ActiveRegionId);
    }

    [Fact]
    public void ReviewOverlayNavigator_ClearsStaleActiveRegionWhenPageChanges()
    {
        var pages = new[]
        {
            new ReviewPageOverlay(0, 1000, 1400, new[]
            {
                new ReviewOverlayRegion("a", "Issuer", 0, new BoundingBox(10, 10, 20, 20), 0.95, string.Empty, "source", "page_header", OverlayHighlightType.Accepted)
            }),
            new ReviewPageOverlay(1, 1000, 1400, new[]
            {
                new ReviewOverlayRegion("b", "Signer", 1, new BoundingBox(30, 30, 40, 40), 0.7, string.Empty, "source", "paragraph", OverlayHighlightType.LowConfidence)
            }),
        };
        var navigator = new ReviewOverlayNavigator(pages);

        Assert.True(navigator.NavigateToRegion("a"));
        Assert.True(navigator.NavigateToPage(1));

        Assert.Equal(1, navigator.CurrentPageIndex);
        Assert.Null(navigator.ActiveRegionId);
    }

    [Fact]
    public void ReviewOverlayNavigator_HandlesLargeRegionIndexForRapidSelection()
    {
        var regions = Enumerable.Range(0, 5_000)
            .Select(index => new ReviewOverlayRegion(
                $"target-{index}",
                "Field",
                2,
                new BoundingBox(index, index, index + 10, index + 10),
                0.9,
                string.Empty,
                "source",
                "paragraph",
                OverlayHighlightType.Accepted))
            .ToArray();
        var navigator = new ReviewOverlayNavigator(new[]
        {
            new ReviewPageOverlay(0, 1000, 1400, Array.Empty<ReviewOverlayRegion>()),
            new ReviewPageOverlay(2, 1000, 1400, regions),
        });

        Assert.True(navigator.NavigateToRegion("target-4999"));

        Assert.Equal(2, navigator.CurrentPageIndex);
        Assert.Equal("target-4999", navigator.ActiveRegionId);
    }

    private static ReviewField CreateSignerField(IReadOnlyList<ReviewEvidenceRegion> evidence) =>
        new(
            "Signer",
            "Pham Chanh Ha",
            0.93,
            ReviewStatus.Accepted,
            new[] { "person-like name found below signer role" },
            "Pham Chanh Ha",
            "paragraph",
            new BoundingBox(680, 690, 895, 722),
            0,
            evidence,
            Array.Empty<ReviewCandidate>());

    private static BlockEvidence CreateEvidence(
        string text,
        EvidenceRole role,
        int pageIndex,
        double y,
        double x,
        int readingOrder) =>
        new(
            pageIndex + 1,
            readingOrder,
            "paragraph",
            text,
            text,
            new BoundingBox(x, y, x + 80, y + 20),
            role,
            "test.rule",
            string.Empty,
            "test.role");

    private static DocumentExtractionResult RunSamplePipeline()
    {
        var json = File.ReadAllText(SamplePath());
        return new SemanticExtractionPipeline().ExtractFromMinerUContentListV2(json);
    }

    private static DocumentStructure LoadSampleDocument()
    {
        var json = File.ReadAllText(SamplePath());
        return new MinerUContentListV2Adapter(Normalizer).Read(json);
    }

    private static DocumentStructure ReadSyntheticDocument(string json) =>
        new MinerUContentListV2Adapter(new VietnameseTextNormalizer()).Read(json);

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
