using DocumentManagement.Application.Models;

namespace DocumentManagement.Application.Interfaces;

public interface IDashboardRepository
{
    Task<DashboardSummaryModel> GetSummaryAsync();
    Task<IReadOnlyList<RecentDocumentItem>> GetRecentDocumentsAsync(int top);
    Task<IReadOnlyList<DashboardStatusChartItem>> GetStatusBreakdownAsync();
}