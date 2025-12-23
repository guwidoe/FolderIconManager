using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using FolderIconManager.Core.Models;
using FolderIconManager.Core.Services;
using Microsoft.Win32;

namespace FolderIconManager.GUI;

public class MainViewModel : INotifyPropertyChanged
{
    private readonly FolderIconService _service;
    private string _folderPath = "";
    private bool _includeSubfolders = true;
    private bool _skipExisting = true;
    private string _statusText = "Ready";
    private bool _isScanning;
    private bool _isProcessing;
    private FolderItemViewModel? _selectedFolder;

    public MainViewModel()
    {
        _service = new FolderIconService();
        _service.Log.OnLog += OnLogEntry;

        Folders = [];
        LogEntries = [];

        BrowseCommand = new RelayCommand(Browse);
        ScanCommand = new RelayCommand(async () => await ScanAsync(), () => CanScan);
        FixSelectedCommand = new RelayCommand(async () => await FixSelectedAsync(), () => HasSelection && SelectedFolder?.Status == FolderIconStatus.ExternalAndValid);
        FixAllCommand = new RelayCommand(async () => await FixAllAsync(), () => HasExternalIcons);
        RestoreSelectedCommand = new RelayCommand(async () => await RestoreSelectedAsync(), () => HasSelection && SelectedFolder?.HasBackup == true);
        RestoreAllCommand = new RelayCommand(async () => await RestoreAllAsync(), () => HasLocalWithBackup);
        UpdateSelectedCommand = new RelayCommand(async () => await UpdateSelectedAsync(), () => HasSelection && SelectedFolder?.HasBackup == true);
        UpdateAllCommand = new RelayCommand(async () => await UpdateAllAsync(), () => HasLocalWithBackup);
        ClearLogCommand = new RelayCommand(ClearLog);
    }

    #region Properties

    public string FolderPath
    {
        get => _folderPath;
        set
        {
            if (_folderPath != value)
            {
                _folderPath = value;
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

    public bool SkipExisting
    {
        get => _skipExisting;
        set { _skipExisting = value; OnPropertyChanged(); }
    }

    public string StatusText
    {
        get => _statusText;
        set { _statusText = value; OnPropertyChanged(); }
    }

    public bool CanScan => !string.IsNullOrWhiteSpace(FolderPath) && !_isScanning && !_isProcessing;

    public bool HasSelection => SelectedFolder != null;

    public bool HasExternalIcons => Folders.Any(f => f.Status == FolderIconStatus.ExternalAndValid);
    
    public bool HasLocalWithBackup => Folders.Any(f => f.Status == FolderIconStatus.LocalAndValid && f.HasBackup);

    public string SummaryText
    {
        get
        {
            var total = Folders.Count;
            var local = Folders.Count(f => f.Status == FolderIconStatus.LocalAndValid);
            var external = Folders.Count(f => f.Status == FolderIconStatus.ExternalAndValid);
            var broken = Folders.Count(f => f.Status is FolderIconStatus.ExternalAndBroken or FolderIconStatus.LocalButMissing);
            var withBackup = Folders.Count(f => f.HasBackup);
            
            return $"Total: {total}  |  Local: {local}  |  External: {external}  |  Broken: {broken}  |  With Backup: {withBackup}";
        }
    }

    public FolderItemViewModel? SelectedFolder
    {
        get => _selectedFolder;
        set
        {
            _selectedFolder = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasSelection));
        }
    }

    public ObservableCollection<FolderItemViewModel> Folders { get; }
    public ObservableCollection<string> LogEntries { get; }

    #endregion

    #region Commands

    public ICommand BrowseCommand { get; }
    public ICommand ScanCommand { get; }
    public ICommand FixSelectedCommand { get; }
    public ICommand FixAllCommand { get; }
    public ICommand RestoreSelectedCommand { get; }
    public ICommand RestoreAllCommand { get; }
    public ICommand UpdateSelectedCommand { get; }
    public ICommand UpdateAllCommand { get; }
    public ICommand ClearLogCommand { get; }

    #endregion

    #region Methods

    private void Browse()
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Select folder to scan",
            InitialDirectory = string.IsNullOrEmpty(FolderPath) ? Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) : FolderPath
        };

        if (dialog.ShowDialog() == true)
        {
            FolderPath = dialog.FolderName;
        }
    }

    private async Task ScanAsync()
    {
        if (string.IsNullOrWhiteSpace(FolderPath))
            return;

        _isScanning = true;
        StatusText = "Scanning...";
        OnPropertyChanged(nameof(CanScan));
        Folders.Clear();

        ScanResult? result = null;
        
        try
        {
            result = await Task.Run(() => _service.Scan(FolderPath, IncludeSubfolders));
        }
        catch (Exception scanEx)
        {
            StatusText = $"Scan error: {scanEx.Message}";
            AddLog($"[ERR] Scan failed: {scanEx.Message}");
            AddLog($"[ERR] Stack trace: {scanEx.StackTrace}");
            _isScanning = false;
            RefreshProperties();
            return;
        }

        try
        {
            // Create view models on UI thread
            foreach (var folder in result.Folders)
            {
                try
                {
                    var vm = new FolderItemViewModel(folder);
                    
                    // Check for backup (with error handling)
                    try
                    {
                        vm.HasBackup = _service.HasBackup(folder.FolderPath);
                        if (vm.HasBackup)
                        {
                            vm.SourceChanged = _service.HasSourceChanged(folder.FolderPath);
                        }
                    }
                    catch (Exception backupEx)
                    {
                        // Log but don't crash - backup check is optional
                        AddLog($"[WRN] Could not check backup for {folder.FolderPath}: {backupEx.Message}");
                    }
                    
                    Folders.Add(vm);
                }
                catch (Exception folderEx)
                {
                    AddLog($"[ERR] Error processing folder: {folderEx.Message}");
                    AddLog($"[ERR] Stack: {folderEx.StackTrace}");
                }
            }

            StatusText = $"Found {result.FoldersWithIcons} folder(s) with icons";
        }
        catch (Exception ex)
        {
            StatusText = $"Error: {ex.Message}";
            AddLog($"[ERR] {ex.Message}");
            AddLog($"[ERR] Stack trace: {ex.StackTrace}");
        }
        finally
        {
            _isScanning = false;
            try
            {
                RefreshProperties();
            }
            catch (Exception refreshEx)
            {
                AddLog($"[ERR] RefreshProperties failed: {refreshEx.Message}");
            }
        }
    }

    private async Task FixSelectedAsync()
    {
        if (SelectedFolder == null)
            return;

        await FixFoldersAsync([SelectedFolder.Model]);
    }

    private async Task FixAllAsync()
    {
        var externalFolders = Folders
            .Where(f => f.Status == FolderIconStatus.ExternalAndValid)
            .Select(f => f.Model)
            .ToList();

        if (externalFolders.Count == 0)
            return;

        await FixFoldersAsync(externalFolders);
    }

    private async Task FixFoldersAsync(List<FolderIconInfo> folders)
    {
        _isProcessing = true;
        StatusText = $"Processing {folders.Count} folder(s)...";
        OnPropertyChanged(nameof(CanScan));

        try
        {
            var result = await Task.Run(() => _service.ExtractAndInstall(folders, SkipExisting));

            // Update the view models
            foreach (var succeeded in result.Succeeded)
            {
                var vm = Folders.FirstOrDefault(f => f.FolderPath == succeeded.FolderPath);
                if (vm != null)
                {
                    vm.UpdateStatus(FolderIconStatus.LocalAndValid);
                    vm.HasBackup = true;
                }
            }

            StatusText = $"Done: {result.Succeeded.Count} fixed, {result.Skipped.Count} skipped, {result.Failed.Count} failed";
        }
        catch (Exception ex)
        {
            StatusText = $"Error: {ex.Message}";
            AddLog($"[ERR] {ex.Message}");
        }
        finally
        {
            _isProcessing = false;
            RefreshProperties();
        }
    }

    private async Task RestoreSelectedAsync()
    {
        if (SelectedFolder == null)
            return;

        _isProcessing = true;
        StatusText = "Restoring...";
        OnPropertyChanged(nameof(CanScan));

        try
        {
            var success = await Task.Run(() => _service.RestoreFromBackup(SelectedFolder.FolderPath));
            
            if (success)
            {
                SelectedFolder.UpdateStatus(FolderIconStatus.ExternalAndValid);
                SelectedFolder.HasBackup = false;
                StatusText = "Restored to original icon reference";
            }
            else
            {
                StatusText = "Failed to restore";
            }
        }
        catch (Exception ex)
        {
            StatusText = $"Error: {ex.Message}";
            AddLog($"[ERR] {ex.Message}");
        }
        finally
        {
            _isProcessing = false;
            RefreshProperties();
        }
    }

    private async Task RestoreAllAsync()
    {
        var foldersWithBackup = Folders.Where(f => f.HasBackup).ToList();
        if (foldersWithBackup.Count == 0)
            return;

        _isProcessing = true;
        StatusText = $"Restoring {foldersWithBackup.Count} folder(s)...";
        OnPropertyChanged(nameof(CanScan));

        try
        {
            var restoredCount = 0;
            await Task.Run(() =>
            {
                foreach (var folder in foldersWithBackup)
                {
                    if (_service.RestoreFromBackup(folder.FolderPath))
                    {
                        restoredCount++;
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            folder.UpdateStatus(FolderIconStatus.ExternalAndValid);
                            folder.HasBackup = false;
                        });
                    }
                }
            });

            StatusText = $"Restored {restoredCount} folder(s)";
        }
        catch (Exception ex)
        {
            StatusText = $"Error: {ex.Message}";
            AddLog($"[ERR] {ex.Message}");
        }
        finally
        {
            _isProcessing = false;
            RefreshProperties();
        }
    }

    private async Task UpdateSelectedAsync()
    {
        if (SelectedFolder == null)
            return;

        _isProcessing = true;
        StatusText = "Updating from source...";
        OnPropertyChanged(nameof(CanScan));

        try
        {
            var success = await Task.Run(() => _service.UpdateFromSource(SelectedFolder.FolderPath));
            
            if (success)
            {
                SelectedFolder.SourceChanged = false;
                StatusText = "Updated from original source";
            }
            else
            {
                StatusText = "Source not found or update failed";
            }
        }
        catch (Exception ex)
        {
            StatusText = $"Error: {ex.Message}";
            AddLog($"[ERR] {ex.Message}");
        }
        finally
        {
            _isProcessing = false;
            RefreshProperties();
        }
    }

    private async Task UpdateAllAsync()
    {
        _isProcessing = true;
        StatusText = "Checking for updates...";
        OnPropertyChanged(nameof(CanScan));

        try
        {
            var updatedCount = await Task.Run(() => _service.UpdateAll(FolderPath, IncludeSubfolders, forceUpdate: false));

            // Refresh source changed status
            foreach (var folder in Folders.Where(f => f.HasBackup))
            {
                folder.SourceChanged = _service.HasSourceChanged(folder.FolderPath);
            }

            StatusText = $"Updated {updatedCount} folder(s) from changed sources";
        }
        catch (Exception ex)
        {
            StatusText = $"Error: {ex.Message}";
            AddLog($"[ERR] {ex.Message}");
        }
        finally
        {
            _isProcessing = false;
            RefreshProperties();
        }
    }

    private void ClearLog()
    {
        LogEntries.Clear();
        _service.Log.Clear();
    }

    private void RefreshProperties()
    {
        OnPropertyChanged(nameof(CanScan));
        OnPropertyChanged(nameof(HasExternalIcons));
        OnPropertyChanged(nameof(HasLocalWithBackup));
        OnPropertyChanged(nameof(SummaryText));
    }

    private void OnLogEntry(LogEntry entry)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            LogEntries.Add(entry.ToString());
            
            // Keep log size manageable
            while (LogEntries.Count > 1000)
            {
                LogEntries.RemoveAt(0);
            }
        });
    }

    private void AddLog(string message)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            LogEntries.Add($"{DateTime.Now:HH:mm:ss} {message}");
        });
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

/// <summary>
/// View model for a single folder item in the list
/// </summary>
public class FolderItemViewModel : INotifyPropertyChanged
{
    private FolderIconStatus _status;
    private bool _hasBackup;
    private bool _sourceChanged;

    public FolderItemViewModel(FolderIconInfo model)
    {
        Model = model;
        _status = model.Status;
    }

    public FolderIconInfo Model { get; }

    public string FolderPath => Model.FolderPath;

    public FolderIconStatus Status
    {
        get => _status;
        private set
        {
            _status = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(StatusIcon));
            OnPropertyChanged(nameof(StatusText));
            OnPropertyChanged(nameof(StatusDescription));
            OnPropertyChanged(nameof(StatusBrush));
        }
    }

    public bool HasBackup
    {
        get => _hasBackup;
        set
        {
            _hasBackup = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(BackupIndicator));
        }
    }

    public bool SourceChanged
    {
        get => _sourceChanged;
        set
        {
            _sourceChanged = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(BackupIndicator));
        }
    }

    public string BackupIndicator
    {
        get
        {
            if (!HasBackup) return "";
            return SourceChanged ? "⟳" : "✎";  // ⟳ = source changed, ✎ = has backup
        }
    }

    public string StatusIcon => Status switch
    {
        FolderIconStatus.LocalAndValid => "✓",
        FolderIconStatus.LocalButMissing => "⚠",
        FolderIconStatus.ExternalAndValid => "→",
        FolderIconStatus.ExternalAndBroken => "✗",
        _ => "?"
    };

    public string StatusText => Status switch
    {
        FolderIconStatus.LocalAndValid => "Local",
        FolderIconStatus.LocalButMissing => "Missing",
        FolderIconStatus.ExternalAndValid => "External",
        FolderIconStatus.ExternalAndBroken => "Broken",
        _ => "Unknown"
    };

    public string StatusDescription => Status switch
    {
        FolderIconStatus.LocalAndValid => HasBackup 
            ? (SourceChanged ? "Local icon - source has been updated" : "Local icon - backup available")
            : "Local icon",
        FolderIconStatus.LocalButMissing => "Local icon file is missing",
        FolderIconStatus.ExternalAndValid => "Icon references external file",
        FolderIconStatus.ExternalAndBroken => "Icon source file not found",
        _ => ""
    };

    // Static frozen brushes for thread safety in WPF
    private static readonly Brush GreenBrush;
    private static readonly Brush YellowBrush;
    private static readonly Brush OrangeBrush;
    private static readonly Brush RedBrush;
    private static readonly Brush GrayBrush;

    static FolderItemViewModel()
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
            if (Model.CurrentIconResource == null)
                return "";

            var source = Model.CurrentIconResource.FilePath;
            if (Model.CurrentIconResource.Index != 0)
                source += $",{Model.CurrentIconResource.Index}";

            return source;
        }
    }

    public void UpdateStatus(FolderIconStatus newStatus)
    {
        Status = newStatus;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
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
        {
            await _executeAsync();
        }
        else
        {
            _execute?.Invoke();
        }
    }
}
