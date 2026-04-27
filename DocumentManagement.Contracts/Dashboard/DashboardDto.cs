namespace DocumentManagement.Contracts.Dashboard;

public class DashboardDto
{
    public DashboardSummaryDto Summary { get; set; } = new();

    public List<RecentDocumentDto> RecentDocuments { get; set; } = new();

    public List<DashboardChartItemDto> EffectivenessChart { get; set; } = new();

    public List<DashboardChartItemDto> MonthlyIssuedChart { get; set; } = new();

    public List<DashboardChartItemDto> DepartmentIssuedChart { get; set; } = new();
}
