using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;

namespace FolderIconManager.GUI.Controls;

/// <summary>
/// A TreeView that displays items in a multi-column grid format with resizable column headers
/// </summary>
public class TreeListView : TreeView
{
    public static readonly DependencyProperty ColumnsProperty =
        DependencyProperty.Register(nameof(Columns), typeof(TreeListViewColumnCollection),
            typeof(TreeListView), new PropertyMetadata(null));

    public TreeListViewColumnCollection Columns
    {
        get => (TreeListViewColumnCollection)GetValue(ColumnsProperty);
        set => SetValue(ColumnsProperty, value);
    }

    public TreeListView()
    {
        Columns = new TreeListViewColumnCollection();
    }
}

/// <summary>
/// Defines a column in a TreeListView
/// </summary>
public class TreeListViewColumn : DependencyObject, INotifyPropertyChanged
{
    private double _width = 100;
    private double _minWidth = 30;
    private bool _isVisible = true;

    public static readonly DependencyProperty HeaderProperty =
        DependencyProperty.Register(nameof(Header), typeof(object), typeof(TreeListViewColumn));

    public static readonly DependencyProperty CellTemplateProperty =
        DependencyProperty.Register(nameof(CellTemplate), typeof(DataTemplate), typeof(TreeListViewColumn));

    public static readonly DependencyProperty DisplayMemberBindingProperty =
        DependencyProperty.Register(nameof(DisplayMemberBinding), typeof(BindingBase), typeof(TreeListViewColumn));

    public object Header
    {
        get => GetValue(HeaderProperty);
        set => SetValue(HeaderProperty, value);
    }

    public DataTemplate CellTemplate
    {
        get => (DataTemplate)GetValue(CellTemplateProperty);
        set => SetValue(CellTemplateProperty, value);
    }

    public BindingBase DisplayMemberBinding
    {
        get => (BindingBase)GetValue(DisplayMemberBindingProperty);
        set => SetValue(DisplayMemberBindingProperty, value);
    }

    public double Width
    {
        get => _width;
        set
        {
            if (Math.Abs(_width - value) > 0.1)
            {
                _width = Math.Max(value, _minWidth);
                OnPropertyChanged();
                OnPropertyChanged(nameof(ActualWidth));
            }
        }
    }

    public double MinWidth
    {
        get => _minWidth;
        set { _minWidth = value; OnPropertyChanged(); }
    }

    public bool IsVisible
    {
        get => _isVisible;
        set
        {
            if (_isVisible != value)
            {
                _isVisible = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ActualWidth));
                OnPropertyChanged(nameof(ColumnVisibility));
            }
        }
    }

    public double ActualWidth => _isVisible ? _width : 0;
    public Visibility ColumnVisibility => _isVisible ? Visibility.Visible : Visibility.Collapsed;

    /// <summary>
    /// Unique identifier for this column (for settings persistence)
    /// </summary>
    public string ColumnId { get; set; } = "";

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

/// <summary>
/// Collection of TreeListView columns
/// </summary>
public class TreeListViewColumnCollection : ObservableCollection<TreeListViewColumn>
{
}

/// <summary>
/// The expander/indentation element for TreeListView items
/// </summary>
public class TreeListViewExpander : Control
{
    public static readonly DependencyProperty LevelProperty =
        DependencyProperty.Register(nameof(Level), typeof(int), typeof(TreeListViewExpander),
            new PropertyMetadata(0, OnLevelChanged));

    public static readonly DependencyProperty IndentSizeProperty =
        DependencyProperty.Register(nameof(IndentSize), typeof(double), typeof(TreeListViewExpander),
            new PropertyMetadata(16.0));

    public int Level
    {
        get => (int)GetValue(LevelProperty);
        set => SetValue(LevelProperty, value);
    }

    public double IndentSize
    {
        get => (double)GetValue(IndentSizeProperty);
        set => SetValue(IndentSizeProperty, value);
    }

    public double TotalIndent => Level * IndentSize;

    private static void OnLevelChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is TreeListViewExpander expander)
        {
            expander.Width = expander.TotalIndent + 20; // +20 for the toggle button
        }
    }
}

/// <summary>
/// Helper to get the nesting level of a TreeViewItem
/// </summary>
public static class TreeListViewHelper
{
    public static int GetItemLevel(DependencyObject item)
    {
        int level = 0;
        var parent = ItemsControl.ItemsControlFromItemContainer(item);
        while (parent != null && parent is not TreeListView)
        {
            level++;
            parent = ItemsControl.ItemsControlFromItemContainer(parent);
        }
        return level;
    }
}

/// <summary>
/// Attached property to track column resize operations
/// </summary>
public class ColumnResizer : DependencyObject
{
    public static readonly DependencyProperty ColumnProperty =
        DependencyProperty.RegisterAttached("Column", typeof(TreeListViewColumn),
            typeof(ColumnResizer), new PropertyMetadata(null));

    public static TreeListViewColumn GetColumn(DependencyObject obj) =>
        (TreeListViewColumn)obj.GetValue(ColumnProperty);

    public static void SetColumn(DependencyObject obj, TreeListViewColumn value) =>
        obj.SetValue(ColumnProperty, value);
}

