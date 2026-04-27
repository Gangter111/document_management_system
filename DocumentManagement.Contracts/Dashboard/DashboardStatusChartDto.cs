namespace DocumentManagement.Contracts.Dashboard;

public class DashboardStatusChartDto
{
    public long StatusId { get; set; }

    public string Name { get; set; } = string.Empty;

    public int Value { get; set; }
}
