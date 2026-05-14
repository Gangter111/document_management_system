using System.Collections.ObjectModel;
using System.Windows.Input;
using DocumentManagement.Contracts.Dashboard;
using DocumentManagement.Wpf.Commands;
using DocumentManagement.Wpf.Services;
using LiveCharts;
using LiveCharts.Wpf;

namespace DocumentManagement.Wpf.ViewModels;

public class DashboardViewModel : BaseViewModel
{
    private readonly ApiService _apiService;

    private int _totalDocuments;
    private int _issuedDocuments;
    private int _effectiveDocuments;
    private int _expiredDocuments;
    private string _topIssuingDepartment = "Chưa có dữ liệu";
    private int _topIssuingDepartmentCount;
    private double _averageIssuedPerMonth;
    private string _selectedMetric = "Total";

    private bool _isLoading;
    private bool _hasError;
    private string _errorMessage = string.Empty;

    public int TotalDocuments
    {
        get => _totalDocuments;
        set
        {
            if (SetProperty(ref _totalDocuments, value))
            {
                RefreshKpiPercentages();
            }
        }
    }

    public int IssuedDocuments
    {
        get => _issuedDocuments;
        set
        {
            if (SetProperty(ref _issuedDocuments, value))
            {
                RefreshKpiPercentages();
            }
        }
    }

    public int EffectiveDocuments
    {
        get => _effectiveDocuments;
        set
        {
            if (SetProperty(ref _effectiveDocuments, value))
            {
                RefreshKpiPercentages();
            }
        }
    }

    public int ExpiredDocuments
    {
        get => _expiredDocuments;
        set
        {
            if (SetProperty(ref _expiredDocuments, value))
            {
                RefreshKpiPercentages();
            }
        }
    }

    public string TopIssuingDepartment
    {
        get => _topIssuingDepartment;
        set => SetProperty(ref _topIssuingDepartment, value);
    }

    public int TopIssuingDepartmentCount
    {
        get => _topIssuingDepartmentCount;
        set => SetProperty(ref _topIssuingDepartmentCount, value);
    }

    public double AverageIssuedPerMonth
    {
        get => _averageIssuedPerMonth;
        set => SetProperty(ref _averageIssuedPerMonth, value);
    }

    public ObservableCollection<RecentDocumentItem> RecentDocuments { get; } = new();

    public SeriesCollection EffectivenessSeries { get; } = new();

    public SeriesCollection MonthlyIssuedSeries { get; } = new();

    public SeriesCollection DepartmentIssuedSeries { get; } = new();

    public ObservableCollection<DashboardChartItemDto> DepartmentIssuedItems { get; } = new();

    public List<string> MonthlyIssuedLabels { get; private set; } = new();

    public List<string> DepartmentIssuedLabels { get; private set; } = new();

    public bool IsLoading
    {
        get => _isLoading;
        set
        {
            if (SetProperty(ref _isLoading, value))
            {
                if (RefreshCommand is RelayCommand relayCommand)
                {
                    relayCommand.RaiseCanExecuteChanged();
                }
            }
        }
    }

    public bool HasError
    {
        get => _hasError;
        set => SetProperty(ref _hasError, value);
    }

    public string ErrorMessage
    {
        get => _errorMessage;
        set => SetProperty(ref _errorMessage, value);
    }

    public bool HasRecentDocuments => RecentDocuments.Count > 0;

    public bool HasEffectivenessChartData => EffectivenessSeries.Count > 0;

    public bool HasMonthlyIssuedChartData => MonthlyIssuedSeries.Count > 0;

    public bool HasDepartmentIssuedChartData => DepartmentIssuedItems.Count > 0;

    public ICommand RefreshCommand { get; }

    public string SelectedMetric
    {
        get => _selectedMetric;
        set
        {
            if (SetProperty(ref _selectedMetric, string.IsNullOrWhiteSpace(value) ? "Total" : value))
            {
                OnPropertyChanged(nameof(SelectedMetricTitle));
                OnPropertyChanged(nameof(SelectedMetricValue));
                OnPropertyChanged(nameof(SelectedMetricDetail));
            }
        }
    }

    public double IssuedPercent => GetPercent(IssuedDocuments);

    public double EffectivePercent => GetPercent(EffectiveDocuments);

    public double ExpiredPercent => GetPercent(ExpiredDocuments);

    public string IssuedPercentText => FormatPercent(IssuedPercent);

    public string EffectivePercentText => FormatPercent(EffectivePercent);

    public string ExpiredPercentText => FormatPercent(ExpiredPercent);

    public string SelectedMetricTitle => SelectedMetric switch
    {
        "Issued" => "Phát hành",
        "Effective" => "Còn hiệu lực",
        "Expired" => "Hết hiệu lực",
        _ => "Tổng số"
    };

    public string SelectedMetricValue => SelectedMetric switch
    {
        "Issued" => IssuedDocuments.ToString("N0"),
        "Effective" => EffectiveDocuments.ToString("N0"),
        "Expired" => ExpiredDocuments.ToString("N0"),
        _ => TotalDocuments.ToString("N0")
    };

    public string SelectedMetricDetail => SelectedMetric switch
    {
        "Issued" => $"{IssuedPercentText} trong tổng số văn bản",
        "Effective" => $"{EffectivePercentText} còn hiệu lực",
        "Expired" => $"{ExpiredPercentText} hết hiệu lực",
        _ => "100% tổng hồ sơ đang theo dõi"
    };

    public DashboardViewModel(ApiService apiService)
    {
        _apiService = apiService;
        RefreshCommand = new RelayCommand(async _ => await LoadAsync(), _ => !IsLoading);
    }

    public async Task LoadAsync()
    {
        if (IsLoading)
        {
            return;
        }

        try
        {
            IsLoading = true;
            HasError = false;
            ErrorMessage = string.Empty;

            var dashboard = await _apiService.GetDashboardAsync();

            ApplySummary(dashboard.Summary);
            LoadRecentDocuments(dashboard.RecentDocuments);
            BuildEffectivenessChart(dashboard.EffectivenessChart);
            BuildMonthlyIssuedChart(dashboard.MonthlyIssuedChart);
            BuildDepartmentIssuedChart(dashboard.DepartmentIssuedChart);
        }
        catch (Exception ex)
        {
            ResetDashboardData();

            HasError = true;
            ErrorMessage = $"Không thể tải dữ liệu dashboard từ API. Chi tiết: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
            RefreshDerivedProperties();
        }
    }

    private void ApplySummary(DashboardSummaryDto summary)
    {
        TotalDocuments = summary.TotalDocuments;
        IssuedDocuments = summary.IssuedDocuments;
        EffectiveDocuments = summary.EffectiveDocuments;
        ExpiredDocuments = summary.ExpiredDocuments;
        TopIssuingDepartment = string.IsNullOrWhiteSpace(summary.TopIssuingDepartment)
            ? "Chưa có dữ liệu"
            : summary.TopIssuingDepartment;
        TopIssuingDepartmentCount = summary.TopIssuingDepartmentCount;
        AverageIssuedPerMonth = summary.AverageIssuedPerMonth;
    }

    private void LoadRecentDocuments(IReadOnlyList<RecentDocumentDto> documents)
    {
        RecentDocuments.Clear();

        foreach (var item in documents)
        {
            RecentDocuments.Add(new RecentDocumentItem
            {
                Id = item.Id,
                DocumentNumber = item.DocumentNumber,
                Title = item.Title,
                StatusText = item.StatusText,
                UrgencyText = item.UrgencyText,
                DocumentDate = item.DocumentDate,
                DueDate = item.DueDate,
                UpdatedAt = item.UpdatedAt
            });
        }

        OnPropertyChanged(nameof(HasRecentDocuments));
    }

    private void BuildEffectivenessChart(IReadOnlyList<DashboardChartItemDto> items)
    {
        EffectivenessSeries.Clear();

        foreach (var item in items.Where(item => item.Value > 0))
        {
            var title = item.Name;

            EffectivenessSeries.Add(new PieSeries
            {
                Title = title,
                Values = new ChartValues<int> { item.Value },
                DataLabels = true,
                LabelPoint = chartPoint =>
                    $"{title}: {chartPoint.Y:0} ({chartPoint.Participation:P0})"
            });
        }

        OnPropertyChanged(nameof(HasEffectivenessChartData));
    }

    private void BuildMonthlyIssuedChart(IReadOnlyList<DashboardChartItemDto> items)
    {
        MonthlyIssuedSeries.Clear();

        MonthlyIssuedLabels = items
            .Select(item => item.Name)
            .ToList();

        MonthlyIssuedSeries.Add(new ColumnSeries
        {
            Title = "Văn bản ban hành",
            Values = new ChartValues<int>(items.Select(item => item.Value)),
            DataLabels = true
        });

        OnPropertyChanged(nameof(MonthlyIssuedLabels));
        OnPropertyChanged(nameof(HasMonthlyIssuedChartData));
    }

    private void BuildDepartmentIssuedChart(IReadOnlyList<DashboardChartItemDto> items)
    {
        DepartmentIssuedSeries.Clear();
        DepartmentIssuedItems.Clear();

        DepartmentIssuedLabels = items
            .Select(item => item.Name)
            .ToList();

        foreach (var item in items.Where(item => item.Value > 0))
        {
            DepartmentIssuedItems.Add(new DashboardChartItemDto
            {
                Name = item.Name,
                Value = item.Value
            });
        }

        DepartmentIssuedSeries.Add(new ColumnSeries
        {
            Title = "Số văn bản",
            Values = new ChartValues<int>(items.Select(item => item.Value)),
            DataLabels = true
        });

        OnPropertyChanged(nameof(DepartmentIssuedLabels));
        OnPropertyChanged(nameof(DepartmentIssuedItems));
        OnPropertyChanged(nameof(HasDepartmentIssuedChartData));
    }

    private void ResetDashboardData()
    {
        TotalDocuments = 0;
        IssuedDocuments = 0;
        EffectiveDocuments = 0;
        ExpiredDocuments = 0;
        TopIssuingDepartment = "Chưa có dữ liệu";
        TopIssuingDepartmentCount = 0;
        AverageIssuedPerMonth = 0;

        RecentDocuments.Clear();
        EffectivenessSeries.Clear();
        MonthlyIssuedSeries.Clear();
        DepartmentIssuedSeries.Clear();
        DepartmentIssuedItems.Clear();

        MonthlyIssuedLabels = new List<string>();
        DepartmentIssuedLabels = new List<string>();

        RefreshDerivedProperties();
    }

    private void RefreshDerivedProperties()
    {
        OnPropertyChanged(nameof(HasRecentDocuments));
        OnPropertyChanged(nameof(HasEffectivenessChartData));
        OnPropertyChanged(nameof(HasMonthlyIssuedChartData));
        OnPropertyChanged(nameof(HasDepartmentIssuedChartData));
        OnPropertyChanged(nameof(MonthlyIssuedLabels));
        OnPropertyChanged(nameof(DepartmentIssuedLabels));
        OnPropertyChanged(nameof(DepartmentIssuedItems));
        RefreshKpiPercentages();
    }

    private void RefreshKpiPercentages()
    {
        OnPropertyChanged(nameof(IssuedPercent));
        OnPropertyChanged(nameof(EffectivePercent));
        OnPropertyChanged(nameof(ExpiredPercent));
        OnPropertyChanged(nameof(IssuedPercentText));
        OnPropertyChanged(nameof(EffectivePercentText));
        OnPropertyChanged(nameof(ExpiredPercentText));
        OnPropertyChanged(nameof(SelectedMetricValue));
        OnPropertyChanged(nameof(SelectedMetricDetail));
    }

    private double GetPercent(int value)
    {
        if (TotalDocuments <= 0)
        {
            return 0;
        }

        return Math.Clamp(value / (double)TotalDocuments * 100.0, 0, 100);
    }

    private static string FormatPercent(double value)
    {
        return $"{value:0}%";
    }
}

public class RecentDocumentItem
{
    public long Id { get; set; }

    public string DocumentNumber { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string StatusText { get; set; } = string.Empty;

    public string UrgencyText { get; set; } = string.Empty;

    public string? DueDate { get; set; }

    public string DocumentDate { get; set; } = string.Empty;

    public DateTime UpdatedAt { get; set; }
}
