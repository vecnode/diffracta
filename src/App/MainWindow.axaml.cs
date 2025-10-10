using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.Platform.Storage;
using System.IO;
using System.Linq;
using System.Text;

namespace Diffracta;

public partial class MainWindow : Window {
    private FileSystemWatcher? _watcher;
    private string _shaderDir = Path.Combine(AppContext.BaseDirectory, "Shaders");
    private readonly StringBuilder _logBuffer = new();
    private bool _isFullscreen = false;
    private bool _isPerformanceMode = false;

    public MainWindow() {
        InitializeComponent();

        Loaded += (_, __) => {
            Directory.CreateDirectory(_shaderDir);
            LeftPanelStatusText.Text = "Initializing shader system";
            LogMessage("Application started");
            LogMessage($"Shader directory: {_shaderDir}");
            Surface.SetLogCallback(LogMessage);
            PopulatePicker();
            SetupWatcher();
            LogMessage("Ready - Select a shader from the dropdown");
        };

        ShaderPicker.SelectionChanged += (_, __) => {
            if (ShaderPicker.SelectedItem is string path && File.Exists(path)) {
                LeftPanelStatusText.Text = $"Compiling {Path.GetFileName(path)}";
                LogMessage($"Loading shader: {Path.GetFileName(path)}");
                Surface.LoadFragmentShaderFromFile(path, out var message);
                LeftPanelStatusText.Text = message;
            }
        };

        FullscreenButton.Click += (_, __) => {
            ToggleFullscreen();
        };

        PerformanceButton.Click += (_, __) => {
            TogglePerformanceMode();
        };

        LoadShaderButton.Click += async (_, __) => {
            await LoadShaderFiles();
        };

        ClearLogButton.Click += (_, __) => {
            _logBuffer.Clear();
            LogTextBox.Text = string.Empty;
        };

        CopyLogButton.Click += (_, __) => {
            var text = LogTextBox.Text;
            if (!string.IsNullOrEmpty(text))
            {
                // Copy to clipboard
                var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
                clipboard?.SetTextAsync(text);
                LogMessage("Log copied to clipboard");
            }
        };

        // Handle Escape key to exit performance mode first, then fullscreen
        KeyDown += (_, e) => {
            if (e.Key == Avalonia.Input.Key.Escape && _isPerformanceMode)
            {
                ExitPerformanceMode();
            }
            else if (e.Key == Avalonia.Input.Key.Escape && _isFullscreen)
            {
                ExitFullscreen();
            }
        };
    }

    private void PopulatePicker() {
        var items = Directory.EnumerateFiles(_shaderDir, "*.glsl")
            .OrderBy(p => Path.GetFileName(p))
            .ToList();

        ShaderPicker.ItemsSource = items;
        if (items.Count > 0) {
            ShaderPicker.SelectedIndex = 0;
            LeftPanelStatusText.Text = "Ready";
        }
        else {
            LeftPanelStatusText.Text = "No shaders found";
        }
    }

    private void SetupWatcher() {
        _watcher = new FileSystemWatcher(_shaderDir, "*.glsl") {
            IncludeSubdirectories = false,
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.FileName
        };
        _watcher.Created += (_, __) => Dispatcher.UIThread.Post(PopulatePicker);
        _watcher.Deleted += (_, __) => Dispatcher.UIThread.Post(PopulatePicker);
        _watcher.Renamed += (_, __) => Dispatcher.UIThread.Post(PopulatePicker);
        _watcher.EnableRaisingEvents = true;
    }

    private void LogMessage(string message) {
        var timestamp = DateTime.Now.ToString("HH:mm:ss");
        var logEntry = $"[{timestamp}] {message}";
        
        _logBuffer.AppendLine(logEntry);
        
        Dispatcher.UIThread.Post(() => {
            LogTextBox.Text = _logBuffer.ToString();
        });
    }

    private void ToggleFullscreen() {
        if (_isFullscreen) {
            ExitFullscreen();
        } else {
            EnterFullscreen();
        }
    }

    private void TogglePerformanceMode() {
        if (_isPerformanceMode) {
            ExitPerformanceMode();
        } else {
            EnterPerformanceMode();
        }
    }

    private void EnterFullscreen() {
        _isFullscreen = true;
        WindowState = WindowState.Maximized;
        FullscreenButton.Content = "Restore";
        
        LogMessage("Entered maximized mode - Press Escape to exit");
    }

    private void ExitFullscreen() {
        _isFullscreen = false;
        WindowState = WindowState.Normal;
        FullscreenButton.Content = "Maximize";
        
        LogMessage("Exited maximized mode");
    }

    private void EnterPerformanceMode() {
        _isPerformanceMode = true;
        PerformanceButton.Content = "Exit Performance";
        
        // Exit fullscreen if active
        if (_isFullscreen) {
            ExitFullscreen();
        }
        
        // Hide all UI panels but keep the shader surface visible
        ControlsPanel.IsVisible = false;
        LogPanel.IsVisible = false;
        BottomRightPanel.IsVisible = false;
        VerticalSplitter.IsVisible = false;
        HorizontalSplitter.IsVisible = false;
        BottomVerticalSplitter.IsVisible = false;
        
        // Go fullscreen for Performance mode to use full viewport
        WindowState = WindowState.FullScreen;
        
        // Hide mouse cursor in performance mode
        Cursor = Avalonia.Input.Cursor.Parse("None");
        
        // Make the shader surface span the entire viewport
        Surface.SetValue(Grid.RowProperty, 0);
        Surface.SetValue(Grid.ColumnProperty, 0);
        Surface.SetValue(Grid.RowSpanProperty, 3);
        Surface.SetValue(Grid.ColumnSpanProperty, 2);
        
        LogMessage("Entered performance mode - Full viewport shader, Press Escape to exit");
    }

    private void ExitPerformanceMode() {
        _isPerformanceMode = false;
        PerformanceButton.Content = "Performance";
        
        // Exit fullscreen and return to windowed mode
        WindowState = WindowState.Normal;
        
        // Restore mouse cursor
        Cursor = Avalonia.Input.Cursor.Parse("Arrow");
        
        // Show all panels again
        ControlsPanel.IsVisible = true;
        LogPanel.IsVisible = true;
        BottomRightPanel.IsVisible = true;
        VerticalSplitter.IsVisible = true;
        HorizontalSplitter.IsVisible = true;
        BottomVerticalSplitter.IsVisible = true;
        
        // Restore normal layout (shader in top-right quadrant)
        Surface.SetValue(Grid.RowProperty, 1);
        Surface.SetValue(Grid.ColumnProperty, 1);
        Surface.SetValue(Grid.RowSpanProperty, 1);
        Surface.SetValue(Grid.ColumnSpanProperty, 1);
        
        LogMessage("Exited performance mode");
    }

    private void OnSaturationChanged(object? sender, Avalonia.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        if (Surface != null && e.NewValue is double value)
        {
            Surface.Saturation = (float)value;
            SaturationValue.Text = value.ToString("F2");
            LogMessage($"Saturation changed to {value:F2}");
        }
    }

    private async Task LoadShaderFiles()
    {
        try
        {
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel?.StorageProvider is not { } storageProvider)
            {
                LogMessage("Unable to access file system - storage provider not available");
                return;
            }

            // Configure file picker options
            var filePickerOptions = new FilePickerOpenOptions
            {
                Title = "Select GLSL Shader Files",
                AllowMultiple = true,
                FileTypeFilter = new[]
                {
                    new FilePickerFileType("GLSL Shader Files")
                    {
                        Patterns = new[] { "*.glsl", "*.frag", "*.vert", "*.comp" }
                    },
                    new FilePickerFileType("All Files")
                    {
                        Patterns = new[] { "*.*" }
                    }
                }
            };

            LogMessage("Opening file dialog to select shader files...");
            var files = await storageProvider.OpenFilePickerAsync(filePickerOptions);

            if (files.Count == 0)
            {
                LogMessage("No files selected");
                return;
            }

            LogMessage($"Selected {files.Count} file(s) for import");

            int successCount = 0;
            int errorCount = 0;

            foreach (var file in files)
            {
                try
                {
                    var fileName = file.Name;
                    var destinationPath = Path.Combine(_shaderDir, fileName);

                    // Check if file already exists
                    if (File.Exists(destinationPath))
                    {
                        LogMessage($"File '{fileName}' already exists in shaders folder - skipping");
                        continue;
                    }

                    // Copy the file to the shaders directory
                    using var sourceStream = await file.OpenReadAsync();
                    using var destinationStream = File.Create(destinationPath);
                    await sourceStream.CopyToAsync(destinationStream);

                    LogMessage($"Successfully imported: {fileName}");
                    successCount++;
                }
                catch (Exception ex)
                {
                    LogMessage($"Error importing '{file.Name}': {ex.Message}");
                    errorCount++;
                }
            }

            // Refresh the shader picker if any files were successfully imported
            if (successCount > 0)
            {
                LogMessage($"Import complete: {successCount} files imported, {errorCount} errors");
                PopulatePicker();
                
                // Auto-select the first newly imported shader
                if (ShaderPicker.ItemsSource is IEnumerable<string> items && items.Any())
                {
                    ShaderPicker.SelectedIndex = 0;
                }
            }
            else
            {
                LogMessage($"Import failed: {errorCount} errors, no files imported");
            }
        }
        catch (Exception ex)
        {
            LogMessage($"Error during file import: {ex.Message}");
        }
    }
}


