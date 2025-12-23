using System.IO;
using System.Windows;

namespace FolderIconManager.GUI;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Subscribe to unhandled exception events
        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
    }

    private void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        LogException("AppDomain.UnhandledException", e.ExceptionObject as Exception);
        ShowErrorMessage(e.ExceptionObject as Exception);
    }

    private void OnDispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
    {
        LogException("DispatcherUnhandledException", e.Exception);
        ShowErrorMessage(e.Exception);
        e.Handled = true; // Prevent crash, allow app to continue
    }

    private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        LogException("UnobservedTaskException", e.Exception);
        e.SetObserved(); // Prevent crash
    }

    private void LogException(string source, Exception? ex)
    {
        try
        {
            var logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "error.log");
            var message = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {source}\n{ex}\n\n";
            File.AppendAllText(logPath, message);
        }
        catch
        {
            // Can't log, just continue
        }
    }

    private void ShowErrorMessage(Exception? ex)
    {
        Dispatcher.BeginInvoke(() =>
        {
            MessageBox.Show(
                $"An error occurred:\n\n{ex?.Message}\n\nDetails have been logged to error.log",
                "Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        });
    }
}

