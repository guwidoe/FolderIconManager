using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using FolderIconManager.Core.Models;
using FolderIconManager.Core.Native;
using FolderIconManager.Core.Services;
using FolderIconManager.GUI.Controls;

namespace FolderIconManager.GUI.Models;

/// <summary>
/// Represents a folder node in the tree view with lazy-loading children
/// </summary>
public class FolderTreeNode : INotifyPropertyChanged, ITreeListViewNode
{
    private bool _isExpanded;
    private bool _isSelected;
    private bool _isMultiSelected;
    private bool _isLoaded;
    private bool _hasBackup;
    private bool _sourceChanged;
    private bool _isFiltered;
    private ImageSource? _iconImage;
    private FolderIconStatus _status = FolderIconStatus.NoIcon;
    private FolderIconInfo? _iconInfo;
    private AttributeStatus _attributeStatus = AttributeStatus.Unknown;

    // Dummy node used to show expander arrow before loading
    private static readonly FolderTreeNode DummyNode = new() { Name = "Loading...", _isLoaded = true };

    public FolderTreeNode()
    {
        Children = new ObservableCollection<FolderTreeNode>();
    }

    public FolderTreeNode(string path, FolderTreeNode? parent = null) : this()
    {
        FullPath = path;
        Name = Path.GetFileName(path);
        if (string.IsNullOrEmpty(Name))
            Name = path; // Root drive like "C:\"
        Parent = parent;
    }

    #region Properties

    public string FullPath { get; set; } = "";
    public string Name { get; set; } = "";
    public FolderTreeNode? Parent { get; set; }
    public ObservableCollection<FolderTreeNode> Children { get; }

    /// <summary>
    /// The icon info from scanning (if this folder has a desktop.ini)
    /// </summary>
    public FolderIconInfo? IconInfo
    {
        get => _iconInfo;
        set
        {
            _iconInfo = value;
            _status = value?.Status ?? FolderIconStatus.NoIcon;
            OnPropertyChanged();
            OnPropertyChanged(nameof(Status));
            OnPropertyChanged(nameof(HasIcon));
            OnPropertyChanged(nameof(StatusIcon));
            OnPropertyChanged(nameof(StatusText));
            OnPropertyChanged(nameof(StatusBrush));
            OnPropertyChanged(nameof(SourceDescription));
        }
    }

    public FolderIconStatus Status
    {
        get => _status;
        set
        {
            _status = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasIcon));
            OnPropertyChanged(nameof(StatusIcon));
            OnPropertyChanged(nameof(StatusText));
            OnPropertyChanged(nameof(StatusBrush));
        }
    }

    public bool HasIcon => Status != FolderIconStatus.NoIcon;

    public bool IsExpanded
    {
        get => _isExpanded;
        set
        {
            if (_isExpanded != value)
            {
                _isExpanded = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasChildren));

                // Lazy load children when expanded
                if (value && !_isLoaded)
                {
                    LoadChildren();
                }

                // Notify all descendants that their visibility may have changed
                NotifyDescendantsVisibilityChanged();
            }
        }
    }

    /// <summary>
    /// Notifies all descendants that their visibility may have changed
    /// </summary>
    private void NotifyDescendantsVisibilityChanged()
    {
        foreach (var child in Children)
        {
            if (child.Name == "Loading...") continue; // Skip dummy nodes
            child.NotifyVisibilityChanged();
            child.NotifyDescendantsVisibilityChanged();
        }
    }

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected != value)
            {
                _isSelected = value;
                OnPropertyChanged();
            }
        }
    }

    public bool IsMultiSelected
    {
        get => _isMultiSelected;
        set
        {
            if (_isMultiSelected != value)
            {
                _isMultiSelected = value;
                OnPropertyChanged();
            }
        }
    }

    public bool HasBackup
    {
        get => _hasBackup;
        set { _hasBackup = value; OnPropertyChanged(); OnPropertyChanged(nameof(BackupIndicator)); }
    }

    public bool SourceChanged
    {
        get => _sourceChanged;
        set { _sourceChanged = value; OnPropertyChanged(); OnPropertyChanged(nameof(BackupIndicator)); }
    }

    public bool IsFiltered
    {
        get => _isFiltered;
        set { _isFiltered = value; OnPropertyChanged(); OnPropertyChanged(nameof(Visibility)); }
    }

    public System.Windows.Visibility Visibility => _isFiltered 
        ? System.Windows.Visibility.Collapsed 
        : System.Windows.Visibility.Visible;

    public ImageSource? IconImage
    {
        get => _iconImage;
        set { _iconImage = value; OnPropertyChanged(); }
    }

    /// <summary>
    /// Whether this node or any descendant has an icon configured
    /// </summary>
    public bool HasIconInSubtree { get; set; }

    #endregion

    #region ITreeListViewNode Implementation

    /// <summary>
    /// The depth level in the tree (0 = root)
    /// </summary>
    public int Level
    {
        get
        {
            int level = 0;
            var current = Parent;
            while (current != null)
            {
                level++;
                current = current.Parent;
            }
            return level;
        }
    }

    /// <summary>
    /// Whether this node has any children
    /// </summary>
    public bool HasChildren => Children.Count > 0;

    /// <summary>
    /// Whether this node should be visible in a flat list view.
    /// A node is visible if it's not filtered AND all ancestors are expanded.
    /// </summary>
    public bool IsVisible
    {
        get
        {
            // Root nodes are always potentially visible (unless filtered)
            if (Parent == null)
                return !_isFiltered;

            // Check if filtered
            if (_isFiltered)
                return false;

            // Check if all ancestors are expanded (and not filtered)
            var current = Parent;
            while (current != null)
            {
                if (!current.IsExpanded || current._isFiltered)
                    return false;
                current = current.Parent;
            }
            return true;
        }
    }

    /// <summary>
    /// Notifies that visibility may have changed (call after expand/collapse)
    /// </summary>
    public void NotifyVisibilityChanged()
    {
        OnPropertyChanged(nameof(IsVisible));
    }

    #endregion

    #region Display Properties

    public string BackupIndicator
    {
        get
        {
            if (!HasBackup) return "";
            return SourceChanged ? "⟳" : "✓";
        }
    }

    public string BackupTooltip
    {
        get
        {
            if (!HasBackup) return "";
            return SourceChanged 
                ? "Source has changed - click Update to refresh" 
                : "Has local backup - can restore to original";
        }
    }

    public AttributeStatus AttributeStatus
    {
        get => _attributeStatus;
        set
        {
            if (_attributeStatus != value)
            {
                _attributeStatus = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(AttributeStatusIcon));
                OnPropertyChanged(nameof(AttributeStatusText));
                OnPropertyChanged(nameof(AttributeStatusBrush));
                OnPropertyChanged(nameof(AttributeTooltip));
                OnPropertyChanged(nameof(NeedsAttributeFix));
            }
        }
    }

    public bool NeedsAttributeFix => _attributeStatus == AttributeStatus.NeedsFix;

    public string AttributeStatusIcon => _attributeStatus switch
    {
        AttributeStatus.Ok => "✓",
        AttributeStatus.NeedsFix => "⚠",
        AttributeStatus.NotApplicable => "",
        _ => ""
    };

    public string AttributeStatusText => _attributeStatus switch
    {
        AttributeStatus.Ok => "OK",
        AttributeStatus.NeedsFix => "Fix",
        AttributeStatus.NotApplicable => "",
        _ => ""
    };

    public Brush AttributeStatusBrush => _attributeStatus switch
    {
        AttributeStatus.Ok => GreenBrush,
        AttributeStatus.NeedsFix => YellowBrush,
        _ => GrayBrush
    };

    public string AttributeTooltip => _attributeStatus switch
    {
        AttributeStatus.Ok => "Folder and desktop.ini attributes are correctly set",
        AttributeStatus.NeedsFix => "Attributes need to be fixed for custom icon to display",
        _ => ""
    };

    public string StatusIcon => Status switch
    {
        FolderIconStatus.LocalAndValid => "✓",
        FolderIconStatus.LocalButMissing => "⚠",
        FolderIconStatus.ExternalAndValid => "→",
        FolderIconStatus.ExternalAndBroken => "✗",
        FolderIconStatus.NoIcon => "○",
        _ => "?"
    };

    public string StatusText => Status switch
    {
        FolderIconStatus.LocalAndValid => "Local",
        FolderIconStatus.LocalButMissing => "Missing",
        FolderIconStatus.ExternalAndValid => "External",
        FolderIconStatus.ExternalAndBroken => "Broken",
        FolderIconStatus.NoIcon => "No icon",
        _ => "Unknown"
    };

    // Static frozen brushes for thread safety
    private static readonly Brush GreenBrush;
    private static readonly Brush YellowBrush;
    private static readonly Brush OrangeBrush;
    private static readonly Brush RedBrush;
    private static readonly Brush GrayBrush;

    static FolderTreeNode()
    {
        GreenBrush = new SolidColorBrush(Color.FromRgb(78, 201, 176));
        GreenBrush.Freeze();
        YellowBrush = new SolidColorBrush(Color.FromRgb(220, 220, 170));
        YellowBrush.Freeze();
        OrangeBrush = new SolidColorBrush(Color.FromRgb(206, 145, 120));
        OrangeBrush.Freeze();
        RedBrush = new SolidColorBrush(Color.FromRgb(241, 76, 76));
        RedBrush.Freeze();
        GrayBrush = new SolidColorBrush(Color.FromRgb(133, 133, 133));
        GrayBrush.Freeze();
    }

    public Brush StatusBrush => Status switch
    {
        FolderIconStatus.LocalAndValid => GreenBrush,
        FolderIconStatus.LocalButMissing => YellowBrush,
        FolderIconStatus.ExternalAndValid => OrangeBrush,
        FolderIconStatus.ExternalAndBroken => RedBrush,
        _ => GrayBrush
    };

    public string SourceDescription
    {
        get
        {
            if (IconInfo?.CurrentIconResource == null)
                return "";

            var source = IconInfo.CurrentIconResource.FilePath;
            if (IconInfo.CurrentIconResource.Index != 0)
                source += $",{IconInfo.CurrentIconResource.Index}";

            return source;
        }
    }

    #endregion

    #region Methods

    /// <summary>
    /// Adds a dummy child so the expander arrow appears
    /// </summary>
    public void AddDummyChild()
    {
        if (!_isLoaded && Children.Count == 0)
        {
            Children.Add(DummyNode);
        }
    }

    /// <summary>
    /// Loads actual children from the file system
    /// </summary>
    public void LoadChildren()
    {
        if (_isLoaded) return;
        _isLoaded = true;

        // Remove dummy
        Children.Clear();

        try
        {
            var dirInfo = new DirectoryInfo(FullPath);
            foreach (var subDir in dirInfo.EnumerateDirectories())
            {
                // Skip hidden/system folders
                if ((subDir.Attributes & FileAttributes.Hidden) != 0 ||
                    (subDir.Attributes & FileAttributes.System) != 0)
                    continue;

                var childNode = new FolderTreeNode(subDir.FullName, this);
                
                // Check if this folder might have children (for expander arrow)
                try
                {
                    if (subDir.EnumerateDirectories().Any(d => 
                        (d.Attributes & FileAttributes.Hidden) == 0 && 
                        (d.Attributes & FileAttributes.System) == 0))
                    {
                        childNode.AddDummyChild();
                    }
                }
                catch
                {
                    // Access denied - still add node but without children
                }

                Children.Add(childNode);
            }
        }
        catch
        {
            // Access denied or other error - leave empty
        }
    }

    /// <summary>
    /// Marks this node as fully loaded (call after scanning)
    /// </summary>
    public void MarkLoaded()
    {
        _isLoaded = true;
    }

    /// <summary>
    /// Finds a child node by path (recursive)
    /// </summary>
    public FolderTreeNode? FindNode(string path)
    {
        if (string.Equals(FullPath, path, StringComparison.OrdinalIgnoreCase))
            return this;

        foreach (var child in Children)
        {
            if (child == DummyNode) continue;
            
            var found = child.FindNode(path);
            if (found != null)
                return found;
        }

        return null;
    }

    /// <summary>
    /// Expands this node and all ancestors
    /// </summary>
    public void ExpandPath()
    {
        Parent?.ExpandPath();
        IsExpanded = true;
    }

    /// <summary>
    /// Recursively expands all children
    /// </summary>
    public void ExpandAll()
    {
        IsExpanded = true;
        foreach (var child in Children)
        {
            if (child != DummyNode)
                child.ExpandAll();
        }
    }

    /// <summary>
    /// Recursively collapses all children
    /// </summary>
    public void CollapseAll()
    {
        foreach (var child in Children)
        {
            if (child != DummyNode)
                child.CollapseAll();
        }
        IsExpanded = false;
    }

    #endregion

    #region INotifyPropertyChanged

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    #endregion

    #region Attribute Checking

    /// <summary>
    /// Checks the folder's attributes and updates the AttributeStatus property
    /// </summary>
    public void CheckAttributes()
    {
        // Only check if this folder has an icon configured
        if (!HasIcon)
        {
            AttributeStatus = AttributeStatus.NotApplicable;
            return;
        }

        var (folderOk, iniOk) = FileAttributeHelper.CheckFolderIconAttributes(FullPath);
        AttributeStatus = (folderOk && iniOk) ? AttributeStatus.Ok : AttributeStatus.NeedsFix;
    }

    /// <summary>
    /// Fixes the folder's attributes to make the custom icon display correctly
    /// </summary>
    public bool FixAttributes()
    {
        if (!HasIcon)
            return false;

        var result = FileAttributeHelper.FixFolderIconAttributes(FullPath);
        if (result)
        {
            AttributeStatus = AttributeStatus.Ok;
        }
        return result;
    }

    #endregion
}

/// <summary>
/// Status of folder icon attributes
/// </summary>
public enum AttributeStatus
{
    /// <summary>Not yet checked or not applicable (no icon)</summary>
    Unknown,
    
    /// <summary>No icon configured, attributes don't matter</summary>
    NotApplicable,
    
    /// <summary>Folder and desktop.ini have correct attributes</summary>
    Ok,
    
    /// <summary>Attributes need to be fixed for icon to display</summary>
    NeedsFix
}

