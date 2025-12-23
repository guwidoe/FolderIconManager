using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using FolderIconManager.GUI.Models;

namespace FolderIconManager.GUI;

public partial class MainWindow : Window
{
    private MainViewModel? _viewModel;
    private FolderTreeNode? _lastSelectedNode;

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
    /// Handles multi-select with Ctrl+Click
    /// </summary>
    private void TreeViewItem_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is TreeViewItem item && item.DataContext is FolderTreeNode node && _viewModel != null)
        {
            if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
            {
                // Ctrl+Click: Toggle selection
                e.Handled = true;
                
                if (node.IsMultiSelected)
                {
                    node.IsMultiSelected = false;
                    _viewModel.SelectedNodes.Remove(node);
                }
                else
                {
                    node.IsMultiSelected = true;
                    _viewModel.SelectedNodes.Add(node);
                }
                
                _lastSelectedNode = node;
                _viewModel.OnMultiSelectionChanged();
            }
            else if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift) && _lastSelectedNode != null)
            {
                // Shift+Click: Range selection (simplified - just add to selection)
                e.Handled = true;
                node.IsMultiSelected = true;
                _viewModel.SelectedNodes.Add(node);
                _viewModel.OnMultiSelectionChanged();
            }
            else
            {
                // Normal click: Clear multi-selection
                ClearMultiSelection();
                _lastSelectedNode = node;
            }
        }
    }

    /// <summary>
    /// Ensures tree view item is selected on right-click (before context menu opens)
    /// </summary>
    private void TreeViewItem_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is TreeViewItem item && item.DataContext is FolderTreeNode node)
        {
            // If right-clicking on a multi-selected node, keep the selection
            if (!node.IsMultiSelected)
            {
                ClearMultiSelection();
                item.IsSelected = true;
            }
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
            _lastSelectedNode = node;
        }
    }

    private void ClearMultiSelection()
    {
        if (_viewModel == null) return;
        
        foreach (var node in _viewModel.SelectedNodes.ToList())
        {
            node.IsMultiSelected = false;
        }
        _viewModel.SelectedNodes.Clear();
        _viewModel.OnMultiSelectionChanged();
    }
}
