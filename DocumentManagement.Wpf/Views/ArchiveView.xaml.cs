using System.Windows.Controls;
using System.Windows.Input;
using DocumentManagement.Wpf.ViewModels;

namespace DocumentManagement.Wpf.Views;

public partial class ArchiveView : UserControl
{
    public ArchiveView()
    {
        InitializeComponent();
    }

    private void DocumentsDataGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is ArchiveViewModel vm)
        {
            if (vm.OpenSelectedDocumentCommand.CanExecute(null))
            {
                vm.OpenSelectedDocumentCommand.Execute(null);
            }
        }
    }
}
