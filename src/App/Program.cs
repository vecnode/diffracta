using Avalonia;
using System.Threading.Tasks;

namespace Diffracta;

internal static class Program {
    private static ApiService? _apiService;

    [STAThread]
    public static void Main(string[] args)
    {
        // Start the REST API server in the background
        _apiService = new ApiService("http://localhost:5000");
        _ = Task.Run(async () =>
        {
            try
            {
                await _apiService.StartAsync();
                Console.WriteLine("REST API server started at http://localhost:5000");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to start API server: {ex.Message}");
            }
        });

        // Start the Avalonia UI application
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);

        // Cleanup: Stop the API server when the application exits
        _apiService?.StopAsync().Wait(TimeSpan.FromSeconds(5));
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .LogToTrace();
}


