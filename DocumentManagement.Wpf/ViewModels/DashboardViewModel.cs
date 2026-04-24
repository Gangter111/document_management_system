using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows.Input;
using DocumentManagement.Application.Interfaces;
using DocumentManagement.Application.Models;
using DocumentManagement.Wpf.Commands;
using LiveCharts;
using LiveCharts.Wpf;

namespace DocumentManagement.Wpf.ViewModels
{
    public class DashboardViewModel : BaseViewModel
    {
        private readonly IDashboardRepository _dashboardRepository;

        private int _totalDocuments;
        private int _draftDocuments;
        private int _pendingApprovalDocuments;
        private int _issuedDocuments;
        private int _archivedDocuments;
        private int _rejectedDocuments;
        private int _overdueDocuments;

        private bool _isLoading;
        private bool _hasError;
        private string _errorMessage = string.Empty;

        public int TotalDocuments
        {
            get => _totalDocuments;
            set => SetProperty(ref _totalDocuments, value);
        }

        public int DraftDocuments
        {
            get => _draftDocuments;
            set => SetProperty(ref _draftDocuments, value);
        }

        public int PendingApprovalDocuments
        {
            get => _pendingApprovalDocuments;
            set => SetProperty(ref _pendingApprovalDocuments, value);
        }

        public int IssuedDocuments
        {
            get => _issuedDocuments;
            set => SetProperty(ref _issuedDocuments, value);
        }

        public int ArchivedDocuments
        {
            get => _archivedDocuments;
            set => SetProperty(ref _archivedDocuments, value);
        }

        public int RejectedDocuments
        {
            get => _rejectedDocuments;
            set => SetProperty(ref _rejectedDocuments, value);
        }

        public int OverdueDocuments
        {
            get => _overdueDocuments;
            set => SetProperty(ref _overdueDocuments, value);
        }

        public ObservableCollection<RecentDocumentItem> RecentDocuments { get; } = new();

        public SeriesCollection StatusSeries { get; } = new SeriesCollection();

        public bool IsLoading
        {
            get => _isLoading;
            set => SetProperty(ref _isLoading, value);
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

        public bool HasStatusChartData => StatusSeries.Count > 0;

        public ICommand RefreshCommand { get; }

        public DashboardViewModel(IDashboardRepository dashboardRepository)
        {
            _dashboardRepository = dashboardRepository ?? throw new ArgumentNullException(nameof(dashboardRepository));

            RefreshCommand = new RelayCommand(async _ => await LoadAsync(), _ => !IsLoading);
        }

        public async Task LoadAsync()
        {
            if (IsLoading)
                return;

            try
            {
                IsLoading = true;
                HasError = false;
                ErrorMessage = string.Empty;

                var summaryTask = _dashboardRepository.GetSummaryAsync();
                var recentDocumentsTask = _dashboardRepository.GetRecentDocumentsAsync(10);
                var statusBreakdownTask = _dashboardRepository.GetStatusBreakdownAsync();

                await Task.WhenAll(summaryTask, recentDocumentsTask, statusBreakdownTask);

                var summary = await summaryTask;
                var recentDocuments = await recentDocumentsTask;
                var statusBreakdown = await statusBreakdownTask;

                TotalDocuments = summary.TotalDocuments;
                DraftDocuments = summary.DraftDocuments;
                PendingApprovalDocuments = summary.PendingApprovalDocuments;
                IssuedDocuments = summary.IssuedDocuments;
                ArchivedDocuments = summary.ArchivedDocuments;
                RejectedDocuments = summary.RejectedDocuments;
                OverdueDocuments = summary.OverdueDocuments;

                BuildStatusChart(statusBreakdown);
                LoadRecentDocuments(recentDocuments);
            }
            catch (Exception ex)
            {
                ResetDashboardData();

                HasError = true;
                ErrorMessage = $"Không thể tải dữ liệu dashboard. Chi tiết: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
                OnPropertyChanged(nameof(HasRecentDocuments));
                OnPropertyChanged(nameof(HasStatusChartData));

                if (RefreshCommand is RelayCommand relayCommand)
                {
                    relayCommand.RaiseCanExecuteChanged();
                }
            }
        }

        private void LoadRecentDocuments(IReadOnlyList<RecentDocumentItem> documents)
        {
            RecentDocuments.Clear();

            foreach (var item in documents)
            {
                RecentDocuments.Add(item);
            }

            OnPropertyChanged(nameof(HasRecentDocuments));
        }

        private void BuildStatusChart(IReadOnlyList<DashboardStatusChartItem> items)
        {
            StatusSeries.Clear();

            foreach (var item in items)
            {
                if (item.Value <= 0)
                    continue;

                var title = item.Name;

                StatusSeries.Add(new PieSeries
                {
                    Title = title,
                    Values = new ChartValues<int> { item.Value },
                    DataLabels = true,
                    LabelPoint = chartPoint =>
                        $"{title}: {chartPoint.Y:0} ({chartPoint.Participation:P0})"
                });
            }

            OnPropertyChanged(nameof(HasStatusChartData));
        }

        private void ResetDashboardData()
        {
            TotalDocuments = 0;
            DraftDocuments = 0;
            PendingApprovalDocuments = 0;
            IssuedDocuments = 0;
            ArchivedDocuments = 0;
            RejectedDocuments = 0;
            OverdueDocuments = 0;

            StatusSeries.Clear();
            RecentDocuments.Clear();

            OnPropertyChanged(nameof(HasRecentDocuments));
            OnPropertyChanged(nameof(HasStatusChartData));
        }
    }
}