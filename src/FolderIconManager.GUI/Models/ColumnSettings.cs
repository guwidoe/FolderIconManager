using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;

namespace FolderIconManager.GUI.Models;

/// <summary>
/// Stores column width and visibility settings for the tree list view
/// </summary>
public class ColumnSettings : INotifyPropertyChanged
{
    private GridLength _nameWidth = new(200, GridUnitType.Pixel);
    private GridLength _statusWidth = new(80, GridUnitType.Pixel);
    private GridLength _sourceWidth = new(300, GridUnitType.Star);
    private GridLength _attributesWidth = new(60, GridUnitType.Pixel);
    private GridLength _backupWidth = new(60, GridUnitType.Pixel);
    
    private bool _nameVisible = true;
    private bool _statusVisible = true;
    private bool _sourceVisible = true;
    private bool _attributesVisible = true;
    private bool _backupVisible = true;

    #region Width Properties

    public GridLength NameWidth
    {
        get => _nameVisible ? _nameWidth : new GridLength(0);
        set { _nameWidth = value; OnPropertyChanged(); }
    }

    public GridLength StatusWidth
    {
        get => _statusVisible ? _statusWidth : new GridLength(0);
        set { _statusWidth = value; OnPropertyChanged(); }
    }

    public GridLength SourceWidth
    {
        get => _sourceVisible ? _sourceWidth : new GridLength(0);
        set { _sourceWidth = value; OnPropertyChanged(); }
    }

    public GridLength AttributesWidth
    {
        get => _attributesVisible ? _attributesWidth : new GridLength(0);
        set { _attributesWidth = value; OnPropertyChanged(); }
    }

    public GridLength BackupWidth
    {
        get => _backupVisible ? _backupWidth : new GridLength(0);
        set { _backupWidth = value; OnPropertyChanged(); }
    }

    #endregion

    #region Visibility Properties

    public bool NameVisible
    {
        get => _nameVisible;
        set
        {
            if (_nameVisible != value)
            {
                _nameVisible = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(NameWidth));
            }
        }
    }

    public bool StatusVisible
    {
        get => _statusVisible;
        set
        {
            if (_statusVisible != value)
            {
                _statusVisible = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(StatusWidth));
            }
        }
    }

    public bool SourceVisible
    {
        get => _sourceVisible;
        set
        {
            if (_sourceVisible != value)
            {
                _sourceVisible = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(SourceWidth));
            }
        }
    }

    public bool AttributesVisible
    {
        get => _attributesVisible;
        set
        {
            if (_attributesVisible != value)
            {
                _attributesVisible = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(AttributesWidth));
            }
        }
    }

    public bool BackupVisible
    {
        get => _backupVisible;
        set
        {
            if (_backupVisible != value)
            {
                _backupVisible = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(BackupWidth));
            }
        }
    }

    #endregion

    #region Methods

    /// <summary>
    /// Resets all columns to default widths and visibility
    /// </summary>
    public void ResetToDefaults()
    {
        _nameWidth = new GridLength(200, GridUnitType.Pixel);
        _statusWidth = new GridLength(80, GridUnitType.Pixel);
        _sourceWidth = new GridLength(300, GridUnitType.Star);
        _attributesWidth = new GridLength(60, GridUnitType.Pixel);
        _backupWidth = new GridLength(60, GridUnitType.Pixel);
        
        _nameVisible = true;
        _statusVisible = true;
        _sourceVisible = true;
        _attributesVisible = true;
        _backupVisible = true;

        OnPropertyChanged(nameof(NameWidth));
        OnPropertyChanged(nameof(StatusWidth));
        OnPropertyChanged(nameof(SourceWidth));
        OnPropertyChanged(nameof(AttributesWidth));
        OnPropertyChanged(nameof(BackupWidth));
        OnPropertyChanged(nameof(NameVisible));
        OnPropertyChanged(nameof(StatusVisible));
        OnPropertyChanged(nameof(SourceVisible));
        OnPropertyChanged(nameof(AttributesVisible));
        OnPropertyChanged(nameof(BackupVisible));
    }

    /// <summary>
    /// Creates a serializable snapshot of the settings
    /// </summary>
    public ColumnSettingsData ToData()
    {
        return new ColumnSettingsData
        {
            NameWidth = _nameWidth.Value,
            StatusWidth = _statusWidth.Value,
            SourceWidth = _sourceWidth.Value,
            AttributesWidth = _attributesWidth.Value,
            BackupWidth = _backupWidth.Value,
            NameVisible = _nameVisible,
            StatusVisible = _statusVisible,
            SourceVisible = _sourceVisible,
            AttributesVisible = _attributesVisible,
            BackupVisible = _backupVisible
        };
    }

    /// <summary>
    /// Restores settings from serialized data
    /// </summary>
    public void LoadFromData(ColumnSettingsData? data)
    {
        if (data == null) return;

        _nameWidth = new GridLength(data.NameWidth, GridUnitType.Pixel);
        _statusWidth = new GridLength(data.StatusWidth, GridUnitType.Pixel);
        _sourceWidth = new GridLength(data.SourceWidth, GridUnitType.Star);
        _attributesWidth = new GridLength(data.AttributesWidth, GridUnitType.Pixel);
        _backupWidth = new GridLength(data.BackupWidth, GridUnitType.Pixel);
        
        _nameVisible = data.NameVisible;
        _statusVisible = data.StatusVisible;
        _sourceVisible = data.SourceVisible;
        _attributesVisible = data.AttributesVisible;
        _backupVisible = data.BackupVisible;

        OnPropertyChanged(nameof(NameWidth));
        OnPropertyChanged(nameof(StatusWidth));
        OnPropertyChanged(nameof(SourceWidth));
        OnPropertyChanged(nameof(AttributesWidth));
        OnPropertyChanged(nameof(BackupWidth));
        OnPropertyChanged(nameof(NameVisible));
        OnPropertyChanged(nameof(StatusVisible));
        OnPropertyChanged(nameof(SourceVisible));
        OnPropertyChanged(nameof(AttributesVisible));
        OnPropertyChanged(nameof(BackupVisible));
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
/// Serializable data class for column settings persistence
/// </summary>
public class ColumnSettingsData
{
    public double NameWidth { get; set; } = 200;
    public double StatusWidth { get; set; } = 80;
    public double SourceWidth { get; set; } = 300;
    public double AttributesWidth { get; set; } = 60;
    public double BackupWidth { get; set; } = 60;
    
    public bool NameVisible { get; set; } = true;
    public bool StatusVisible { get; set; } = true;
    public bool SourceVisible { get; set; } = true;
    public bool AttributesVisible { get; set; } = true;
    public bool BackupVisible { get; set; } = true;
}

