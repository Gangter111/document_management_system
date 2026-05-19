using System.Windows;
using DocumentManagement.Intelligence.Review;
using DocumentManagement.Wpf.ViewModels;

namespace DocumentManagement.Wpf.Views;

public partial class DocumentExtractionReviewWindow : Window
{
    public DocumentExtractionReviewWindow(DocumentExtractionReviewViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        OverlayCanvas.RegionClicked += OnOverlayRegionClicked;
        Closed += OnClosed;
    }

    private void OnOverlayRegionClicked(object? sender, ReviewOverlayRegion region)
    {
        if (DataContext is DocumentExtractionReviewViewModel viewModel)
        {
            viewModel.ActivateOverlayRegion(region);
        }
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        OverlayCanvas.RegionClicked -= OnOverlayRegionClicked;
        Closed -= OnClosed;
    }
}
