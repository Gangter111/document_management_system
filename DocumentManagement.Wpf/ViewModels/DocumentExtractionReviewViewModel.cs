using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Windows.Input;
using DocumentManagement.Intelligence.Models;
using DocumentManagement.Intelligence.Review;
using DocumentManagement.Wpf.Commands;
using DocumentManagement.Wpf.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Win32;

namespace DocumentManagement.Wpf.ViewModels;

public sealed class DocumentExtractionReviewViewModel : BaseViewModel
{
    private readonly IntelligenceReviewService _reviewService;
    private readonly INotificationService _notificationService;
    private readonly ReviewRuntimeCoordinator _runtimeCoordinator = new();

    private string _selectedFilePath = string.Empty;
    private string _debugReport = string.Empty;
    private bool _isLoading;
    private ReviewFieldItemViewModel? _selectedField;
    private ReviewCandidateItemViewModel? _selectedAcceptedCandidate;
    private ReviewCandidateItemViewModel? _selectedRejectedCandidate;
    private ReviewPageOverlay? _currentPageOverlay;
    private string? _activeOverlayRegionId;
    private string? _activeSemanticGroupId;
    private int _currentPageIndex;
    private bool _showAcceptedOverlays = true;
    private bool _showRejectedOverlays = true;
    private bool _showLowConfidenceOnly;
    private bool _isDebugOverlayEnabled;
    private IReadOnlyList<ReviewPageOverlay> _pageOverlays = Array.Empty<ReviewPageOverlay>();
    private int[] _orderedPageIndexes = Array.Empty<int>();
    private Dictionary<int, ReviewPageOverlay> _pageOverlayByIndex = new();
    private Dictionary<string, OverlayFocus> _overlayFocusBySelectionKey = new(StringComparer.Ordinal);

    public DocumentExtractionReviewViewModel(
        IntelligenceReviewService reviewService,
        IConfiguration configuration,
        INotificationService notificationService)
    {
        _reviewService = reviewService;
        _notificationService = notificationService;
        IsFeatureEnabled = configuration.GetValue("Features:IntelligenceReviewEnabled", false);

        OpenFileCommand = new RelayCommand(async _ => await OpenFileAsync(), _ => IsFeatureEnabled && !IsLoading);
        PreviousPageCommand = new RelayCommand(_ => NavigatePage(-1), _ => CanNavigatePage(-1));
        NextPageCommand = new RelayCommand(_ => NavigatePage(1), _ => CanNavigatePage(1));
    }

    public bool IsFeatureEnabled { get; }

    public string SelectedFilePath
    {
        get => _selectedFilePath;
        private set => SetProperty(ref _selectedFilePath, value);
    }

    public string DebugReport
    {
        get => _debugReport;
        private set => SetProperty(ref _debugReport, value);
    }

    public bool IsLoading
    {
        get => _isLoading;
        private set
        {
            if (SetProperty(ref _isLoading, value))
            {
                (OpenFileCommand as RelayCommand)?.RaiseCanExecuteChanged();
            }
        }
    }

    public ObservableCollection<ReviewFieldItemViewModel> Fields { get; } = new ResettableObservableCollection<ReviewFieldItemViewModel>();

    public ObservableCollection<ReviewCandidateItemViewModel> AcceptedCandidates { get; } = new ResettableObservableCollection<ReviewCandidateItemViewModel>();

    public ObservableCollection<ReviewCandidateItemViewModel> RejectedCandidates { get; } = new ResettableObservableCollection<ReviewCandidateItemViewModel>();

    public IReadOnlyList<ReviewPageOverlay> PageOverlays => _pageOverlays;

    public IReadOnlyList<ReviewRuntimeDiagnostic> RuntimeDiagnostics => _runtimeCoordinator.Diagnostics;

    public ReviewFieldItemViewModel? SelectedField
    {
        get => _selectedField;
        set
        {
            if (SetProperty(ref _selectedField, value) && value is not null)
            {
                NavigateToOverlay(value.PageIndex, value.Name, value.SourceBoundingBox, value.Status);
            }
        }
    }

    public ReviewCandidateItemViewModel? SelectedAcceptedCandidate
    {
        get => _selectedAcceptedCandidate;
        set
        {
            if (SetProperty(ref _selectedAcceptedCandidate, value) && value is not null)
            {
                NavigateToOverlay(value.FocusPageIndex, value.FieldName, value.FocusBoundingBox, value.Status);
            }
        }
    }

    public ReviewCandidateItemViewModel? SelectedRejectedCandidate
    {
        get => _selectedRejectedCandidate;
        set
        {
            if (SetProperty(ref _selectedRejectedCandidate, value) && value is not null)
            {
                NavigateToOverlay(value.FocusPageIndex, value.FieldName, value.FocusBoundingBox, value.Status);
            }
        }
    }

    public ReviewPageOverlay? CurrentPageOverlay
    {
        get => _currentPageOverlay;
        private set => SetProperty(ref _currentPageOverlay, value);
    }

    public string? ActiveOverlayRegionId
    {
        get => _activeOverlayRegionId;
        private set => SetProperty(ref _activeOverlayRegionId, value);
    }

    public string? ActiveSemanticGroupId
    {
        get => _activeSemanticGroupId;
        private set => SetProperty(ref _activeSemanticGroupId, value);
    }

    public int CurrentPageIndex
    {
        get => _currentPageIndex;
        private set
        {
            if (SetProperty(ref _currentPageIndex, value))
            {
                CurrentPageOverlay = _pageOverlayByIndex.GetValueOrDefault(value);
                OnPropertyChanged(nameof(CurrentPageNumber));
                OnPropertyChanged(nameof(PageDisplayText));
                RaisePageCommandState();
            }
        }
    }

    public int CurrentPageNumber => CurrentPageIndex + 1;

    public string PageDisplayText => _orderedPageIndexes.Length == 0
        ? "Page 0 / 0"
        : $"Page {CurrentPageNumber} / {_orderedPageIndexes.Length}";

    public bool ShowAcceptedOverlays
    {
        get => _showAcceptedOverlays;
        set
        {
            if (SetProperty(ref _showAcceptedOverlays, value))
            {
                ClearActiveOverlayIfHidden();
            }
        }
    }

    public bool ShowRejectedOverlays
    {
        get => _showRejectedOverlays;
        set
        {
            if (SetProperty(ref _showRejectedOverlays, value))
            {
                ClearActiveOverlayIfHidden();
            }
        }
    }

    public bool ShowLowConfidenceOnly
    {
        get => _showLowConfidenceOnly;
        set
        {
            if (SetProperty(ref _showLowConfidenceOnly, value))
            {
                ClearActiveOverlayIfHidden();
            }
        }
    }

    public bool IsDebugOverlayEnabled
    {
        get => _isDebugOverlayEnabled;
        set => SetProperty(ref _isDebugOverlayEnabled, value);
    }

    public ICommand OpenFileCommand { get; }

    public ICommand PreviousPageCommand { get; }

    public ICommand NextPageCommand { get; }

    public void ActivateOverlayRegion(ReviewOverlayRegion region)
    {
        if (!_pageOverlayByIndex.ContainsKey(region.PageIndex))
        {
            ActiveOverlayRegionId = null;
            return;
        }

        CurrentPageIndex = region.PageIndex;
        if (IsRegionVisible(region))
        {
            ActiveOverlayRegionId = region.Id;
            ActiveSemanticGroupId = region.SemanticGroupId;
        }
        else
        {
            ClearActiveOverlay();
        }
    }

    private async Task OpenFileAsync()
    {
        if (!IsFeatureEnabled)
        {
            _notificationService.ShowWarning(
                "Chuc nang review semantic extraction dang tat.",
                "Document Intelligence");
            return;
        }

        var dialog = new OpenFileDialog
        {
            Title = "Chon MinerU content_list_v2 JSON",
            Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*"
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        await LoadFileAsync(dialog.FileName);
    }

    private async Task LoadFileAsync(string filePath)
    {
        using var refresh = _runtimeCoordinator.BeginRefresh();
        try
        {
            IsLoading = true;
            SelectedFilePath = filePath;

            var review = await _reviewService.LoadMinerUExtractionAsync(filePath, refresh.Token);
            if (!_runtimeCoordinator.RejectIfStale(refresh, "stale-refresh-rejected").ShouldApply)
            {
                return;
            }

            Apply(review, refresh.Version);
            _runtimeCoordinator.Complete(refresh);
        }
        catch (OperationCanceledException)
        {
            _runtimeCoordinator.Complete(refresh, canceled: true);
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or System.Text.Json.JsonException or UnauthorizedAccessException)
        {
            if (!_runtimeCoordinator.RejectIfStale(refresh, "stale-refresh-error-discarded").ShouldApply)
            {
                return;
            }

            _notificationService.ShowError(
                $"Khong the doc MinerU JSON: {ex.Message}",
                "Document Intelligence");
        }
        finally
        {
            if (_runtimeCoordinator.IsCurrent(refresh.Version))
            {
                IsLoading = false;
            }
        }
    }

    private void Apply(ReviewDocumentExtraction review, long refreshVersion = 0)
    {
        if (refreshVersion > 0 && !_runtimeCoordinator.RejectIfStale(refreshVersion, "stale-refresh-rejected").ShouldApply)
        {
            return;
        }

        var previousFocus = CaptureCurrentFocus();
        var fields = review.Fields.Select(field => new ReviewFieldItemViewModel(field)).ToArray();
        var acceptedCandidates = review.AcceptedCandidates.Select(candidate => new ReviewCandidateItemViewModel(candidate)).ToArray();
        var rejectedCandidates = review.RejectedCandidates.Select(candidate => new ReviewCandidateItemViewModel(candidate)).ToArray();

        if (refreshVersion > 0 && !_runtimeCoordinator.RejectIfStale(refreshVersion, "replay-invalidation").ShouldApply)
        {
            return;
        }

        Replace(Fields, fields);
        Replace(AcceptedCandidates, acceptedCandidates);
        Replace(RejectedCandidates, rejectedCandidates);

        _pageOverlays = review.PageOverlays;
        _orderedPageIndexes = review.PageOverlays.Select(page => page.PageIndex).OrderBy(pageIndex => pageIndex).ToArray();
        _pageOverlayByIndex = review.PageOverlays.ToDictionary(page => page.PageIndex);
        _overlayFocusBySelectionKey = BuildOverlayIndex(review.PageOverlays);
        OnPropertyChanged(nameof(PageOverlays));

        SelectedField = null;
        SelectedAcceptedCandidate = null;
        SelectedRejectedCandidate = null;
        RestoreOrInitializeFocus(previousFocus, refreshVersion);
        DebugReport = review.DebugReport;
        OnPropertyChanged(nameof(PageDisplayText));
        RaisePageCommandState();
    }

    private static void Replace<T>(ObservableCollection<T> target, IEnumerable<T> source)
    {
        if (target is ResettableObservableCollection<T> resettable)
        {
            resettable.ReplaceAll(source);
            return;
        }

        target.Clear();
        foreach (var item in source)
        {
            target.Add(item);
        }
    }

    private void NavigateToOverlay(int pageIndex, string fieldName, BoundingBox? sourceBoundingBox, string status)
    {
        if (sourceBoundingBox is null)
        {
            ClearActiveOverlay();
            return;
        }

        CurrentPageIndex = Math.Max(0, pageIndex);

        var statusType = status switch
        {
            nameof(ReviewStatus.Accepted) => OverlayHighlightType.Accepted,
            nameof(ReviewStatus.Rejected) => OverlayHighlightType.Rejected,
            _ => OverlayHighlightType.LowConfidence
        };

        if (_overlayFocusBySelectionKey.TryGetValue(CreateOverlayKey(CurrentPageIndex, fieldName, statusType, sourceBoundingBox), out var focus))
        {
            CurrentPageIndex = focus.PageIndex;
            ActiveOverlayRegionId = focus.RegionId;
            ActiveSemanticGroupId = focus.SemanticGroupId;
        }
        else
        {
            ClearActiveOverlay();
        }

        ClearActiveOverlayIfHidden();
    }

    private void NavigatePage(int delta)
    {
        var currentPosition = Array.IndexOf(_orderedPageIndexes, CurrentPageIndex);
        var nextPosition = currentPosition + delta;
        if (nextPosition < 0 || nextPosition >= _orderedPageIndexes.Length)
        {
            return;
        }

        CurrentPageIndex = _orderedPageIndexes[nextPosition];
        if (ActiveOverlayRegionId is not null && !CurrentPageContainsActiveRegion())
        {
            ClearActiveOverlay();
        }
    }

    private bool CanNavigatePage(int delta)
    {
        var currentPosition = Array.IndexOf(_orderedPageIndexes, CurrentPageIndex);
        return currentPosition >= 0 && currentPosition + delta >= 0 && currentPosition + delta < _orderedPageIndexes.Length;
    }

    private void RaisePageCommandState()
    {
        (PreviousPageCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (NextPageCommand as RelayCommand)?.RaiseCanExecuteChanged();
    }

    private bool CurrentPageContainsActiveRegion()
    {
        var regions = CurrentPageOverlay?.Regions;
        if (regions is null || ActiveOverlayRegionId is null)
        {
            return false;
        }

        for (var i = 0; i < regions.Count; i++)
        {
            if (string.Equals(regions[i].Id, ActiveOverlayRegionId, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private void ClearActiveOverlayIfHidden()
    {
        if (ActiveOverlayRegionId is null)
        {
            ActiveSemanticGroupId = null;
            return;
        }

        var regions = CurrentPageOverlay?.Regions;
        if (regions is null)
        {
            ClearActiveOverlay();
            return;
        }

        for (var i = 0; i < regions.Count; i++)
        {
            var region = regions[i];
            if (string.Equals(region.Id, ActiveOverlayRegionId, StringComparison.Ordinal))
            {
                if (!IsRegionVisible(region))
                {
                    ClearActiveOverlay();
                }

                return;
            }
        }

        ClearActiveOverlay();
    }

    private bool IsRegionVisible(ReviewOverlayRegion region)
    {
        if (ShowLowConfidenceOnly)
        {
            return region.HighlightType == OverlayHighlightType.LowConfidence;
        }

        return region.HighlightType switch
        {
            OverlayHighlightType.Accepted => ShowAcceptedOverlays,
            OverlayHighlightType.LowConfidence => ShowAcceptedOverlays,
            OverlayHighlightType.Rejected => ShowRejectedOverlays,
            _ => false
        };
    }

    private void ClearActiveOverlay()
    {
        ActiveOverlayRegionId = null;
        ActiveSemanticGroupId = null;
    }

    private ReviewOverlayFocusState? CaptureCurrentFocus()
    {
        if (ActiveOverlayRegionId is null)
        {
            return null;
        }

        foreach (var page in _pageOverlays)
        {
            for (var i = 0; i < page.Regions.Count; i++)
            {
                if (string.Equals(page.Regions[i].Id, ActiveOverlayRegionId, StringComparison.Ordinal))
                {
                    return ReviewOverlayFocusState.FromRegion(page.Regions[i]);
                }
            }
        }

        return null;
    }

    private void RestoreOrInitializeFocus(ReviewOverlayFocusState? previousFocus, long refreshVersion = 0)
    {
        if (refreshVersion > 0 && !_runtimeCoordinator.RejectIfStale(refreshVersion, "focus-restoration-invalidation").ShouldApply)
        {
            return;
        }

        ClearActiveOverlay();
        if (_orderedPageIndexes.Length == 0)
        {
            CurrentPageIndex = 0;
            CurrentPageOverlay = null;
            return;
        }

        var navigator = new ReviewOverlayNavigator(_pageOverlays);
        var replay = navigator.RestoreFocusWithReplay(previousFocus);
        if (refreshVersion > 0 && !_runtimeCoordinator.IsCurrent(refreshVersion))
        {
            return;
        }

        if (replay.Succeeded)
        {
            CurrentPageIndex = navigator.CurrentPageIndex;
            ActiveOverlayRegionId = navigator.ActiveRegionId;
            ActiveSemanticGroupId = navigator.ActiveSemanticGroupId;
            return;
        }

        CurrentPageIndex = _orderedPageIndexes[0];
        CurrentPageOverlay = _pageOverlayByIndex.GetValueOrDefault(CurrentPageIndex);
    }

    private static Dictionary<string, OverlayFocus> BuildOverlayIndex(IReadOnlyList<ReviewPageOverlay> pages)
    {
        var index = new Dictionary<string, OverlayFocus>(StringComparer.Ordinal);
        for (var pageIndex = 0; pageIndex < pages.Count; pageIndex++)
        {
            var regions = pages[pageIndex].Regions;
            for (var regionIndex = 0; regionIndex < regions.Count; regionIndex++)
            {
                var region = regions[regionIndex];
                var focus = new OverlayFocus(region.Id, region.SemanticGroupId, region.PageIndex);
                var key = CreateOverlayKey(region.PageIndex, region.FieldName, region.HighlightType, region.BoundingBox);
                if (region.IsPrimary)
                {
                    index[key] = focus;
                }
                else
                {
                    index.TryAdd(key, focus);
                }
            }
        }

        return index;
    }

    private static string CreateOverlayKey(int pageIndex, string fieldName, OverlayHighlightType highlightType, BoundingBox boundingBox) =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"{pageIndex}|{fieldName}|{highlightType}|{boundingBox.X1:0.###}|{boundingBox.Y1:0.###}|{boundingBox.X2:0.###}|{boundingBox.Y2:0.###}");

    private sealed record OverlayFocus(string RegionId, string SemanticGroupId, int PageIndex);
}

internal sealed class ResettableObservableCollection<T> : ObservableCollection<T>
{
    public void ReplaceAll(IEnumerable<T> items)
    {
        Items.Clear();
        foreach (var item in items)
        {
            Items.Add(item);
        }

        OnPropertyChanged(new PropertyChangedEventArgs(nameof(Count)));
        OnPropertyChanged(new PropertyChangedEventArgs("Item[]"));
        OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
    }
}
