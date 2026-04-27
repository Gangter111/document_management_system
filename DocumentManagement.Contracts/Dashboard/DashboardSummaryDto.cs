namespace DocumentManagement.Contracts.Dashboard;

public class DashboardSummaryDto
{
    public int TotalDocuments { get; set; }

    public int EffectiveDocuments { get; set; }

    public int ExpiredDocuments { get; set; }

    public int IssuedDocuments { get; set; }

    public string TopIssuingDepartment { get; set; } = "Chưa có dữ liệu";

    public int TopIssuingDepartmentCount { get; set; }

    public double AverageIssuedPerMonth { get; set; }
}
