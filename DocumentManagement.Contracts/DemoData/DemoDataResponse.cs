namespace DocumentManagement.Contracts.DemoData;

public class DemoDataResponse
{
    public int ActiveDemoCount { get; set; }

    public int AffectedCount { get; set; }

    public string Message { get; set; } = string.Empty;
}
