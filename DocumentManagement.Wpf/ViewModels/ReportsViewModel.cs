using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Input;
using DocumentManagement.Contracts.Reports;
using DocumentManagement.Wpf.Commands;
using DocumentManagement.Wpf.Services;
using Microsoft.Win32;
using OfficeOpenXml;

namespace DocumentManagement.Wpf.ViewModels;

public class ReportsViewModel : BaseViewModel
{
    private readonly ApiService _apiService;
    private readonly INotificationService _notificationService;
    private readonly ClientPermissionService _permissionService;

    private bool _isLoading;
    private string _statusMessage = "Sẵn sàng";
    private int _totalDocuments;
    private int _archivedDocuments;
    private int _effectiveDocuments;
    private int _expiredDocuments;
    private int _draftDocuments;

    public ObservableCollection<ReportChartItemDto> ByStatus { get; } = new();

    public ObservableCollection<ReportChartItemDto> ByCategory { get; } = new();

    public ObservableCollection<ReportChartItemDto> ByDepartment { get; } = new();

    public bool IsLoading
    {
        get => _isLoading;
        set => SetProperty(ref _isLoading, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public int TotalDocuments
    {
        get => _totalDocuments;
        set => SetProperty(ref _totalDocuments, value);
    }

    public int ArchivedDocuments
    {
        get => _archivedDocuments;
        set => SetProperty(ref _archivedDocuments, value);
    }

    public int EffectiveDocuments
    {
        get => _effectiveDocuments;
        set => SetProperty(ref _effectiveDocuments, value);
    }

    public int ExpiredDocuments
    {
        get => _expiredDocuments;
        set => SetProperty(ref _expiredDocuments, value);
    }

    public int DraftDocuments
    {
        get => _draftDocuments;
        set => SetProperty(ref _draftDocuments, value);
    }

    public bool CanViewReports => _permissionService.CanViewReports();

    public ICommand RefreshCommand { get; }

    public ICommand ExportCommand { get; }

    public ReportsViewModel(
        ApiService apiService,
        INotificationService notificationService,
        ClientPermissionService permissionService)
    {
        _apiService = apiService;
        _notificationService = notificationService;
        _permissionService = permissionService;

        RefreshCommand = new RelayCommand(async _ => await LoadAsync(), _ => !IsLoading);
        ExportCommand = new RelayCommand(async _ => await ExportAsync(), _ => CanViewReports && !IsLoading);
    }

    public async Task LoadAsync()
    {
        try
        {
            IsLoading = true;

            if (!CanViewReports)
            {
                StatusMessage = "Bạn không có quyền xem báo cáo.";
                return;
            }

            var report = await _apiService.GetReportSummaryAsync();
            Apply(report);
            StatusMessage = "Báo cáo đã được cập nhật.";
        }
        catch (Exception ex)
        {
            StatusMessage = "Lỗi tải báo cáo";
            _notificationService.ShowError("Không thể tải báo cáo: " + ex.Message, "Lỗi");
        }
        finally
        {
            IsLoading = false;
            CommandManager.InvalidateRequerySuggested();
        }
    }

    private void Apply(ReportSummaryDto report)
    {
        TotalDocuments = report.TotalDocuments;
        ArchivedDocuments = report.ArchivedDocuments;
        EffectiveDocuments = report.EffectiveDocuments;
        ExpiredDocuments = report.ExpiredDocuments;
        DraftDocuments = report.DraftDocuments;

        Replace(ByStatus, report.ByStatus);
        Replace(ByCategory, report.ByCategory);
        Replace(ByDepartment, report.ByDepartment);
    }

    private async Task ExportAsync()
    {
        if (!CanViewReports)
        {
            return;
        }

        var dialog = new SaveFileDialog
        {
            Filter = "Excel Files (*.xlsx)|*.xlsx",
            FileName = $"Bao_cao_tong_hop_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx"
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        try
        {
            IsLoading = true;

            await Task.Run(() =>
            {
                ExcelPackage.LicenseContext = LicenseContext.NonCommercial;

                using var package = new ExcelPackage();
                var summary = package.Workbook.Worksheets.Add("Tong hop");
                summary.Cells[1, 1].Value = "Chỉ tiêu";
                summary.Cells[1, 2].Value = "Số lượng";
                summary.Cells[2, 1].Value = "Tổng văn bản";
                summary.Cells[2, 2].Value = TotalDocuments;
                summary.Cells[3, 1].Value = "Đã lưu trữ";
                summary.Cells[3, 2].Value = ArchivedDocuments;
                summary.Cells[4, 1].Value = "Còn hiệu lực";
                summary.Cells[4, 2].Value = EffectiveDocuments;
                summary.Cells[5, 1].Value = "Hết hiệu lực";
                summary.Cells[5, 2].Value = ExpiredDocuments;
                summary.Cells[6, 1].Value = "Bản nháp";
                summary.Cells[6, 2].Value = DraftDocuments;
                summary.Cells.AutoFitColumns();

                AddSheet(package, "Trang thai", ByStatus);
                AddSheet(package, "Danh muc", ByCategory);
                AddSheet(package, "Phong ban", ByDepartment);

                File.WriteAllBytes(dialog.FileName, package.GetAsByteArray());
            });

            _notificationService.ShowSuccess("Đã xuất báo cáo Excel.", "Báo cáo");
        }
        catch (Exception ex)
        {
            _notificationService.ShowError("Không thể xuất báo cáo: " + ex.Message, "Lỗi");
        }
        finally
        {
            IsLoading = false;
        }
    }

    private static void Replace(
        ObservableCollection<ReportChartItemDto> target,
        IReadOnlyList<ReportChartItemDto> source)
    {
        target.Clear();

        foreach (var item in source)
        {
            target.Add(item);
        }
    }

    private static void AddSheet(
        ExcelPackage package,
        string name,
        IEnumerable<ReportChartItemDto> items)
    {
        var sheet = package.Workbook.Worksheets.Add(name);
        sheet.Cells[1, 1].Value = "Tên";
        sheet.Cells[1, 2].Value = "Số lượng";

        var row = 2;
        foreach (var item in items)
        {
            sheet.Cells[row, 1].Value = item.Name;
            sheet.Cells[row, 2].Value = item.Value;
            row++;
        }

        sheet.Cells.AutoFitColumns();
    }
}
