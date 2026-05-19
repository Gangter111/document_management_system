using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace DocumentManagement.Intelligence.Review;

public static class ReviewOverlayIdentity
{
    public static string EvidenceKey(ReviewOverlayRegion region) =>
        StableHash(string.Create(
            CultureInfo.InvariantCulture,
            $"{Math.Max(0, region.PageIndex)}:{region.BoundingBox.X1:0.###}:{region.BoundingBox.Y1:0.###}:{region.BoundingBox.X2:0.###}:{region.BoundingBox.Y2:0.###}:{region.BlockType}:{region.SourceText}:{region.Role}:{region.Provenance?.SemanticRole ?? string.Empty}"));

    public static string SelectionKey(ReviewOverlayRegion region) =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"{region.FieldName}|{region.HighlightType}|{region.Role}|{region.Provenance?.SemanticRole ?? string.Empty}|{EvidenceKey(region)}");

    public static string SourceKey(ReviewOverlayRegion region) =>
        StableHash(string.Create(
            CultureInfo.InvariantCulture,
            $"{Math.Max(0, region.PageIndex)}:{region.BoundingBox.X1:0.###}:{region.BoundingBox.Y1:0.###}:{region.BoundingBox.X2:0.###}:{region.BoundingBox.Y2:0.###}:{region.BlockType}:{region.SourceText}:{region.Provenance?.SemanticRole ?? string.Empty}"));

    public static string SourceKey(ReviewEvidenceRegion evidence) =>
        StableHash(string.Create(
            CultureInfo.InvariantCulture,
            $"{Math.Max(0, evidence.PageIndex)}:{evidence.BoundingBox?.X1 ?? -1:0.###}:{evidence.BoundingBox?.Y1 ?? -1:0.###}:{evidence.BoundingBox?.X2 ?? -1:0.###}:{evidence.BoundingBox?.Y2 ?? -1:0.###}:{evidence.BlockType}:{evidence.SourceText}:{evidence.Provenance?.SemanticRole ?? string.Empty}"));

    internal static string StableHash(string value)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(hash, 0, 8).ToLowerInvariant();
    }
}
