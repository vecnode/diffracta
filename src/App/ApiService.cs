using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Diffracta;

/// <summary>
/// Service that manages the ASP.NET Core web server lifecycle.
/// Runs the web server in a background thread so it doesn't block the Avalonia UI.
/// </summary>
public class ApiService
{
    private WebApplication? _app;
    private Task? _serverTask;
    private CancellationTokenSource? _cancellationTokenSource;
    private readonly string _baseUrl;

    public ApiService(string baseUrl = "http://localhost:5000")
    {
        _baseUrl = baseUrl;
    }

    /// <summary>
    /// Starts the web server in a background task.
    /// </summary>
    public async Task StartAsync()
    {
        if (_app != null)
        {
            return; // Already started
        }

        _cancellationTokenSource = new CancellationTokenSource();

        var builder = WebApplication.CreateBuilder();

        // Configure the URLs
        builder.WebHost.UseUrls(_baseUrl);

        // Configure services
        builder.Services.AddCors(options =>
        {
            options.AddDefaultPolicy(policy =>
            {
                policy.AllowAnyOrigin()
                      .AllowAnyMethod()
                      .AllowAnyHeader();
            });
        });

        // Build the app
        _app = builder.Build();

        // Configure the HTTP request pipeline
        _app.UseCors();

        // Register API endpoints
        RegisterEndpoints(_app);

        // Start the server in a background task
        _serverTask = Task.Run(async () =>
        {
            try
            {
                await _app.RunAsync(_cancellationTokenSource.Token);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"API Server Error: {ex.Message}");
            }
        }, _cancellationTokenSource.Token);

        // Give it a moment to start
        await Task.Delay(500);
    }

    /// <summary>
    /// Stops the web server.
    /// </summary>
    public async Task StopAsync()
    {
        if (_cancellationTokenSource != null)
        {
            _cancellationTokenSource.Cancel();
        }

        if (_app != null)
        {
            await _app.StopAsync();
            await _app.DisposeAsync();
            _app = null;
        }

        if (_serverTask != null)
        {
            try
            {
                await _serverTask;
            }
            catch (OperationCanceledException)
            {
                // Expected when cancelling
            }
            _serverTask = null;
        }

        _cancellationTokenSource?.Dispose();
        _cancellationTokenSource = null;
    }

    private void RegisterEndpoints(WebApplication app)
    {
        app.MapGet("/", () => Results.Json(new { message = "Diffracta API", version = "0.1.0" }));
        app.MapGet("/api/shader/list", () => ApiEndpoints.ListShaders());
        app.MapGet("/api/nodes", () => ApiEndpoints.GetProcessingNodes());
        app.MapPost("/api/nodes/{slot:int}/active", (int slot, SetActiveRequest r) => ApiEndpoints.SetNodeActive(slot, r));
        app.MapPost("/api/nodes/{slot:int}/value", (int slot, SetValueRequest r) => ApiEndpoints.SetNodeValue(slot, r));
        app.MapGet("/api/state", () => ApiEndpoints.GetApplicationState());
        app.MapPost("/api/performance", (SetPerformanceModeRequest r) => ApiEndpoints.SetPerformanceMode(r));
    }
}

