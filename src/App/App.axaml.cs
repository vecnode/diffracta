using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using System;
using System.IO;

namespace Diffracta;

public partial class App : Application {
    public override void Initialize() {
        // Add global exception handler
        AppDomain.CurrentDomain.UnhandledException += (sender, e) =>
        {
            var error = $"Unhandled Exception: {e.ExceptionObject}\n";
            System.Diagnostics.Debug.WriteLine(error);
            Console.WriteLine(error);
            
            // Write to file for debugging
            try
            {
                var logPath = Path.Combine(AppContext.BaseDirectory, "error.log");
                File.AppendAllText(logPath, $"[{DateTime.Now}] {error}\n");
            }
            catch { }
        };
        
        try
        {
            AvaloniaXamlLoader.Load(this);
        }
        catch (Exception ex)
        {
            var error = $"XAML Load Error: {ex.Message}\n{ex.StackTrace}";
            System.Diagnostics.Debug.WriteLine(error);
            Console.WriteLine(error);
            
            try
            {
                var logPath = Path.Combine(AppContext.BaseDirectory, "error.log");
                File.AppendAllText(logPath, $"[{DateTime.Now}] {error}\n");
            }
            catch { }
            
            throw;
        }
    }

    public override void OnFrameworkInitializationCompleted() {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop) {
            var mainWindow = new MainWindow();
            desktop.MainWindow = mainWindow;
            
            // Register MainWindow instance with API endpoints so they can access it
            ApiEndpoints.MainWindow = mainWindow;
        }
        base.OnFrameworkInitializationCompleted();
    }
}
