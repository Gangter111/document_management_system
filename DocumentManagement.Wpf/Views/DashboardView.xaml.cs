using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using DocumentManagement.Wpf.ViewModels;

namespace DocumentManagement.Wpf.Views;

public partial class DashboardView : UserControl
{
    private bool _isLoaded;

    public DashboardView()
    {
        InitializeComponent();
        Loaded += DashboardView_Loaded;
    }

    private async void DashboardView_Loaded(object sender, RoutedEventArgs e)
    {
        if (_isLoaded)
        {
            return;
        }

        _isLoaded = true;

        if (DataContext is DashboardViewModel vm)
        {
            await vm.LoadAsync();
        }
    }

    private async void RecentDocumentsGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is not DataGrid grid)
        {
            return;
        }

        if (grid.SelectedItem is not RecentDocumentItem item)
        {
            return;
        }

        if (Application.Current?.MainWindow?.DataContext is not MainViewModel mainVm)
        {
            return;
        }

        await mainVm.OpenDocumentAsync(item.Id);
    }
}