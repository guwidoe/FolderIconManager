using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
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
        // Set the window icon
        LoadWindowIcon();
        
        // Restore window state
        _viewModel?.RestoreWindowState(this);
        
        // Apply title bar theme based on current theme
        ApplyTitleBarTheme();
        
        // Subscribe to theme changes
        if (_viewModel?.ThemeService != null)
        {
            _viewModel.ThemeService.ThemeChanged += () => ApplyTitleBarTheme();
        }
    }

    private void LoadWindowIcon()
    {
        try
        {
            // Try to load the icon from the output directory
            var iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "AppIcon.ico");
            
            if (File.Exists(iconPath))
            {
                var iconUri = new Uri(iconPath, UriKind.Absolute);
                Icon = BitmapFrame.Create(iconUri);
            }
        }
        catch (Exception ex)
        {
            // Log error but don't crash the app
            System.Diagnostics.Debug.WriteLine($"Failed to load window icon: {ex.Message}");
        }
    }

    private void ApplyTitleBarTheme()
    {
        if (_viewModel?.ThemeService != null)
        {
            _viewModel.ThemeService.ApplyTitleBarTheme(this);
        }
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
            // Check if the click is directly on this item's content, not on a child TreeViewItem
            // This prevents the tunneling event from selecting parent items
            if (!IsClickOnThisItem(item, e))
                return;

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
    /// Checks if the click event is directly on this TreeViewItem's header, not on a child item
    /// </summary>
    private bool IsClickOnThisItem(TreeViewItem item, MouseButtonEventArgs e)
    {
        // Get the element that was actually clicked
        var clickedElement = e.OriginalSource as DependencyObject;
        if (clickedElement == null) return false;

        // Walk up the visual tree from the clicked element
        // If we hit this TreeViewItem before hitting another TreeViewItem, the click is on this item
        while (clickedElement != null)
        {
            if (clickedElement == item)
                return true;

            // If we find a different TreeViewItem first, this click is for a child item
            if (clickedElement is TreeViewItem && clickedElement != item)
                return false;

            clickedElement = VisualTreeHelper.GetParent(clickedElement);
        }

        return false;
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

    /// <summary>
    /// Opens the column visibility context menu
    /// </summary>
    private void ColumnSettings_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.ContextMenu != null)
        {
            button.ContextMenu.PlacementTarget = button;
            button.ContextMenu.IsOpen = true;
        }
    }

    /// <summary>
    /// Resets all columns to their default widths and visibility
    /// </summary>
    private void ResetColumns_Click(object sender, RoutedEventArgs e)
    {
        _viewModel?.ColumnWidths.ResetToDefaults();
    }
}
