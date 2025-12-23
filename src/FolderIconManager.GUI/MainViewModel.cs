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
        FixSelectedCommand = new RelayCommand(async () => await FixSelectedAsync(), () => HasSelection);
        FixAllCommand = new RelayCommand(async () => await FixAllAsync(), () => HasExternalIcons);
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

    public string SummaryText
    {
        get
        {
            var total = Folders.Count;
            var local = Folders.Count(f => f.Status == FolderIconStatus.LocalAndValid);
            var external = Folders.Count(f => f.Status == FolderIconStatus.ExternalAndValid);
            var broken = Folders.Count(f => f.Status is FolderIconStatus.ExternalAndBroken or FolderIconStatus.LocalButMissing);
            
            return $"Total: {total}  |  Local: {local}  |  External: {external}  |  Broken: {broken}";
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

        try
        {
            var result = await Task.Run(() => _service.Scan(FolderPath, IncludeSubfolders));

            foreach (var folder in result.Folders)
            {
                Folders.Add(new FolderItemViewModel(folder));
            }

            StatusText = $"Found {result.FoldersWithIcons} folder(s) with icons";
        }
        catch (Exception ex)
        {
            StatusText = $"Error: {ex.Message}";
            AddLog($"[ERR] {ex.Message}");
        }
        finally
        {
            _isScanning = false;
            OnPropertyChanged(nameof(CanScan));
            OnPropertyChanged(nameof(HasExternalIcons));
            OnPropertyChanged(nameof(SummaryText));
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
            OnPropertyChanged(nameof(CanScan));
            OnPropertyChanged(nameof(HasExternalIcons));
            OnPropertyChanged(nameof(SummaryText));
        }
    }

    private void ClearLog()
    {
        LogEntries.Clear();
        _service.Log.Clear();
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
        FolderIconStatus.LocalAndValid => "Icon is stored locally in folder",
        FolderIconStatus.LocalButMissing => "Local icon file is missing",
        FolderIconStatus.ExternalAndValid => "Icon references external file",
        FolderIconStatus.ExternalAndBroken => "Icon source file not found",
        _ => ""
    };

    public Brush StatusBrush => Status switch
    {
        FolderIconStatus.LocalAndValid => new SolidColorBrush(Color.FromRgb(78, 201, 176)),     // Green
        FolderIconStatus.LocalButMissing => new SolidColorBrush(Color.FromRgb(220, 220, 170)), // Yellow
        FolderIconStatus.ExternalAndValid => new SolidColorBrush(Color.FromRgb(206, 145, 120)), // Orange
        FolderIconStatus.ExternalAndBroken => new SolidColorBrush(Color.FromRgb(241, 76, 76)),  // Red
        _ => new SolidColorBrush(Color.FromRgb(133, 133, 133))
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

