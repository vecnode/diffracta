using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using System.IO;
using System.Linq;
using System.Text;

namespace AvaloniaVideoSynth;

public partial class MainWindow : Window {
    private FileSystemWatcher? _watcher;
    private string _shaderDir = Path.Combine(AppContext.BaseDirectory, "Shaders");
    private readonly StringBuilder _logBuffer = new();
    private bool _isFullscreen = false;

    public MainWindow() {
        InitializeComponent();

        Loaded += (_, __) => {
            Directory.CreateDirectory(_shaderDir);
            StatusText.Text = "Initializing shader system";
            LogMessage("Application started");
            LogMessage($"Shader directory: {_shaderDir}");
            Surface.SetLogCallback(LogMessage);
            PopulatePicker();
            SetupWatcher();
            LogMessage("Ready - Select a shader from the dropdown");
        };

        ShaderPicker.SelectionChanged += (_, __) => {
            if (ShaderPicker.SelectedItem is string path && File.Exists(path)) {
                StatusText.Text = $"Compiling {Path.GetFileName(path)}";
                LogMessage($"Loading shader: {Path.GetFileName(path)}");
                Surface.LoadFragmentShaderFromFile(path, out var message);
                StatusText.Text = message;
            }
        };


        ToggleLogButton.Click += (_, __) => {
            if (LogPanel.IsVisible)
            {
                LogPanel.IsVisible = false;
                ToggleLogButton.Content = "Show Log";
            }
            else
            {
                LogPanel.IsVisible = true;
                ToggleLogButton.Content = "Hide Log";
            }
        };

        ToggleControllerButton.Click += (_, __) => {
            if (ControlsPanel.IsVisible)
            {
                ControlsPanel.IsVisible = false;
                ToggleControllerButton.Content = "Show Controller";
            }
            else
            {
                ControlsPanel.IsVisible = true;
                ToggleControllerButton.Content = "Hide Controller";
            }
        };

        FullscreenButton.Click += (_, __) => {
            ToggleFullscreen();
        };

        LoadShaderButton.Click += (_, __) => {
            LogMessage("Load Shader button clicked - functionality not yet implemented");
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

        // Handle Escape key to exit fullscreen
        KeyDown += (_, e) => {
            if (e.Key == Avalonia.Input.Key.Escape && _isFullscreen)
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
        }
        else {
            StatusText.Text = $"Put some .glsl files in {_shaderDir}";
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

    private void EnterFullscreen() {
        _isFullscreen = true;
        WindowState = WindowState.FullScreen;
        FullscreenButton.Content = "Exit Fullscreen";
        
        // Hide all panels in fullscreen
        ControlsPanel.IsVisible = false;
        LogPanel.IsVisible = false;
        ToggleControllerButton.Content = "Show Controller";
        ToggleLogButton.Content = "Show Log";
        
        LogMessage("Entered fullscreen mode - Press Escape to exit");
    }

    private void ExitFullscreen() {
        _isFullscreen = false;
        WindowState = WindowState.Normal;
        FullscreenButton.Content = "Fullscreen";
        LogMessage("Exited fullscreen mode");
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
}


