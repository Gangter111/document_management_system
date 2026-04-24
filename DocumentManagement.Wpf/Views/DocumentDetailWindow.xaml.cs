using System.Windows;

namespace DocumentManagement.Wpf.Views;

public partial class DocumentDetailWindow : Window
{
    public DocumentDetailWindow(ViewModels.DocumentDetailViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
