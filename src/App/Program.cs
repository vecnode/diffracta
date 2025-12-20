using Avalonia;
using System;
using System.Threading.Tasks;

namespace Diffracta;

internal static class Program {
    public static ApiService? ApiService { get; private set; }

    [STAThread]
    public static void Main(string[] args)
    {
        // Start the REST API server in the background (fire-and-forget, zero UI delay)
        ApiService = new ApiService("http://localhost:5000");
        _ = Task.Run(async () =>
        {
            try
            {
                await ApiService.StartAsync().ConfigureAwait(false);
                Console.WriteLine("REST API server started at http://localhost:5000");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to start API server: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"API Server Error: {ex}");
            }
        });

        // Start the Avalonia UI application immediately (no delay)
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .LogToTrace();
}


