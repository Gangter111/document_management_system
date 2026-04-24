using System.Windows.Controls;
using System.Windows.Input;
using DocumentManagement.Wpf.ViewModels;

namespace DocumentManagement.Wpf.Views
{
    public partial class DocumentListView : UserControl
    {
        public DocumentListView()
        {
            InitializeComponent();
        }

        private async void DocumentsDataGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (DataContext is not DocumentListViewModel vm)
                return;

            await vm.OpenSelectedDocumentAsync();
        }
    }
}