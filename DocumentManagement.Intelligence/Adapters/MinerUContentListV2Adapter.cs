using System.Text.Json;
using DocumentManagement.Intelligence.Models;
using DocumentManagement.Intelligence.Normalization;

namespace DocumentManagement.Intelligence.Adapters;

public sealed class MinerUContentListV2Adapter
{
    private readonly VietnameseTextNormalizer _normalizer;

    public MinerUContentListV2Adapter(VietnameseTextNormalizer normalizer)
    {
        _normalizer = normalizer;
    }

    public DocumentStructure Read(string json)
    {
        using var document = JsonDocument.Parse(json);
        if (document.RootElement.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidDataException("MinerU content_list_v2 root must be an array.");
        }

        var blocks = new List<DocumentBlock>();
        var pages = new List<DocumentPageMetrics>();
        var order = 0;
        var pageNumber = 1;

        foreach (var pageElement in document.RootElement.EnumerateArray())
        {
            var blockElements = EnumeratePageBlocks(pageElement, pageNumber, pages);
            if (blockElements.Count == 0)
            {
                pageNumber++;
                continue;
            }

            foreach (var blockElement in blockElements)
            {
                if (blockElement.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                var blockType = ReadString(blockElement, "type");
                var rawText = ReadContentText(blockElement, blockType);
                if (string.IsNullOrWhiteSpace(rawText) || !TryReadBoundingBox(blockElement, out var boundingBox))
                {
                    continue;
                }

                blocks.Add(new DocumentBlock(
                    pageNumber,
                    order++,
                    blockType,
                    rawText,
                    _normalizer.Normalize(rawText),
                    boundingBox,
                    ReadLineMetadata(blockElement),
                    ReadSpanMetadata(blockElement),
                    ReadNonTextMetadata(blockElement)));
            }

            pageNumber++;
        }

        return new DocumentStructure(blocks, pages);
    }

    private static IReadOnlyList<JsonElement> EnumeratePageBlocks(
        JsonElement pageElement,
        int pageNumber,
        List<DocumentPageMetrics> pages)
    {
        if (pageElement.ValueKind == JsonValueKind.Array)
        {
            return pageElement.EnumerateArray().ToArray();
        }

        if (pageElement.ValueKind != JsonValueKind.Object)
        {
            return Array.Empty<JsonElement>();
        }

        if (TryReadPageMetrics(pageElement, pageNumber, out var metrics))
        {
            pages.Add(metrics);
        }

        foreach (var propertyName in new[] { "blocks", "content", "items" })
        {
            if (pageElement.TryGetProperty(propertyName, out var blocksElement) &&
                blocksElement.ValueKind == JsonValueKind.Array)
            {
                return blocksElement.EnumerateArray().ToArray();
            }
        }

        return Array.Empty<JsonElement>();
    }

    private static string ReadString(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? string.Empty
            : string.Empty;

    private static string ReadContentText(JsonElement blockElement, string blockType)
    {
        if (!blockElement.TryGetProperty("content", out var contentElement) || contentElement.ValueKind != JsonValueKind.Object)
        {
            return string.Empty;
        }

        var preferredProperty = $"{blockType}_content";
        if (contentElement.TryGetProperty(preferredProperty, out var preferredContent))
        {
            return ReadTextRuns(preferredContent);
        }

        foreach (var property in contentElement.EnumerateObject())
        {
            if (property.Name.EndsWith("_content", StringComparison.OrdinalIgnoreCase))
            {
                return ReadTextRuns(property.Value);
            }
        }

        return string.Empty;
    }

    private static string ReadTextRuns(JsonElement contentElement)
    {
        if (contentElement.ValueKind == JsonValueKind.String)
        {
            return contentElement.GetString() ?? string.Empty;
        }

        if (contentElement.ValueKind != JsonValueKind.Array)
        {
            return string.Empty;
        }

        var runs = new List<string>();
        foreach (var runElement in contentElement.EnumerateArray())
        {
            if (runElement.ValueKind == JsonValueKind.String)
            {
                runs.Add(runElement.GetString() ?? string.Empty);
                continue;
            }

            if (runElement.ValueKind == JsonValueKind.Object &&
                runElement.TryGetProperty("content", out var content) &&
                content.ValueKind == JsonValueKind.String)
            {
                runs.Add(content.GetString() ?? string.Empty);
            }
        }

        return string.Join(" ", runs.Where(run => !string.IsNullOrWhiteSpace(run)));
    }

    private static IReadOnlyList<DocumentLineMetadata>? ReadLineMetadata(JsonElement blockElement)
    {
        if (!blockElement.TryGetProperty("lines", out var linesElement) || linesElement.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        var lines = new List<DocumentLineMetadata>();
        foreach (var lineElement in linesElement.EnumerateArray())
        {
            var text = lineElement.ValueKind == JsonValueKind.String
                ? lineElement.GetString() ?? string.Empty
                : ReadString(lineElement, "text");
            if (string.IsNullOrWhiteSpace(text))
            {
                continue;
            }

            lines.Add(new DocumentLineMetadata(text, TryReadBoundingBox(lineElement, out var bbox) ? bbox : null));
        }

        return lines.Count == 0 ? null : lines;
    }

    private static IReadOnlyList<DocumentSpanMetadata>? ReadSpanMetadata(JsonElement blockElement)
    {
        if (!blockElement.TryGetProperty("spans", out var spansElement) || spansElement.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        var spans = new List<DocumentSpanMetadata>();
        foreach (var spanElement in spansElement.EnumerateArray())
        {
            var text = spanElement.ValueKind == JsonValueKind.String
                ? spanElement.GetString() ?? string.Empty
                : ReadString(spanElement, "text");
            if (string.IsNullOrWhiteSpace(text))
            {
                continue;
            }

            var confidence = spanElement.ValueKind == JsonValueKind.Object &&
                spanElement.TryGetProperty("confidence", out var confidenceElement) &&
                confidenceElement.ValueKind == JsonValueKind.Number
                ? confidenceElement.GetDouble()
                : (double?)null;
            spans.Add(new DocumentSpanMetadata(text, TryReadBoundingBox(spanElement, out var bbox) ? bbox : null, confidence));
        }

        return spans.Count == 0 ? null : spans;
    }

    private static IReadOnlyDictionary<string, string>? ReadNonTextMetadata(JsonElement blockElement)
    {
        if (!blockElement.TryGetProperty("metadata", out var metadataElement) || metadataElement.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var metadata = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var property in metadataElement.EnumerateObject())
        {
            if (property.Value.ValueKind is JsonValueKind.String or JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False)
            {
                metadata[property.Name] = property.Value.ToString();
            }
        }

        return metadata.Count == 0 ? null : metadata;
    }

    private static bool TryReadPageMetrics(JsonElement pageElement, int pageNumber, out DocumentPageMetrics metrics)
    {
        metrics = new DocumentPageMetrics(pageNumber - 1, 0, 0);
        if (TryReadNumber(pageElement, "width", out var width) &&
            TryReadNumber(pageElement, "height", out var height))
        {
            metrics = new DocumentPageMetrics(pageNumber - 1, width, height);
            return true;
        }

        if (pageElement.TryGetProperty("page_info", out var pageInfo) &&
            TryReadNumber(pageInfo, "width", out width) &&
            TryReadNumber(pageInfo, "height", out height))
        {
            metrics = new DocumentPageMetrics(pageNumber - 1, width, height);
            return true;
        }

        return false;
    }

    private static bool TryReadNumber(JsonElement element, string propertyName, out double value)
    {
        value = 0;
        return element.TryGetProperty(propertyName, out var numberElement) &&
            numberElement.ValueKind == JsonValueKind.Number &&
            numberElement.TryGetDouble(out value);
    }

    private static bool TryReadBoundingBox(JsonElement blockElement, out BoundingBox boundingBox)
    {
        boundingBox = new BoundingBox(0, 0, 0, 0);
        if (blockElement.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        if (!blockElement.TryGetProperty("bbox", out var bboxElement) ||
            bboxElement.ValueKind != JsonValueKind.Array ||
            bboxElement.GetArrayLength() != 4)
        {
            return false;
        }

        var values = bboxElement.EnumerateArray().Select(value => value.GetDouble()).ToArray();
        boundingBox = new BoundingBox(values[0], values[1], values[2], values[3]);
        return true;
    }
}
