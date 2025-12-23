using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using FolderIconManager.Core.Models;
using FolderIconManager.Core.Services;
using FolderIconManager.GUI.Models;
using FolderIconManager.GUI.Services;
using Microsoft.Win32;

namespace FolderIconManager.GUI;

public class MainViewModel : INotifyPropertyChanged
{
    private readonly FolderIconService _service;
    private readonly IconCacheService _iconCache;
    private readonly UserDataService _userData;
    private readonly UndoService _undoService;
    private readonly ThemeService _themeService;
    
    private string _folderPath = "";
    private bool _includeSubfolders = true;
    private int? _depthLimit;
    private string _statusText = "Ready";
    private bool _isScanning;
    private bool _isProcessing;
    private FolderTreeNode? _selectedNode;
    private string _filterText = "";
    private FilterMode _filterMode = FilterMode.All;
    private List<FolderTreeNode> _allNodes = [];
    private HashSet<FolderTreeNode> _selectedNodes = [];
    private ColumnSettings _columnWidths = new();

    public MainViewModel()
    {
        _service = new FolderIconService();
        _service.Log.OnLog += OnLogEntry;
        
        _iconCache = new IconCacheService();
        _userData = new UserDataService();
        _undoService = new UndoService();
        _undoService.UndoStackChanged += () => OnPropertyChanged(nameof(CanUndo));
        
        _themeService = new ThemeService();
        _themeService.ApplyTheme(_userData.Settings.Theme);

        RootNodes = [];
        LogEntries = [];

        // Load settings
        _includeSubfolders = _userData.Settings.IncludeSubfoldersByDefault;
        _depthLimit = _userData.Settings.ScanDepthLimit;
        if (!string.IsNullOrEmpty(_userData.Settings.LastFolderPath))
            _folderPath = _userData.Settings.LastFolderPath;

        // Commands
        BrowseCommand = new RelayCommand(Browse);
        ScanCommand = new RelayCommand(async () => await ScanAsync(), () => CanScan);
        ExpandAllCommand = new RelayCommand(ExpandAll, () => RootNodes.Count > 0);
        CollapseAllCommand = new RelayCommand(CollapseAll, () => RootNodes.Count > 0);
        
        // Node actions
        SetIconCommand = new RelayCommand(async () => await SetIconAsync(), () => SelectedNode != null);
        MakeLocalCommand = new RelayCommand(async () => await MakeLocalAsync(), () => CanMakeLocal);
        RestoreCommand = new RelayCommand(async () => await RestoreAsync(), () => CanRestore);
        UpdateFromSourceCommand = new RelayCommand(async () => await UpdateFromSourceAsync(), () => CanUpdateFromSource);
        RemoveIconCommand = new RelayCommand(async () => await RemoveIconAsync(), () => SelectedNode?.HasIcon == true);
        OpenInExplorerCommand = new RelayCommand(OpenInExplorer, () => SelectedNode != null);
        CopyPathCommand = new RelayCommand(CopyPath, () => SelectedNode != null);
        CopyIconSourcePathCommand = new RelayCommand(CopyIconSourcePath, () => SelectedNode?.HasIcon == true);
        
        // Undo
        UndoCommand = new RelayCommand(async () => await UndoAsync(), () => CanUndo);
        
        // Log
        ClearLogCommand = new RelayCommand(ClearLog);
        
        // Settings
        OpenSettingsCommand = new RelayCommand(OpenSettings);
    }

    #region Properties

    public ObservableCollection<FolderTreeNode> RootNodes { get; }
    public ObservableCollection<string> LogEntries { get; }

    public string FolderPath
    {
        get => _folderPath;
        set
        {
            if (_folderPath != value)
            {
                _folderPath = value;
                _userData.Settings.LastFolderPath = value;
                _userData.SaveSettings();
                OnPropertyChanged();
                OnPropertyChanged(nameof(CanScan));
            }
        }
    }

    public bool IncludeSubfolders
    {
        get => _includeSubfolders;
        set { _includeSubfolders = value; OnPropertyChanged(); }
    }

    public int? DepthLimit
    {
        get => _depthLimit;
        set { _depthLimit = value; OnPropertyChanged(); }
    }

    public List<int?> DepthLimitOptions { get; } = [null, 1, 2, 3, 5, 10];

    public string StatusText
    {
        get => _statusText;
        set { _statusText = value; OnPropertyChanged(); }
    }

    public bool CanScan => !string.IsNullOrWhiteSpace(FolderPath) && !_isScanning && !_isProcessing;
    
    public bool CanUndo => _undoService.CanUndo;

    public FolderTreeNode? SelectedNode
    {
        get => _selectedNode;
        set
        {
            if (_selectedNode != value)
            {
                _selectedNode = value;
                OnPropertyChanged();
                RefreshCommandStates();
            }
        }
    }

    public string FilterText
    {
        get => _filterText;
        set { _filterText = value; OnPropertyChanged(); ApplyFilter(); }
    }

    public FilterMode FilterMode
    {
        get => _filterMode;
        set { _filterMode = value; OnPropertyChanged(); OnPropertyChanged(nameof(FilterModeIndex)); ApplyFilter(); }
    }

    public int FilterModeIndex
    {
        get => (int)_filterMode;
        set { FilterMode = (FilterMode)value; }
    }

    public HashSet<FolderTreeNode> SelectedNodes => _selectedNodes;

    public ColumnSettings ColumnWidths => _columnWidths;

    public int SelectedCount => _selectedNodes.Count;

    public bool HasMultiSelection => _selectedNodes.Count > 1;

    public string SelectionText => _selectedNodes.Count > 1 ? $"{_selectedNodes.Count} selected" : "";

    /// <summary>
    /// Gets all selected nodes (multi-selected or single selected)
    /// </summary>
    public IEnumerable<FolderTreeNode> AllSelectedNodes
    {
        get
        {
            if (_selectedNodes.Count > 0)
                return _selectedNodes;
            if (_selectedNode != null)
                return [_selectedNode];
            return [];
        }
    }

    private bool CanMakeLocal => SelectedNode?.Status == FolderIconStatus.ExternalAndValid || 
                                  _selectedNodes.Any(n => n.Status == FolderIconStatus.ExternalAndValid);
    private bool CanRestore => SelectedNode?.HasBackup == true;
    private bool CanUpdateFromSource => SelectedNode?.HasBackup == true && SelectedNode?.SourceChanged == true;

    public string SummaryText
    {
        get
        {
            var allNodes = GetAllNodes(RootNodes).ToList();
            var withIcons = allNodes.Count(n => n.HasIcon);
            var local = allNodes.Count(n => n.Status == FolderIconStatus.LocalAndValid);
            var external = allNodes.Count(n => n.Status == FolderIconStatus.ExternalAndValid);
            var broken = allNodes.Count(n => n.Status is FolderIconStatus.ExternalAndBroken or FolderIconStatus.LocalButMissing);
            
            return $"Total: {allNodes.Count}  |  With Icons: {withIcons}  |  Local: {local}  |  External: {external}  |  Broken: {broken}";
        }
    }

    #endregion

    #region Commands

    public ICommand BrowseCommand { get; }
    public ICommand ScanCommand { get; }
    public ICommand ExpandAllCommand { get; }
    public ICommand CollapseAllCommand { get; }
    public ICommand SetIconCommand { get; }
    public ICommand MakeLocalCommand { get; }
    public ICommand RestoreCommand { get; }
    public ICommand UpdateFromSourceCommand { get; }
    public ICommand RemoveIconCommand { get; }
    public ICommand OpenInExplorerCommand { get; }
    public ICommand CopyPathCommand { get; }
    public ICommand CopyIconSourcePathCommand { get; }
    public ICommand UndoCommand { get; }
    public ICommand ClearLogCommand { get; }
    public ICommand OpenSettingsCommand { get; }

    #endregion

    #region Services (exposed for dialogs)

    public IconCacheService IconCache => _iconCache;
    public UserDataService UserData => _userData;
    public FolderIconService Service => _service;
    public ThemeService ThemeService => _themeService;

    #endregion

    #region Methods

    private void Browse()
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Select folder to scan",
            InitialDirectory = string.IsNullOrEmpty(FolderPath) 
                ? Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) 
                : FolderPath
        };

        if (dialog.ShowDialog() == true)
        {
            FolderPath = dialog.FolderName;
        }
    }

    private async Task ScanAsync()
    {
        if (string.IsNullOrWhiteSpace(FolderPath) || !Directory.Exists(FolderPath))
        {
            StatusText = "Invalid folder path";
            return;
        }

        _isScanning = true;
        StatusText = "Scanning...";
        OnPropertyChanged(nameof(CanScan));
        RootNodes.Clear();

        try
        {
            // Build the folder tree
            var rootNode = await Task.Run(() => BuildFolderTree(FolderPath, _depthLimit));
            
            // Scan for existing icons (respecting depth limit)
            var scanResult = await Task.Run(() => _service.Scan(FolderPath, IncludeSubfolders, _depthLimit));
            
            // Apply icon info to tree nodes
            await Task.Run(() =>
            {
                foreach (var folder in scanResult.Folders)
                {
                    var node = rootNode.FindNode(folder.FolderPath);
                    if (node != null)
                    {
                        node.IconInfo = folder;
                        node.HasIconInSubtree = true;
                        
                        // Check backup status
                        try
                        {
                            node.HasBackup = _service.HasBackup(folder.FolderPath);
                            if (node.HasBackup)
                            {
                                node.SourceChanged = _service.HasSourceChanged(folder.FolderPath);
                            }
                        }
                        catch { /* Ignore backup check errors */ }

                        // Mark parents as having icons in subtree
                        var parent = node.Parent;
                        while (parent != null)
                        {
                            parent.HasIconInSubtree = true;
                            parent = parent.Parent;
                        }
                    }
                }
            });

            // Load icon thumbnails for folders with icons
            await LoadIconThumbnailsAsync(rootNode);

            // Smart expand: expand nodes with icons, collapse others
            SmartExpand(rootNode);

            Application.Current.Dispatcher.Invoke(() =>
            {
                RootNodes.Add(rootNode);
                _allNodes = GetAllNodes(RootNodes).ToList();
                StatusText = $"Found {scanResult.FoldersWithIcons} folder(s) with icons";
                OnPropertyChanged(nameof(SummaryText));
                
                // Apply any active filter
                if (_filterMode != FilterMode.All || !string.IsNullOrEmpty(_filterText))
                {
                    ApplyFilter();
                }
            });
        }
        catch (Exception ex)
        {
            StatusText = $"Error: {ex.Message}";
            AddLog($"[ERR] Scan failed: {ex.Message}");
        }
        finally
        {
            _isScanning = false;
            OnPropertyChanged(nameof(CanScan));
        }
    }

    private FolderTreeNode BuildFolderTree(string rootPath, int? maxDepth)
    {
        var rootNode = new FolderTreeNode(rootPath);
        BuildTreeRecursive(rootNode, 0, maxDepth);
        return rootNode;
    }

    private void BuildTreeRecursive(FolderTreeNode node, int currentDepth, int? maxDepth)
    {
        if (maxDepth.HasValue && currentDepth >= maxDepth.Value)
        {
            node.AddDummyChild(); // Allow manual expansion
            return;
        }

        node.MarkLoaded();

        try
        {
            var dirInfo = new DirectoryInfo(node.FullPath);
            foreach (var subDir in dirInfo.EnumerateDirectories())
            {
                // Skip hidden/system unless enabled
                if (!_userData.Settings.ShowHiddenFolders)
                {
                    if ((subDir.Attributes & FileAttributes.Hidden) != 0 ||
                        (subDir.Attributes & FileAttributes.System) != 0)
                        continue;
                }

                var childNode = new FolderTreeNode(subDir.FullName, node);
                node.Children.Add(childNode);
                
                BuildTreeRecursive(childNode, currentDepth + 1, maxDepth);
            }
        }
        catch
        {
            // Access denied or other error
        }
    }

    private async Task LoadIconThumbnailsAsync(FolderTreeNode root)
    {
        var iconsToLoad = new List<(string path, int index)>();
        
        CollectIconsRecursive(root, iconsToLoad);
        
        if (iconsToLoad.Count > 0)
        {
            await _iconCache.PreloadIconsAsync(iconsToLoad);
            
            // Now set the icons on nodes
            Application.Current.Dispatcher.Invoke(() =>
            {
                SetIconImagesRecursive(root);
            });
        }
    }

    private void CollectIconsRecursive(FolderTreeNode node, List<(string, int)> icons)
    {
        if (node.IconInfo?.CurrentIconResource != null)
        {
            var path = node.IconInfo.ResolvedIconPath;
            if (!string.IsNullOrEmpty(path))
            {
                icons.Add((path, node.IconInfo.CurrentIconResource.Index));
            }
        }

        foreach (var child in node.Children)
        {
            CollectIconsRecursive(child, icons);
        }
    }

    private void SetIconImagesRecursive(FolderTreeNode node)
    {
        if (node.IconInfo?.CurrentIconResource != null)
        {
            var path = node.IconInfo.ResolvedIconPath;
            var index = node.IconInfo.CurrentIconResource.Index;
            node.IconImage = _iconCache.GetFolderIcon(node.FullPath, path, index, node.IsExpanded);
        }
        else
        {
            node.IconImage = _iconCache.DefaultFolderIcon;
        }

        foreach (var child in node.Children)
        {
            SetIconImagesRecursive(child);
        }
    }

    private void SmartExpand(FolderTreeNode node)
    {
        // Expand this node if any child has an icon (to make that child visible)
        // But don't expand the child itself - user can do that manually
        bool shouldExpand = node.Children.Any(c => c.HasIcon || c.HasIconInSubtree);
        
        if (shouldExpand)
        {
            node.IsExpanded = true;
            
            // Recurse only into children that have icon descendants (but aren't icon folders themselves)
            // This expands the path TO icon folders without expanding the icon folders
            foreach (var child in node.Children)
            {
                if (child.HasIconInSubtree)
                {
                    SmartExpand(child);
                }
            }
        }
    }

    private void ExpandAll()
    {
        foreach (var node in RootNodes)
        {
            node.ExpandAll();
        }
    }

    private void CollapseAll()
    {
        foreach (var node in RootNodes)
        {
            node.CollapseAll();
        }
    }

    private async Task SetIconAsync()
    {
        if (SelectedNode == null) return;

        var dialog = new Dialogs.IconPickerDialog(_userData);
        dialog.Owner = Application.Current.MainWindow;
        
        if (dialog.ShowDialog() == true && !string.IsNullOrEmpty(dialog.SelectedIconPath))
        {
            await ApplyIconAsync(
                SelectedNode, 
                dialog.SelectedIconPath, 
                dialog.SelectedIconIndex, 
                useLocal: dialog.UseLocalStorage);
        }
    }

    private async Task MakeLocalAsync()
    {
        var nodesToProcess = AllSelectedNodes
            .Where(n => n.IconInfo != null && n.Status == FolderIconStatus.ExternalAndValid)
            .ToList();
        
        if (nodesToProcess.Count == 0) return;

        _isProcessing = true;
        var isBulk = nodesToProcess.Count > 1;
        StatusText = isBulk ? $"Extracting {nodesToProcess.Count} icons..." : "Extracting icon...";

        try
        {
            var successCount = 0;
            
            foreach (var node in nodesToProcess)
            {
                if (node.IconInfo == null) continue;
                
                // Capture state for undo BEFORE making changes
                var undoOp = await Task.Run(() => CaptureStateForUndo(node, UndoOperationType.MakeLocal, "Make local"));

                var result = await Task.Run(() => _service.ExtractAndInstall([node.IconInfo], skipExisting: false));
                
                if (result.Succeeded.Count > 0)
                {
                    if (undoOp != null)
                    {
                        _undoService.RecordOperation(undoOp);
                    }

                    RefreshNodeAfterUndo(node);
                    AddLog($"[INF] Localized icon for {node.Name}");
                    successCount++;
                }
            }

            StatusText = isBulk 
                ? $"Extracted {successCount} of {nodesToProcess.Count} icons" 
                : (successCount > 0 ? "Icon extracted to local file" : "Failed to extract icon");
        }
        catch (Exception ex)
        {
            StatusText = $"Error: {ex.Message}";
            AddLog($"[ERR] {ex.Message}");
        }
        finally
        {
            _isProcessing = false;
            RefreshCommandStates();
            OnPropertyChanged(nameof(SummaryText));
        }
    }

    private async Task RestoreAsync()
    {
        if (SelectedNode == null) return;

        _isProcessing = true;
        StatusText = "Restoring...";

        try
        {
            // Capture state for undo BEFORE making changes  
            var undoOp = await Task.Run(() => CaptureStateForUndo(SelectedNode, UndoOperationType.Restore, "Restore original"));

            var success = await Task.Run(() => _service.RestoreFromBackup(SelectedNode.FullPath));
            
            if (success)
            {
                // Record undo operation
                if (undoOp != null)
                {
                    _undoService.RecordOperation(undoOp);
                }

                // Re-scan the folder to get updated IconInfo from desktop.ini
                // This ensures the icon source column shows the restored original path
                RefreshNodeAfterUndo(SelectedNode);
                StatusText = "Restored to original icon";
                AddLog($"[INF] Restored original icon for {SelectedNode.Name}");
            }
            else
            {
                StatusText = "Failed to restore";
            }
        }
        catch (Exception ex)
        {
            StatusText = $"Error: {ex.Message}";
        }
        finally
        {
            _isProcessing = false;
            RefreshCommandStates();
        }
    }

    private async Task UpdateFromSourceAsync()
    {
        if (SelectedNode == null) return;

        _isProcessing = true;
        StatusText = "Updating from source...";

        try
        {
            var success = await Task.Run(() => _service.UpdateFromSource(SelectedNode.FullPath));
            
            if (success)
            {
                SelectedNode.SourceChanged = false;
                StatusText = "Updated from source";
                RefreshNodeIcon(SelectedNode);
            }
            else
            {
                StatusText = "Failed to update";
            }
        }
        catch (Exception ex)
        {
            StatusText = $"Error: {ex.Message}";
        }
        finally
        {
            _isProcessing = false;
            RefreshCommandStates();
        }
    }

    private async Task RemoveIconAsync()
    {
        var nodesToProcess = AllSelectedNodes.Where(n => n.HasIcon).ToList();
        
        if (nodesToProcess.Count == 0) return;

        _isProcessing = true;
        var isBulk = nodesToProcess.Count > 1;
        StatusText = isBulk ? $"Removing {nodesToProcess.Count} icons..." : "Removing icon...";

        try
        {
            var successCount = 0;
            
            foreach (var node in nodesToProcess)
            {
                // Capture state for undo BEFORE making changes
                var undoOp = await Task.Run(() => CaptureStateForUndo(node, UndoOperationType.RemoveIcon, "Remove icon"));
                
                await Task.Run(() =>
                {
                    var iniPath = Path.Combine(node.FullPath, "desktop.ini");
                    
                    if (File.Exists(iniPath))
                    {
                        File.SetAttributes(iniPath, FileAttributes.Normal);
                        File.Delete(iniPath);
                    }

                    var localIcon = Path.Combine(node.FullPath, "folder.ico");
                    if (File.Exists(localIcon))
                    {
                        File.SetAttributes(localIcon, FileAttributes.Normal);
                        File.Delete(localIcon);
                    }

                    BackupManifest.Delete(node.FullPath);
                    Core.Native.FileAttributeHelper.NotifyShellOfChange(node.FullPath);
                });

                if (undoOp != null)
                {
                    _undoService.RecordOperation(undoOp);
                }

                node.Status = FolderIconStatus.NoIcon;
                node.IconInfo = null;
                node.HasBackup = false;
                node.IconImage = _iconCache.DefaultFolderIcon;
                AddLog($"[INF] Removed icon from {node.Name}");
                successCount++;
            }

            StatusText = isBulk ? $"Removed {successCount} icons" : "Icon removed";
        }
        catch (Exception ex)
        {
            StatusText = $"Error: {ex.Message}";
        }
        finally
        {
            _isProcessing = false;
            RefreshCommandStates();
            OnPropertyChanged(nameof(SummaryText));
        }
    }

    private UndoOperation? CaptureStateForUndo(FolderTreeNode node, UndoOperationType type, string description)
    {
        string? originalPath = null;
        int originalIndex = 0;
        byte[]? localIconData = null;
        string? backupJson = null;
        bool hadBackup = false;

        // Capture current icon reference
        if (node.IconInfo?.CurrentIconResource != null)
        {
            originalPath = node.IconInfo.CurrentIconResource.FilePath;
            originalIndex = node.IconInfo.CurrentIconResource.Index;
        }

        // Capture local icon data if it exists
        var localIconPath = Path.Combine(node.FullPath, "folder.ico");
        if (File.Exists(localIconPath))
        {
            try
            {
                File.SetAttributes(localIconPath, FileAttributes.Normal);
                localIconData = File.ReadAllBytes(localIconPath);
            }
            catch { }
        }

        // Capture backup manifest
        var manifestPath = Path.Combine(node.FullPath, ".folder-icon-backup.json");
        if (File.Exists(manifestPath))
        {
            try
            {
                hadBackup = true;
                backupJson = File.ReadAllText(manifestPath);
            }
            catch { }
        }

        return new UndoOperation
        {
            FolderPath = node.FullPath,
            Type = type,
            Description = $"{description}: {node.Name}",
            OriginalIconPath = originalPath,
            OriginalIconIndex = originalIndex,
            LocalIconData = localIconData,
            HadBackupManifest = hadBackup,
            BackupManifestJson = backupJson
        };
    }

    private async Task ApplyIconAsync(FolderTreeNode node, string iconPath, int iconIndex, bool useLocal)
    {
        _isProcessing = true;
        StatusText = "Applying icon...";

        try
        {
            // Capture state for undo BEFORE making changes
            var undoOp = await Task.Run(() => CaptureStateForUndo(node, UndoOperationType.SetIcon, "Set icon"));

            await Task.Run(() =>
            {
                if (useLocal)
                {
                    var localPath = Path.Combine(node.FullPath, "folder.ico");
                    _service.ExtractIconFromPath(iconPath, iconIndex, localPath);
                    _service.UpdateDesktopIni(node.FullPath, localPath);
                    _service.ApplyAttributes(node.FullPath, localPath);
                    
                    var manifest = new BackupManifest
                    {
                        BackupDate = DateTime.Now,
                        OriginalIconPath = iconPath,
                        OriginalIconIndex = iconIndex,
                        LocalIconName = "folder.ico"
                    };
                    manifest.Save(node.FullPath);
                }
                else
                {
                    var iniPath = Path.Combine(node.FullPath, "desktop.ini");
                    var iniFile = new Core.Native.IniFile(iniPath);
                    iniFile.WriteIconResource(iconPath, iconIndex);
                    _service.ApplyAttributes(node.FullPath, "");
                }

                Core.Native.FileAttributeHelper.NotifyShellOfChange(node.FullPath);
            });

            // Record undo operation
            if (undoOp != null)
            {
                _undoService.RecordOperation(undoOp);
            }

            // Refresh the node from disk to get updated state
            RefreshNodeAfterUndo(node);
            _userData.AddRecentIcon(new IconReference { FilePath = iconPath, Index = iconIndex });
            
            StatusText = "Icon applied";
            AddLog($"[INF] Set icon for {node.Name}");
        }
        catch (Exception ex)
        {
            StatusText = $"Error: {ex.Message}";
            AddLog($"[ERR] {ex.Message}");
        }
        finally
        {
            _isProcessing = false;
            RefreshCommandStates();
            OnPropertyChanged(nameof(SummaryText));
        }
    }

    private void RefreshNodeIcon(FolderTreeNode node)
    {
        if (node.IconInfo?.CurrentIconResource != null)
        {
            var path = node.IconInfo.ResolvedIconPath;
            var index = node.IconInfo.CurrentIconResource.Index;
            node.IconImage = _iconCache.GetFolderIcon(node.FullPath, path, index, node.IsExpanded);
        }
        else
        {
            // Reload icon info
            var localIcon = Path.Combine(node.FullPath, "folder.ico");
            if (File.Exists(localIcon))
            {
                node.IconImage = _iconCache.GetFolderIcon(node.FullPath, localIcon, 0, node.IsExpanded);
            }
            else
            {
                node.IconImage = _iconCache.DefaultFolderIcon;
            }
        }
    }

    private async Task UndoAsync()
    {
        var operation = _undoService.Pop();
        if (operation == null) return;

        _isProcessing = true;
        StatusText = $"Undoing: {operation.Description}...";

        try
        {
            await Task.Run(() =>
            {
                switch (operation.Type)
                {
                    case UndoOperationType.SetIcon:
                    case UndoOperationType.MakeLocal:
                        // Restore previous state
                        if (string.IsNullOrEmpty(operation.OriginalIconPath))
                        {
                            // Had no icon before - remove current
                            RemoveIconFiles(operation.FolderPath);
                        }
                        else
                        {
                            // Restore original icon reference
                            var iniFile = new Core.Native.IniFile(Path.Combine(operation.FolderPath, "desktop.ini"));
                            iniFile.WriteIconResource(operation.OriginalIconPath, operation.OriginalIconIndex);
                            _service.ApplyAttributes(operation.FolderPath, "");
                        }
                        
                        // Restore backup manifest if existed
                        if (operation.HadBackupManifest && !string.IsNullOrEmpty(operation.BackupManifestJson))
                        {
                            var manifestPath = Path.Combine(operation.FolderPath, ".folder-icon-backup.json");
                            File.WriteAllText(manifestPath, operation.BackupManifestJson);
                            File.SetAttributes(manifestPath, FileAttributes.Hidden);
                        }
                        else
                        {
                            BackupManifest.Delete(operation.FolderPath);
                        }
                        
                        // Restore local icon if we had it backed up
                        if (operation.LocalIconData != null)
                        {
                            var localPath = Path.Combine(operation.FolderPath, "folder.ico");
                            File.WriteAllBytes(localPath, operation.LocalIconData);
                            File.SetAttributes(localPath, FileAttributes.Hidden);
                        }
                        break;

                    case UndoOperationType.RemoveIcon:
                        // Restore the removed icon
                        if (!string.IsNullOrEmpty(operation.OriginalIconPath))
                        {
                            var iniFile = new Core.Native.IniFile(Path.Combine(operation.FolderPath, "desktop.ini"));
                            iniFile.WriteIconResource(operation.OriginalIconPath, operation.OriginalIconIndex);
                            _service.ApplyAttributes(operation.FolderPath, "");
                        }
                        
                        // Restore local icon data
                        if (operation.LocalIconData != null)
                        {
                            var localPath = Path.Combine(operation.FolderPath, "folder.ico");
                            File.WriteAllBytes(localPath, operation.LocalIconData);
                            File.SetAttributes(localPath, FileAttributes.Hidden);
                        }
                        
                        // Restore backup manifest
                        if (!string.IsNullOrEmpty(operation.BackupManifestJson))
                        {
                            var manifestPath = Path.Combine(operation.FolderPath, ".folder-icon-backup.json");
                            File.WriteAllText(manifestPath, operation.BackupManifestJson);
                            File.SetAttributes(manifestPath, FileAttributes.Hidden);
                        }
                        break;

                    case UndoOperationType.Restore:
                        // Restore back to local version
                        if (operation.LocalIconData != null)
                        {
                            var localPath = Path.Combine(operation.FolderPath, "folder.ico");
                            File.WriteAllBytes(localPath, operation.LocalIconData);
                            File.SetAttributes(localPath, FileAttributes.Hidden);
                            
                            var iniFile = new Core.Native.IniFile(Path.Combine(operation.FolderPath, "desktop.ini"));
                            iniFile.WriteIconResource(localPath, 0);
                            _service.ApplyAttributes(operation.FolderPath, localPath);
                        }
                        
                        // Restore backup manifest
                        if (!string.IsNullOrEmpty(operation.BackupManifestJson))
                        {
                            var manifestPath = Path.Combine(operation.FolderPath, ".folder-icon-backup.json");
                            File.WriteAllText(manifestPath, operation.BackupManifestJson);
                            File.SetAttributes(manifestPath, FileAttributes.Hidden);
                        }
                        break;
                }

                Core.Native.FileAttributeHelper.NotifyShellOfChange(operation.FolderPath);
            });

            // Update the node in the tree
            var node = RootNodes.FirstOrDefault()?.FindNode(operation.FolderPath);
            if (node != null)
            {
                RefreshNodeAfterUndo(node);
            }

            StatusText = $"Undone: {operation.Description}";
            AddLog($"[INF] Undone: {operation.Description}");
        }
        catch (Exception ex)
        {
            StatusText = $"Undo failed: {ex.Message}";
            AddLog($"[ERR] Undo failed: {ex.Message}");
        }
        finally
        {
            _isProcessing = false;
            RefreshCommandStates();
            OnPropertyChanged(nameof(SummaryText));
        }
    }

    private void RemoveIconFiles(string folderPath)
    {
        var iniPath = Path.Combine(folderPath, "desktop.ini");
        if (File.Exists(iniPath))
        {
            File.SetAttributes(iniPath, FileAttributes.Normal);
            File.Delete(iniPath);
        }

        var localIcon = Path.Combine(folderPath, "folder.ico");
        if (File.Exists(localIcon))
        {
            File.SetAttributes(localIcon, FileAttributes.Normal);
            File.Delete(localIcon);
        }

        BackupManifest.Delete(folderPath);
    }

    private void RefreshNodeAfterUndo(FolderTreeNode node)
    {
        // Re-scan the folder to get updated status
        try
        {
            var info = _service.GetFolderIconInfo(node.FullPath);
            node.IconInfo = info;
            node.HasBackup = _service.HasBackup(node.FullPath);
            if (node.HasBackup)
            {
                node.SourceChanged = _service.HasSourceChanged(node.FullPath);
            }
            else
            {
                node.SourceChanged = false;
            }
            RefreshNodeIcon(node);
        }
        catch
        {
            node.IconInfo = null;
            node.HasBackup = false;
            node.SourceChanged = false;
            node.IconImage = _iconCache.DefaultFolderIcon;
        }
    }

    private void OpenInExplorer()
    {
        if (SelectedNode == null) return;
        
        try
        {
            System.Diagnostics.Process.Start("explorer.exe", SelectedNode.FullPath);
        }
        catch (Exception ex)
        {
            AddLog($"[ERR] Could not open explorer: {ex.Message}");
        }
    }

    private void CopyPath()
    {
        if (SelectedNode == null) return;
        
        try
        {
            Clipboard.SetText(SelectedNode.FullPath);
            StatusText = "Path copied to clipboard";
        }
        catch
        {
            // Clipboard errors - ignore
        }
    }

    private void CopyIconSourcePath()
    {
        if (SelectedNode == null || !SelectedNode.HasIcon) return;
        
        try
        {
            var sourcePath = SelectedNode.SourceDescription;
            if (!string.IsNullOrEmpty(sourcePath))
            {
                Clipboard.SetText(sourcePath);
                StatusText = "Icon source path copied to clipboard";
            }
            else
            {
                StatusText = "No icon source path available";
            }
        }
        catch
        {
            // Clipboard errors - ignore
        }
    }

    private void ApplyFilter()
    {
        if (_allNodes.Count == 0) return;

        var searchText = _filterText?.Trim().ToLowerInvariant() ?? "";
        
        foreach (var node in _allNodes)
        {
            bool matchesFilter = _filterMode switch
            {
                FilterMode.All => true,
                FilterMode.WithIcons => node.HasIcon,
                FilterMode.ExternalOnly => node.Status == FolderIconStatus.ExternalAndValid,
                FilterMode.LocalOnly => node.Status == FolderIconStatus.LocalAndValid,
                FilterMode.BrokenOnly => node.Status is FolderIconStatus.ExternalAndBroken or FolderIconStatus.LocalButMissing,
                _ => true
            };

            bool matchesSearch = string.IsNullOrEmpty(searchText) ||
                                 node.Name.ToLowerInvariant().Contains(searchText) ||
                                 node.SourceDescription.ToLowerInvariant().Contains(searchText) ||
                                 node.FullPath.ToLowerInvariant().Contains(searchText);

            node.IsFiltered = !(matchesFilter && matchesSearch);
            
            // If a node matches, make sure its ancestors are visible
            if (!node.IsFiltered)
            {
                var parent = node.Parent;
                while (parent != null)
                {
                    parent.IsFiltered = false;
                    if (!parent.IsExpanded && (matchesFilter && matchesSearch))
                    {
                        parent.IsExpanded = true;
                    }
                    parent = parent.Parent;
                }
            }
        }

        // Update summary
        var visibleCount = _allNodes.Count(n => !n.IsFiltered);
        if (_filterMode != FilterMode.All || !string.IsNullOrEmpty(searchText))
        {
            StatusText = $"Showing {visibleCount} of {_allNodes.Count} folders";
        }
        
        OnPropertyChanged(nameof(SummaryText));
    }

    private void ClearLog()
    {
        LogEntries.Clear();
        _service.Log.Clear();
    }

    private void OpenSettings()
    {
        var dialog = new Dialogs.SettingsDialog(_userData, _themeService);
        dialog.Owner = Application.Current.MainWindow;
        
        if (dialog.ShowDialog() == true)
        {
            // Settings were saved - update local state
            IncludeSubfolders = _userData.Settings.IncludeSubfoldersByDefault;
            DepthLimit = _userData.Settings.ScanDepthLimit;
            AddLog("[INF] Settings saved");
        }
    }

    private void RefreshCommandStates()
    {
        OnPropertyChanged(nameof(CanScan));
        OnPropertyChanged(nameof(SelectedCount));
        CommandManager.InvalidateRequerySuggested();
    }

    public void OnMultiSelectionChanged()
    {
        OnPropertyChanged(nameof(SelectedCount));
        OnPropertyChanged(nameof(AllSelectedNodes));
        OnPropertyChanged(nameof(HasMultiSelection));
        OnPropertyChanged(nameof(SelectionText));
        RefreshCommandStates();
    }

    private void OnLogEntry(LogEntry entry)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            LogEntries.Add(entry.ToString());
            while (LogEntries.Count > 1000)
                LogEntries.RemoveAt(0);
        });
    }

    private void AddLog(string message)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            LogEntries.Add($"{DateTime.Now:HH:mm:ss} {message}");
        });
    }

    private static IEnumerable<FolderTreeNode> GetAllNodes(IEnumerable<FolderTreeNode> nodes)
    {
        foreach (var node in nodes)
        {
            yield return node;
            foreach (var child in GetAllNodes(node.Children))
            {
                yield return child;
            }
        }
    }

    #endregion

    #region Window State

    public void SaveWindowState(Window window)
    {
        _userData.Settings.WindowLeft = window.Left;
        _userData.Settings.WindowTop = window.Top;
        _userData.Settings.WindowWidth = window.Width;
        _userData.Settings.WindowHeight = window.Height;
        _userData.Settings.WindowMaximized = window.WindowState == WindowState.Maximized;
        _userData.SaveSettings();
    }

    public void RestoreWindowState(Window window)
    {
        var s = _userData.Settings;
        if (s.WindowWidth.HasValue && s.WindowHeight.HasValue)
        {
            window.Width = s.WindowWidth.Value;
            window.Height = s.WindowHeight.Value;
        }
        if (s.WindowLeft.HasValue && s.WindowTop.HasValue)
        {
            window.Left = s.WindowLeft.Value;
            window.Top = s.WindowTop.Value;
        }
        if (s.WindowMaximized == true)
        {
            window.WindowState = WindowState.Maximized;
        }
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

public enum FilterMode
{
    All,
    WithIcons,
    ExternalOnly,
    LocalOnly,
    BrokenOnly
}

/// <summary>
/// Simple relay command implementation
/// </summary>
public class RelayCommand : ICommand
{
    private readonly Action? _execute;
    private readonly Func<Task>? _executeAsync;
    private readonly Func<bool>? _canExecute;

    public RelayCommand(Action execute, Func<bool>? canExecute = null)
    {
        _execute = execute;
        _canExecute = canExecute;
    }

    public RelayCommand(Func<Task> executeAsync, Func<bool>? canExecute = null)
    {
        _executeAsync = executeAsync;
        _canExecute = canExecute;
    }

    public event EventHandler? CanExecuteChanged
    {
        add => CommandManager.RequerySuggested += value;
        remove => CommandManager.RequerySuggested -= value;
    }

    public bool CanExecute(object? parameter) => _canExecute?.Invoke() ?? true;

    public async void Execute(object? parameter)
    {
        if (_executeAsync != null)
            await _executeAsync();
        else
            _execute?.Invoke();
    }
}
