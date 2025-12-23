using System.Windows;
using System.Windows.Controls;

namespace FolderIconManager.GUI;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        
        // Auto-scroll log to bottom when new entries are added
        if (DataContext is MainViewModel vm)
        {
            vm.LogEntries.CollectionChanged += (s, e) =>
            {
                LogScrollViewer.ScrollToEnd();
            };
        }
    }
}

