namespace DocumentManagement.Contracts.Reports;

public class ReportSummaryDto
{
    public int TotalDocuments { get; set; }

    public int ArchivedDocuments { get; set; }

    public int EffectiveDocuments { get; set; }

    public int ExpiredDocuments { get; set; }

    public int DraftDocuments { get; set; }

    public IReadOnlyList<ReportChartItemDto> ByStatus { get; set; } = new List<ReportChartItemDto>();

    public IReadOnlyList<ReportChartItemDto> ByCategory { get; set; } = new List<ReportChartItemDto>();

    public IReadOnlyList<ReportChartItemDto> ByDepartment { get; set; } = new List<ReportChartItemDto>();
}

public class ReportChartItemDto
{
    public string Name { get; set; } = string.Empty;

    public int Value { get; set; }
}
