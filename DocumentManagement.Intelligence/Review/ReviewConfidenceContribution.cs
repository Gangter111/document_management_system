namespace DocumentManagement.Intelligence.Review;

public sealed record ReviewConfidenceContribution(
    string Name,
    double Value,
    string Explanation);
