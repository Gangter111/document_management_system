using DocumentManagement.Intelligence;
using DocumentManagement.Intelligence.Results;

var samplePath = args.Length > 0
    ? args[0]
    : Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "artifacts", "samples", "decision_content_list_v2.json"));

if (!File.Exists(samplePath))
{
    Console.Error.WriteLine($"Sample JSON not found: {samplePath}");
    return 1;
}

var json = await File.ReadAllTextAsync(samplePath);
var pipeline = new SemanticExtractionPipeline();
var result = pipeline.ExtractFromMinerUContentListV2(json);

if (result.DebugReport is not null)
{
    Console.WriteLine(result.DebugReport.ToConsoleText());
}

Console.WriteLine();
Console.WriteLine("=== FIELDS ===");
PrintField("Issuer", result.Issuer);
PrintField("Document number", result.DocumentNumber);
PrintField("Issue date", result.IssueDate);
PrintField("Title", result.Title);
PrintField("Signer", result.Signer);
PrintField("Recipients", result.Recipients);

Console.WriteLine();
Console.WriteLine("=== BODY SECTIONS ===");
if (result.BodySections.Count == 0)
{
    Console.WriteLine("  (empty)");
}
else
{
    foreach (var section in result.BodySections.OrderBy(section => section.Number))
    {
        Console.WriteLine($"  {section.Heading} [{section.Confidence:0.00}]");
        Console.WriteLine($"    {section.Text}");
    }
}

return 0;

static void PrintField<T>(string label, ExtractedField<T> field)
{
    var value = field.HasValue ? FormatValue(field.Value) : "(empty)";
    Console.WriteLine($"{label}: {value} [{field.Confidence:0.00}] {field.Reason}");
}

static string? FormatValue<T>(T? value) =>
    value is DateOnly date ? date.ToString("yyyy-MM-dd") : value?.ToString();
