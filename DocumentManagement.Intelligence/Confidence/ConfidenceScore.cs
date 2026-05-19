namespace DocumentManagement.Intelligence.Confidence;

public sealed record ConfidenceScore(
    double Value,
    IReadOnlyDictionary<string, double> Factors,
    IReadOnlyList<string> Reasoning)
{
    public static ConfidenceScore Zero(string reason) =>
        new(0, new Dictionary<string, double>(), new[] { reason });

    public static ConfidenceScore FromFactors(IReadOnlyDictionary<string, double> factors, params string[] reasoning)
    {
        var value = Math.Clamp(factors.Values.Sum(), 0, 0.99);
        return new ConfidenceScore(value, factors, reasoning);
    }
}
