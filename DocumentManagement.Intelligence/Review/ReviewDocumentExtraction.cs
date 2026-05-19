namespace DocumentManagement.Intelligence.Review;

public sealed record ReviewDocumentExtraction(
    IReadOnlyList<ReviewField> Fields,
    IReadOnlyList<ReviewCandidate> AcceptedCandidates,
    IReadOnlyList<ReviewCandidate> RejectedCandidates,
    IReadOnlyList<ReviewPageOverlay> PageOverlays,
    string DebugReport);
