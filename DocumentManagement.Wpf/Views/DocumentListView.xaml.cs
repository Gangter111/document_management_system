using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using DocumentManagement.Wpf.ViewModels;

namespace DocumentManagement.Wpf.Views
{
    public partial class DocumentListView : UserControl
    {
        public DocumentListView()
        {
            InitializeComponent();
            Loaded += (_, _) => DocumentsDataGrid.Focus();
            PreviewKeyDown += DocumentListView_PreviewKeyDown;
        }

        private async void DocumentsDataGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (DataContext is not DocumentListViewModel vm)
                return;

            await vm.OpenSelectedDocumentAsync();
        }

        private void DocumentListView_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (DataContext is not DocumentListViewModel vm)
            {
                return;
            }

            if (e.Key == Key.Escape && SearchBox.IsKeyboardFocusWithin && !IsImeOrDeadKey(e) && !HasOpenPopupControl(this))
            {
                ClearWorkspaceSelection(vm);
                e.Handled = true;
                return;
            }

            if (ShouldSkipWorkspaceShortcut(e))
            {
                return;
            }

            if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.F)
            {
                SearchBox.Focus();
                SearchBox.SelectAll();
                e.Handled = true;
                return;
            }

            if (e.Key == Key.F5 && vm.RefreshCommand.CanExecute(null))
            {
                vm.RefreshCommand.Execute(null);
                e.Handled = true;
                return;
            }

            if (Keyboard.Modifiers == ModifierKeys.Alt && e.Key == Key.A && vm.ArchiveSelectedCommand.CanExecute(null))
            {
                vm.ArchiveSelectedCommand.Execute(null);
                e.Handled = true;
                return;
            }

            if (e.Key == Key.Enter && vm.OpenSelectedDocumentCommand.CanExecute(null))
            {
                vm.OpenSelectedDocumentCommand.Execute(null);
                e.Handled = true;
                return;
            }

            if (e.Key == Key.Space && vm.SelectedDocument != null)
            {
                vm.SelectedDocument.IsBatchSelected = !vm.SelectedDocument.IsBatchSelected;
                e.Handled = true;
                return;
            }

            if (e.Key == Key.Escape)
            {
                ClearWorkspaceSelection(vm);
                e.Handled = true;
                return;
            }

            if (Keyboard.Modifiers == ModifierKeys.None && e.Key is Key.J or Key.K)
            {
                MoveGridSelection(e.Key == Key.J ? 1 : -1);
                e.Handled = true;
            }
        }

        private void MoveGridSelection(int delta)
        {
            if (DocumentsDataGrid.Items.Count == 0)
            {
                return;
            }

            var index = DocumentsDataGrid.SelectedIndex;
            if (index < 0)
            {
                index = 0;
            }
            else
            {
                index = Math.Clamp(index + delta, 0, DocumentsDataGrid.Items.Count - 1);
            }

            DocumentsDataGrid.SelectedIndex = index;
            DocumentsDataGrid.ScrollIntoView(DocumentsDataGrid.SelectedItem);
            DocumentsDataGrid.Focus();
        }

        private bool ShouldSkipWorkspaceShortcut(KeyEventArgs e)
        {
            if (IsImeOrDeadKey(e))
            {
                return true;
            }

            return IsEditingOrPopupContext(e.OriginalSource as DependencyObject)
                || IsEditingOrPopupContext(Keyboard.FocusedElement as DependencyObject)
                || HasOpenPopupControl(this);
        }

        private void ClearWorkspaceSelection(DocumentListViewModel vm)
        {
            vm.SearchText = null;
            if (vm.ClearBatchCommand.CanExecute(null))
            {
                vm.ClearBatchCommand.Execute(null);
            }

            DocumentsDataGrid.Focus();
        }

        private static bool IsImeOrDeadKey(KeyEventArgs e)
        {
            return e.Key is Key.ImeProcessed or Key.DeadCharProcessed || e.ImeProcessedKey != Key.None || e.DeadCharProcessedKey != Key.None;
        }

        private static bool IsEditingOrPopupContext(DependencyObject? source)
        {
            while (source != null)
            {
                if (source is TextBoxBase or PasswordBox)
                {
                    return true;
                }

                if (source is ComboBox comboBox && comboBox.IsKeyboardFocusWithin)
                {
                    return true;
                }

                if (source is ComboBox { IsDropDownOpen: true })
                {
                    return true;
                }

                if (source is DatePicker datePicker && datePicker.IsKeyboardFocusWithin)
                {
                    return true;
                }

                if (source is DatePicker { IsDropDownOpen: true })
                {
                    return true;
                }

                if (source is Popup popup && popup.IsOpen)
                {
                    return true;
                }

                if (source is DataGridCell { IsEditing: true })
                {
                    return true;
                }

                source = GetParent(source);
            }

            return false;
        }

        private static DependencyObject? GetParent(DependencyObject source)
        {
            if (source is Popup popup)
            {
                return popup.PlacementTarget;
            }

            try
            {
                var visualParent = VisualTreeHelper.GetParent(source);
                if (visualParent != null)
                {
                    return visualParent;
                }
            }
            catch (InvalidOperationException)
            {
            }

            var logicalParent = LogicalTreeHelper.GetParent(source);
            if (logicalParent != null)
            {
                return logicalParent;
            }

            return source switch
            {
                FrameworkElement { Parent: not null } element => element.Parent,
                FrameworkElement { TemplatedParent: DependencyObject templatedParent } => templatedParent,
                FrameworkContentElement { Parent: not null } contentElement => contentElement.Parent,
                FrameworkContentElement { TemplatedParent: DependencyObject templatedParent } => templatedParent,
                _ => null
            };
        }

        private static bool HasOpenPopupControl(DependencyObject root)
        {
            if (root is ComboBox { IsDropDownOpen: true } || root is DatePicker { IsDropDownOpen: true })
            {
                return true;
            }

            int childCount;
            try
            {
                childCount = VisualTreeHelper.GetChildrenCount(root);
            }
            catch (InvalidOperationException)
            {
                return false;
            }

            for (var i = 0; i < childCount; i++)
            {
                if (HasOpenPopupControl(VisualTreeHelper.GetChild(root, i)))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
