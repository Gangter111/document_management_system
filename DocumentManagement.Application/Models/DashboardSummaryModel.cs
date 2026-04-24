namespace DocumentManagement.Application.Models;

public class DashboardSummaryModel
{
    public int TotalDocuments { get; set; }
    public int DraftDocuments { get; set; }
    public int PendingApprovalDocuments { get; set; }
    public int IssuedDocuments { get; set; }
    public int ArchivedDocuments { get; set; }
    public int RejectedDocuments { get; set; }
    public int OverdueDocuments { get; set; }
}