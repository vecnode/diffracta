using System;
using System.IO;
using System.Linq;
using Avalonia.Threading;
using Microsoft.AspNetCore.Http;

namespace Diffracta;

/// <summary>
/// Static class containing all API endpoint implementations.
/// Uses a service locator pattern to access the MainWindow instance.
/// </summary>
public static class ApiEndpoints
{
    /// <summary>
    /// Gets or sets the MainWindow instance. Set by Program.cs after the window is created.
    /// </summary>
    public static MainWindow? MainWindow { get; set; }

    /// <summary>
    /// Gets information about the currently loaded shader.
    /// </summary>
    public static IResult GetShaderInfo()
    {
        if (MainWindow?.Surface == null)
        {
            return Results.Json(new { error = "Shader surface not available" }, statusCode: 503);
        }

        var surface = MainWindow.Surface;
        return Results.Json(new
        {
            isLoaded = surface.IsMainShaderLoaded,
            shaderName = surface.IsMainShaderLoaded ? "Loaded" : "None"
        });
    }

    /// <summary>
    /// Lists all available shader files.
    /// </summary>
    public static IResult ListShaders()
    {
        if (MainWindow == null)
        {
            return Results.Json(new { error = "Application not initialized" }, statusCode: 503);
        }

        try
        {
            var shaderDir = Path.Combine(AppContext.BaseDirectory, "Shaders");
            if (!Directory.Exists(shaderDir))
            {
                return Results.Json(new { shaders = Array.Empty<string>() });
            }

            var shaders = Directory.GetFiles(shaderDir, "*.glsl")
                .Select(Path.GetFileName)
                .Where(f => f != null)
                .Cast<string>()
                .OrderBy(f => f)
                .ToArray();

            return Results.Json(new { shaders });
        }
        catch (Exception ex)
        {
            return Results.Json(new { error = ex.Message }, statusCode: 500);
        }
    }

    /// <summary>
    /// Gets the state of all processing nodes.
    /// </summary>
    public static IResult GetProcessingNodes()
    {
        if (MainWindow?.Surface == null)
        {
            return Results.Json(new { error = "Shader surface not available" }, statusCode: 503);
        }

        var surface = MainWindow.Surface;
        var nodes = new object[6];

        for (int i = 0; i < 6; i++)
        {
            nodes[i] = new
            {
                slot = i,
                active = surface.GetSlotActive(i),
                value = surface.GetSlotValue(i),
                shaderName = surface.GetProcessingNodeShaderName(i),
                shaderLoaded = surface.IsProcessingNodeShaderLoaded(i)
            };
        }

        return Results.Json(new { nodes });
    }

    /// <summary>
    /// Sets the active state of a processing node.
    /// </summary>
    public static IResult SetNodeActive(int slot, SetActiveRequest request)
    {
        if (MainWindow?.Surface == null)
        {
            return Results.Json(new { error = "Shader surface not available" }, statusCode: 503);
        }

        if (slot < 0 || slot >= 6)
        {
            return Results.Json(new { error = "Slot must be between 0 and 5" }, statusCode: 400);
        }

        try
        {
            var surface = MainWindow.Surface;
            Dispatcher.UIThread.Post(() =>
            {
                surface.SetSlotActive(slot, request.Active);
            }, DispatcherPriority.Normal);

            System.Threading.Thread.Sleep(50);

            return Results.Json(new { 
                success = true, 
                slot, 
                active = request.Active 
            });
        }
        catch (Exception ex)
        {
            return Results.Json(new { error = ex.Message }, statusCode: 500);
        }
    }

    /// <summary>
    /// Sets the value of a processing node.
    /// </summary>
    public static IResult SetNodeValue(int slot, SetValueRequest request)
    {
        if (MainWindow?.Surface == null)
        {
            return Results.Json(new { error = "Shader surface not available" }, statusCode: 503);
        }

        if (slot < 0 || slot >= 6)
        {
            return Results.Json(new { error = "Slot must be between 0 and 5" }, statusCode: 400);
        }

        if (request.Value < 0.0f || request.Value > 1.0f)
        {
            return Results.Json(new { error = "Value must be between 0.0 and 1.0" }, statusCode: 400);
        }

        try
        {
            var surface = MainWindow.Surface;
            Dispatcher.UIThread.Post(() =>
            {
                surface.SetSlotValue(slot, request.Value);
            }, DispatcherPriority.Normal);

            System.Threading.Thread.Sleep(50);

            return Results.Json(new { 
                success = true, 
                slot, 
                value = request.Value 
            });
        }
        catch (Exception ex)
        {
            return Results.Json(new { error = ex.Message }, statusCode: 500);
        }
    }

    /// <summary>
    /// Gets the current application state.
    /// </summary>
    public static IResult GetApplicationState()
    {
        if (MainWindow == null)
        {
            return Results.Json(new { error = "Application not initialized" }, statusCode: 503);
        }

        try
        {
            return Results.Json(new
            {
                performanceMode = MainWindow.IsPerformanceMode,
                shaderLoaded = MainWindow.Surface?.IsMainShaderLoaded ?? false
            });
        }
        catch (Exception ex)
        {
            return Results.Json(new { error = ex.Message }, statusCode: 500);
        }
    }

    /// <summary>
    /// Sets the performance mode.
    /// </summary>
    public static IResult SetPerformanceMode(SetPerformanceModeRequest request)
    {
        if (MainWindow == null)
        {
            return Results.Json(new { error = "Application not initialized" }, statusCode: 503);
        }

        try
        {
            Dispatcher.UIThread.Post(() =>
            {
                if (request.Enabled != MainWindow.IsPerformanceMode)
                {
                    MainWindow.TogglePerformanceMode();
                }
            }, DispatcherPriority.Normal);

            System.Threading.Thread.Sleep(50);

            return Results.Json(new { 
                success = true, 
                performanceMode = request.Enabled 
            });
        }
        catch (Exception ex)
        {
            return Results.Json(new { error = ex.Message }, statusCode: 500);
        }
    }
}

// Request/Response DTOs
public record SetActiveRequest(bool Active);
public record SetValueRequest(float Value);
public record SetPerformanceModeRequest(bool Enabled);

