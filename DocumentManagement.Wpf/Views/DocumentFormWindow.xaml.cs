using System.Windows;

namespace DocumentManagement.Wpf.Views;

public partial class DocumentFormWindow : Window
{
    public DocumentFormWindow(ViewModels.DocumentFormViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}