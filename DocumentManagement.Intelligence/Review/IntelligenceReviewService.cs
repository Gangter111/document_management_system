using DocumentManagement.Intelligence.Confidence;
using DocumentManagement.Intelligence.Results;

namespace DocumentManagement.Intelligence.Review;

public sealed class IntelligenceReviewService
{
    private readonly SemanticExtractionPipeline _pipeline;

    public IntelligenceReviewService()
        : this(new SemanticExtractionPipeline())
    {
    }

    public IntelligenceReviewService(SemanticExtractionPipeline pipeline)
    {
        _pipeline = pipeline;
    }

    public async Task<ReviewDocumentExtraction> LoadMinerUExtractionAsync(string jsonPath, CancellationToken cancellationToken = default)
    {
        var json = await File.ReadAllTextAsync(jsonPath, cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        var result = _pipeline.ExtractFromMinerUContentListV2(json);
        cancellationToken.ThrowIfCancellationRequested();
        return Map(result);
    }

    public ReviewDocumentExtraction LoadMinerUExtraction(string json)
    {
        return Map(_pipeline.ExtractFromMinerUContentListV2(json));
    }

    private static ReviewDocumentExtraction Map(DocumentExtractionResult result)
    {
        var fields = new[]
        {
            MapField("Issuer", result.Issuer),
            MapField("Document number", result.DocumentNumber),
            MapField("Issue date", result.IssueDate),
            MapField("Title", result.Title),
            MapField("Signer", result.Signer),
            MapField("Recipients", result.Recipients),
        }
        .Concat(result.BodySections.Select(section => MapBodySection(section)))
        .ToArray();

        var allCandidates = fields.SelectMany(field => field.Candidates).ToArray();
        var accepted = allCandidates
            .Where(candidate => candidate.Status == ReviewStatus.Accepted || candidate.Status == ReviewStatus.LowConfidence)
            .ToArray();
        var rejected = allCandidates
            .Where(candidate => candidate.Status == ReviewStatus.Rejected)
            .ToArray();

        var pageOverlays = ReviewOverlayMapper.CreatePages(fields, rejected, result.PageMetrics);

        return new ReviewDocumentExtraction(
            fields,
            accepted,
            rejected,
            pageOverlays,
            result.DebugReport?.ToConsoleText() ?? string.Empty);
    }

    private static ReviewField MapField<T>(string name, ExtractedField<T> field)
    {
        var candidates = field.Candidates.Select(MapCandidateForReview).ToArray();
        var evidence = ReviewEvidenceResolver.ResolvePrimary(field.Evidence);
        var evidenceRegions = MapEvidenceRegions(field.Evidence);
        return new ReviewField(
            name,
            field.HasValue ? FormatValue(field.Value) : string.Empty,
            field.Confidence,
            ToFieldStatus(field),
            SplitReasoning(field.Reason),
            evidence?.NormalizedText ?? string.Empty,
            evidence?.BlockType ?? string.Empty,
            evidence?.BoundingBox,
            evidence?.PageNumber - 1 ?? 0,
            evidenceRegions,
            candidates,
            BuildReasoningChain(field.ConfidenceScore, evidenceRegions, ToFieldStatus(field), field.RejectReasons.Select(reason => reason.Message).ToArray()),
            BuildConfidenceBreakdown(field.ConfidenceScore));
    }

    private static ReviewField MapBodySection(BodySection section)
    {
        var evidence = ReviewEvidenceResolver.ResolvePrimary(section.Evidence);
        var candidates = section.Candidates.Select(MapCandidateForReview).ToArray();
        var evidenceRegions = MapEvidenceRegions(section.Evidence);
        return new ReviewField(
            section.Heading,
            section.Text,
            section.Confidence,
            ToStatus(section.Confidence, isRejected: false, hasValue: !string.IsNullOrWhiteSpace(section.Text)),
            section.ConfidenceScore.Reasoning,
            evidence?.NormalizedText ?? string.Empty,
            evidence?.BlockType ?? string.Empty,
            evidence?.BoundingBox,
            evidence?.PageNumber - 1 ?? 0,
            evidenceRegions,
            candidates,
            BuildReasoningChain(section.ConfidenceScore, evidenceRegions, ToStatus(section.Confidence, isRejected: false, hasValue: !string.IsNullOrWhiteSpace(section.Text)), Array.Empty<string>()),
            BuildConfidenceBreakdown(section.ConfidenceScore));
    }

    public static ReviewCandidate MapCandidateForReview(IExtractionCandidate candidate)
    {
        var evidence = ReviewEvidenceResolver.ResolvePrimary(candidate.Evidence);
        var evidenceRegions = MapEvidenceRegions(candidate.Evidence);
        var isRejected = candidate.RejectReasons.Count > 0 || candidate.Confidence.Value <= 0;
        var rejectReasons = candidate.RejectReasons.Select(reason => reason.Message).ToArray();
        if (isRejected && rejectReasons.Length == 0)
        {
            rejectReasons = candidate.Reasoning.ToArray();
        }

        return new ReviewCandidate(
            candidate.FieldName,
            isRejected || string.IsNullOrWhiteSpace(candidate.DisplayValue) ? candidate.CandidateText : candidate.DisplayValue,
            candidate.Confidence.Value,
            ToStatus(candidate.Confidence.Value, isRejected, !string.IsNullOrWhiteSpace(candidate.DisplayValue)),
            candidate.Reasoning,
            rejectReasons,
            evidence?.NormalizedText ?? candidate.CandidateText,
            evidence?.BlockType ?? string.Empty,
            evidence?.BoundingBox,
            evidence?.PageNumber - 1 ?? 0,
            evidenceRegions,
            BuildReasoningChain(candidate.Confidence, evidenceRegions, ToStatus(candidate.Confidence.Value, isRejected, !string.IsNullOrWhiteSpace(candidate.DisplayValue)), rejectReasons),
            BuildConfidenceBreakdown(candidate.Confidence));
    }

    private static IReadOnlyList<ReviewEvidenceRegion> MapEvidenceRegions(IReadOnlyList<BlockEvidence> evidence)
    {
        if (evidence.Count == 0)
        {
            return Array.Empty<ReviewEvidenceRegion>();
        }

        var ordered = ReviewEvidenceResolver.OrderBlockEvidence(evidence);
        var primary = ordered.FirstOrDefault();
        var diagnostics = ReviewEvidenceResolver.ExplainOrderedBlockEvidence(ordered);
        var regions = new List<ReviewEvidenceRegion>(ordered.Count);
        for (var i = 0; i < ordered.Count; i++)
        {
            var item = ordered[i];
            var pageIndex = item.PageNumber - 1;
            var provenance = item.Provenance with
            {
                Diagnostics = diagnostics[i]
            };

            regions.Add(new ReviewEvidenceRegion(
                pageIndex,
                item.BoundingBox,
                item.NormalizedText,
                item.BlockType,
                item.ReadingOrder,
                ReferenceEquals(item, primary),
                item.Role,
                provenance));
        }

        return regions;
    }

    private static ReviewStatus ToFieldStatus<T>(ExtractedField<T> field) =>
        ToStatus(field.Confidence, field.RejectReasons.Count > 0, field.HasValue);

    private static IReadOnlyList<ReviewReasoningStep> BuildReasoningChain(
        ConfidenceScore confidence,
        IReadOnlyList<ReviewEvidenceRegion> evidence,
        ReviewStatus status,
        IReadOnlyList<string> rejectReasons)
    {
        var steps = new List<ReviewReasoningStep>();
        var acceptedEvidence = evidence
            .Where(item => item.Role == EvidenceRole.Primary)
            .Select(item => item.SourceText)
            .Where(text => !string.IsNullOrWhiteSpace(text))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var supportingEvidence = evidence
            .Where(item => item.Role is EvidenceRole.Supporting or EvidenceRole.Contextual)
            .Select(item => $"{item.Role}: {item.SourceText}")
            .Where(text => !string.IsNullOrWhiteSpace(text))
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        foreach (var text in acceptedEvidence)
        {
            steps.Add(new ReviewReasoningStep("Accepted evidence", text));
        }

        foreach (var text in supportingEvidence)
        {
            steps.Add(new ReviewReasoningStep("Supporting evidence", text));
        }

        foreach (var text in confidence.Reasoning.Where(reason => !string.IsNullOrWhiteSpace(reason)))
        {
            steps.Add(new ReviewReasoningStep("Scoring reason", text));
        }

        foreach (var text in rejectReasons.Where(reason => !string.IsNullOrWhiteSpace(reason)).Distinct(StringComparer.Ordinal))
        {
            steps.Add(new ReviewReasoningStep("Rejected evidence", text));
        }

        var decision = status switch
        {
            ReviewStatus.Accepted => $"Accepted with confidence {confidence.Value:P0}.",
            ReviewStatus.LowConfidence => $"Needs review with confidence {confidence.Value:P0}.",
            ReviewStatus.Rejected => $"Rejected with confidence {confidence.Value:P0}.",
            _ => $"No accepted value; confidence {confidence.Value:P0}."
        };
        steps.Add(new ReviewReasoningStep("Final decision", decision));

        return steps;
    }

    private static IReadOnlyList<ReviewConfidenceContribution> BuildConfidenceBreakdown(ConfidenceScore confidence)
    {
        var factors = confidence.Factors;
        var semantic = ReadFactor(factors, "semanticConsistency") + ReadFactor(factors, "keywordProximity");
        var layout = ReadFactor(factors, "layoutRegion") + ReadFactor(factors, "blockType");
        var ocrPenalty = Math.Max(0, 0.18 - ReadFactor(factors, "ocrQuality"));
        var ambiguityPenalty = ResolveAmbiguityPenalty(confidence);

        return new[]
        {
            new ReviewConfidenceContribution(
                "Semantic match",
                semantic,
                "Keyword proximity plus semantic consistency contribution."),
            new ReviewConfidenceContribution(
                "Layout",
                layout,
                "Expected page region plus block type contribution."),
            new ReviewConfidenceContribution(
                "OCR degradation penalty",
                -ocrPenalty,
                ocrPenalty <= 0 ? "No OCR degradation penalty detected." : "Penalty inferred from reduced OCR quality contribution."),
            new ReviewConfidenceContribution(
                "Ambiguity penalty",
                -ambiguityPenalty,
                ambiguityPenalty <= 0 ? "No ambiguity penalty detected." : "Penalty inferred from rejection or uncertain reasoning.")
        };
    }

    private static double ReadFactor(IReadOnlyDictionary<string, double> factors, string key) =>
        factors.TryGetValue(key, out var value) ? value : 0;

    private static double ResolveAmbiguityPenalty(ConfidenceScore confidence)
    {
        if (confidence.Value <= 0 && confidence.Factors.Count == 0)
        {
            return 0.25;
        }

        return confidence.Reasoning.Any(reason =>
            reason.Contains("uncertain", StringComparison.OrdinalIgnoreCase) ||
            reason.Contains("missing", StringComparison.OrdinalIgnoreCase) ||
            reason.Contains("ambiguous", StringComparison.OrdinalIgnoreCase))
            ? 0.12
            : 0;
    }

    private static ReviewStatus ToStatus(double confidence, bool isRejected, bool hasValue)
    {
        if (isRejected)
        {
            return ReviewStatus.Rejected;
        }

        if (!hasValue || confidence <= 0)
        {
            return ReviewStatus.Empty;
        }

        return confidence >= 0.85 ? ReviewStatus.Accepted : ReviewStatus.LowConfidence;
    }

    private static IReadOnlyList<string> SplitReasoning(string reason) =>
        string.IsNullOrWhiteSpace(reason)
            ? Array.Empty<string>()
            : reason.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static string FormatValue<T>(T? value) =>
        value is DateOnly date ? date.ToString("yyyy-MM-dd") : value?.ToString() ?? string.Empty;
}
