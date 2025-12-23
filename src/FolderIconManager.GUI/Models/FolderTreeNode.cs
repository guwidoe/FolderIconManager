using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using FolderIconManager.Core.Models;
using FolderIconManager.Core.Services;

namespace FolderIconManager.GUI.Models;

/// <summary>
/// Represents a folder node in the tree view with lazy-loading children
/// </summary>
public class FolderTreeNode : INotifyPropertyChanged
{
    private bool _isExpanded;
    private bool _isSelected;
    private bool _isLoaded;
    private bool _hasBackup;
    private bool _sourceChanged;
    private ImageSource? _iconImage;
    private FolderIconStatus _status = FolderIconStatus.NoIcon;
    private FolderIconInfo? _iconInfo;

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

                // Lazy load children when expanded
                if (value && !_isLoaded)
                {
                    LoadChildren();
                }
            }
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

    #region Display Properties

    public string BackupIndicator
    {
        get
        {
            if (!HasBackup) return "";
            return SourceChanged ? "⟳" : "✓";
        }
    }

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
}

