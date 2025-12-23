using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using FolderIconManager.GUI.Models;

namespace FolderIconManager.GUI;

public partial class MainWindow : Window
{
    private MainViewModel? _viewModel;

    public MainWindow()
    {
        InitializeComponent();
        
        _viewModel = DataContext as MainViewModel;
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        // Restore window state
        _viewModel?.RestoreWindowState(this);
    }

    private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
    {
        // Save window state
        _viewModel?.SaveWindowState(this);
    }

    /// <summary>
    /// Ensures tree view item is selected on right-click (before context menu opens)
    /// </summary>
    private void TreeViewItem_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is TreeViewItem item)
        {
            item.IsSelected = true;
            item.Focus();
            e.Handled = true;
        }
    }

    /// <summary>
    /// Handles tree view selection change and updates the view model
    /// </summary>
    private void TreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (_viewModel != null && e.NewValue is FolderTreeNode node)
        {
            _viewModel.SelectedNode = node;
        }
    }
}
