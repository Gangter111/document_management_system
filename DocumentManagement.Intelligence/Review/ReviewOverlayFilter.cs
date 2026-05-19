namespace DocumentManagement.Intelligence.Review;

public sealed record ReviewOverlayFilter(
    bool ShowAccepted,
    bool ShowRejected,
    bool ShowLowConfidenceOnly);
