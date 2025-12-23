using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace FolderIconManager.GUI.Controls;

/// <summary>
/// A ListView-based control that displays hierarchical data with proper column alignment.
/// Unlike TreeView, indentation only affects the first column content, not the entire row.
/// </summary>
public class TreeListView : ListView
{
    static TreeListView()
    {
        DefaultStyleKeyProperty.OverrideMetadata(
            typeof(TreeListView),
            new FrameworkPropertyMetadata(typeof(TreeListView)));
    }

    public TreeListView()
    {
        // Enable column reordering by default
        var gridView = new GridView { AllowsColumnReorder = true };
        View = gridView;
    }

    /// <summary>
    /// Gets the GridView associated with this TreeListView
    /// </summary>
    public GridView? GridView => View as GridView;

    protected override DependencyObject GetContainerForItemOverride()
    {
        return new TreeListViewItem();
    }

    protected override bool IsItemItsOwnContainerOverride(object item)
    {
        return item is TreeListViewItem;
    }

    protected override void PrepareContainerForItemOverride(DependencyObject element, object item)
    {
        base.PrepareContainerForItemOverride(element, item);
        
        if (element is TreeListViewItem container && item is ITreeListViewNode node)
        {
            container.Level = node.Level;
            container.IsExpandable = node.HasChildren;
            container.IsExpanded = node.IsExpanded;
        }
    }
}

/// <summary>
/// Container for items in a TreeListView
/// </summary>
public class TreeListViewItem : ListViewItem
{
    static TreeListViewItem()
    {
        DefaultStyleKeyProperty.OverrideMetadata(
            typeof(TreeListViewItem),
            new FrameworkPropertyMetadata(typeof(TreeListViewItem)));
    }

    #region Level Property

    public static readonly DependencyProperty LevelProperty =
        DependencyProperty.Register(
            nameof(Level),
            typeof(int),
            typeof(TreeListViewItem),
            new PropertyMetadata(0));

    public int Level
    {
        get => (int)GetValue(LevelProperty);
        set => SetValue(LevelProperty, value);
    }

    #endregion

    #region IsExpanded Property

    public static readonly DependencyProperty IsExpandedProperty =
        DependencyProperty.Register(
            nameof(IsExpanded),
            typeof(bool),
            typeof(TreeListViewItem),
            new PropertyMetadata(false, OnIsExpandedChanged));

    public bool IsExpanded
    {
        get => (bool)GetValue(IsExpandedProperty);
        set => SetValue(IsExpandedProperty, value);
    }

    private static void OnIsExpandedChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is TreeListViewItem item && item.DataContext is ITreeListViewNode node)
        {
            node.IsExpanded = (bool)e.NewValue;
        }
    }

    #endregion

    #region IsExpandable Property

    public static readonly DependencyProperty IsExpandableProperty =
        DependencyProperty.Register(
            nameof(IsExpandable),
            typeof(bool),
            typeof(TreeListViewItem),
            new PropertyMetadata(false));

    public bool IsExpandable
    {
        get => (bool)GetValue(IsExpandableProperty);
        set => SetValue(IsExpandableProperty, value);
    }

    #endregion

    protected override void OnMouseDoubleClick(MouseButtonEventArgs e)
    {
        base.OnMouseDoubleClick(e);
        
        // Toggle expand on double-click if expandable
        if (IsExpandable)
        {
            IsExpanded = !IsExpanded;
            e.Handled = true;
        }
    }
}

/// <summary>
/// Interface that data items must implement to work with TreeListView
/// </summary>
public interface ITreeListViewNode
{
    /// <summary>
    /// The depth level in the tree (0 = root)
    /// </summary>
    int Level { get; }
    
    /// <summary>
    /// Whether this node has children
    /// </summary>
    bool HasChildren { get; }
    
    /// <summary>
    /// Whether this node is currently expanded
    /// </summary>
    bool IsExpanded { get; set; }
    
    /// <summary>
    /// Whether this node is visible (based on parent expansion state)
    /// </summary>
    bool IsVisible { get; }
}
