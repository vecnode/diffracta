using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.Platform.Storage;
using Avalonia.Styling;
using Avalonia.Controls.Templates;
using System.IO;
using System.Linq;
using System.Text;
using System.ComponentModel;

namespace Diffracta;

public partial class MainWindow : Window, INotifyPropertyChanged {
    private FileSystemWatcher? _watcher;
    private string _shaderDir = Path.Combine(AppContext.BaseDirectory, "Shaders");
    private readonly StringBuilder _logBuffer = new();
    private bool _isPerformanceMode = false;
    private MainTempo _globalTempoNumber;
    private DispatcherTimer? _tempoTimer;
    private bool _isTempoRunning = false;
    

    public new event PropertyChangedEventHandler? PropertyChanged;

    public MainWindow() {
        InitializeComponent();
        
        // Initialize global tempo number
        _globalTempoNumber = new MainTempo();
        
        // Set up data binding
        DataContext = this;

        Loaded += (_, __) => {
            Directory.CreateDirectory(_shaderDir);
            LogMessage("Application started");
            LogMessage($"Shader directory: {_shaderDir}");
            Surface.SetLogCallback(LogMessage);
            PopulatePicker();
            SetupWatcher();
            UpdateTabContent();
            LogMessage("Ready - Select a shader from the dropdown");
        };

        ShaderPicker.SelectionChanged += (_, __) => {
            if (ShaderPicker.SelectedItem is string filename) {
                var fullPath = Path.Combine(_shaderDir, filename);
                if (File.Exists(fullPath)) {
                    Surface.LoadFragmentShaderFromFile(fullPath, out var message);
                    UpdateTabContent();
                }
            }
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
            LogScrollViewer.ScrollToEnd();
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

        TouchpadButton.Click += OnTouchpadClicked;
        ResetButton.Click += OnResetButtonClicked;

        // Page navigation event handlers
        Page1Button.Click += (_, __) => SwitchToPage(1);
        Page2Button.Click += (_, __) => SwitchToPage(2);
        Page3Button.Click += (_, __) => SwitchToPage(3);
        Page4Button.Click += (_, __) => SwitchToPage(4);

        // Handle Escape key to exit performance mode
        KeyDown += (_, e) => {
            if (e.Key == Avalonia.Input.Key.Escape && _isPerformanceMode)
            {
                ExitPerformanceMode();
            }
        };
    }

    private void PopulatePicker() {
        var items = Directory.EnumerateFiles(_shaderDir, "*.glsl")
            .OrderBy(p => Path.GetFileName(p))
            .Select(p => Path.GetFileName(p))
            .ToList();

        ShaderPicker.ItemsSource = items;
        if (items.Count > 0) {
            ShaderPicker.SelectedIndex = 0;
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
            // Auto-scroll to bottom
            LogScrollViewer.ScrollToEnd();
        });
    }

    private void TogglePerformanceMode() {
        if (_isPerformanceMode) {
            ExitPerformanceMode();
        } else {
            EnterPerformanceMode();
        }
    }

    private void EnterPerformanceMode() {
        _isPerformanceMode = true;
        PerformanceButton.Content = "Exit Performance";
        
        // Hide all UI panels but keep the shader surface visible
        ControlsPanel.IsVisible = false;
        BottomRightPanel.IsVisible = false;
        VerticalSplitter.IsVisible = false;
        HorizontalSplitter.IsVisible = false;
        
        // Go fullscreen for Performance mode to use full viewport
        WindowState = WindowState.FullScreen;
        
        // Hide mouse cursor in performance mode
        Cursor = Avalonia.Input.Cursor.Parse("None");
        
        // Make the shader surface span the entire viewport
        Surface.SetValue(Grid.RowProperty, 0);
        Surface.SetValue(Grid.ColumnProperty, 0);
        Surface.SetValue(Grid.RowSpanProperty, 3);
        Surface.SetValue(Grid.ColumnSpanProperty, 3);
        
        LogMessage("Entered performance mode - Full viewport shader, Press Escape to exit");
        UpdateTabContent();
    }

    private void ExitPerformanceMode() {
        _isPerformanceMode = false;
        PerformanceButton.Content = "Performance";
        
        // Return to windowed mode
        WindowState = WindowState.Normal;
        
        // Restore mouse cursor
        Cursor = Avalonia.Input.Cursor.Parse("Arrow");
        
        // Show all panels again
        ControlsPanel.IsVisible = true;
        BottomRightPanel.IsVisible = true;
        VerticalSplitter.IsVisible = true;
        HorizontalSplitter.IsVisible = true;
        
        // Restore normal layout (shader in top-right quadrant)
        Surface.SetValue(Grid.RowProperty, 1);
        Surface.SetValue(Grid.ColumnProperty, 2);
        Surface.SetValue(Grid.RowSpanProperty, 1);
        Surface.SetValue(Grid.ColumnSpanProperty, 1);
        
        LogMessage("Exited performance mode");
        UpdateTabContent();
    }

    private void OnSaturationChanged(object? sender, Avalonia.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        if (Surface != null && e.NewValue is double value)
        {
            Surface.Saturation = (float)value;
            Slot1Value.Text = value.ToString("F2");
            UpdateTabContent();
        }
    }

    private void OnPingPongChanged(object? sender, Avalonia.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        if (Surface != null && e.NewValue is double value)
        {
            Surface.PingPongDelay = (float)value;
            Slot2Value.Text = value.ToString("F2");
            UpdateTabContent();
        }
    }

    private void OnSlot1ToggleClicked(object? sender, RoutedEventArgs e) => OnSlotToggleClicked(0, sender, e);
    private void OnSlot2ToggleClicked(object? sender, RoutedEventArgs e) => OnSlotToggleClicked(1, sender, e);
    private void OnSlot3ToggleClicked(object? sender, RoutedEventArgs e) => OnSlotToggleClicked(2, sender, e);
    private void OnSlot4ToggleClicked(object? sender, RoutedEventArgs e) => OnSlotToggleClicked(3, sender, e);
    private void OnSlot5ToggleClicked(object? sender, RoutedEventArgs e) => OnSlotToggleClicked(4, sender, e);

    private void OnSlot1ValueChanged(object? sender, Avalonia.Controls.Primitives.RangeBaseValueChangedEventArgs e) => OnSlotValueChanged(0, sender, e);
    private void OnSlot2ValueChanged(object? sender, Avalonia.Controls.Primitives.RangeBaseValueChangedEventArgs e) => OnSlotValueChanged(1, sender, e);
    private void OnSlot3ValueChanged(object? sender, Avalonia.Controls.Primitives.RangeBaseValueChangedEventArgs e) => OnSlotValueChanged(2, sender, e);
    private void OnSlot4ValueChanged(object? sender, Avalonia.Controls.Primitives.RangeBaseValueChangedEventArgs e) => OnSlotValueChanged(3, sender, e);
    private void OnSlot5ValueChanged(object? sender, Avalonia.Controls.Primitives.RangeBaseValueChangedEventArgs e) => OnSlotValueChanged(4, sender, e);

    private void OnSlotToggleClicked(int slot, object? sender, RoutedEventArgs e)
    {
        if (Surface != null)
        {
            bool newState = !Surface.GetSlotActive(slot);
            Surface.SetSlotActive(slot, newState);
            LogMessage($"Slot {slot + 1} shader {(newState ? "activated" : "deactivated")}");
            
            // Update button appearance
            var button = sender as Button;
            if (button != null)
            {
                button.Content = newState ? "ON" : "OFF";
                button.Background = newState ? 
                    Avalonia.Media.Brushes.Green : Avalonia.Media.Brushes.Red;
            }
            
            UpdateTabContent();
        }
    }

    private void OnSlotValueChanged(int slot, object? sender, Avalonia.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        if (Surface != null && e.NewValue is double value)
        {
            Surface.SetSlotValue(slot, (float)value);
            LogMessage($"Slot {slot + 1} value changed to {value:F2}");
            UpdateTabContent();
        }
    }

    private void OnTouchpadClicked(object? sender, RoutedEventArgs e)
    {
        LogMessage("Touchpad button clicked!");
        Console.WriteLine("Touchpad button clicked!");
    }

    private void OnTempoButtonPressed(object? sender, RoutedEventArgs e)
    {
        LogMessage("=== TEMPO BUTTON CLICKED ===");
        LogMessage($"Tempo button pressed - Current state: {(_isTempoRunning ? "Running" : "Stopped")}");
        
        if (_isTempoRunning)
        {
            StopTempo();
        }
        else
        {
            StartTempo();
        }
    }

    private void OnResetButtonClicked(object? sender, RoutedEventArgs e)
    {
        StopTempo();
        _globalTempoNumber.Reset();
        LogMessage("Tempo reset");
    }

    private void StartTempo()
    {
        _isTempoRunning = true;
        _tempoTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _tempoTimer.Tick += (_, __) => {
            _globalTempoNumber.Increment();
            LogMessage($"Global Tempo: {_globalTempoNumber.TimeDisplay} (Seconds: {_globalTempoNumber.Seconds})");
        };
        _tempoTimer.Start();
        
        // Notify UI of button state changes
        OnPropertyChanged(nameof(TempoButtonText));
        OnPropertyChanged(nameof(TempoButtonBackground));
        
        LogMessage("Tempo started");
    }

    private void StopTempo()
    {
        _isTempoRunning = false;
        _tempoTimer?.Stop();
        _tempoTimer = null;
        
        // Notify UI of button state changes
        OnPropertyChanged(nameof(TempoButtonText));
        OnPropertyChanged(nameof(TempoButtonBackground));
        
        LogMessage($"Tempo stopped - Total time: {_globalTempoNumber.TimeDisplay}");
    }

    // Properties for data binding
    public MainTempo Tempo => _globalTempoNumber;
    
    public string TempoButtonText => _isTempoRunning ? "Stop Clock" : "Start Clock";
    
    public string TempoButtonBackground => _isTempoRunning ? "#ff8c00" : "#d3d3d3";

    private void UpdateTabContent()
    {
        // Update Info tab
        if (ShaderPicker.SelectedItem is string selectedShader)
        {
            var fullPath = Path.Combine(_shaderDir, selectedShader);
            var fileInfo = new FileInfo(fullPath);
            ShaderInfoText.Text = $"Current: {selectedShader}\nSize: {fileInfo.Length} bytes\nModified: {fileInfo.LastWriteTime:yyyy-MM-dd HH:mm:ss}";
        }
        else
        {
            ShaderInfoText.Text = "No shader loaded";
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

    protected virtual void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    // Page navigation method
    private void SwitchToPage(int pageNumber)
    {
        switch (pageNumber)
        {
            case 1:
                // Controls page is already the default content
                PageContentControl.Content = ControlsPage;
                LogMessage("Switched to Controls page");
                break;
            case 2:
                PageContentControl.Content = CreateToolsPage();
                LogMessage("Switched to Tools page");
                break;
            case 3:
                PageContentControl.Content = CreateSettingsPage();
                LogMessage("Switched to Settings page");
                break;
            case 4:
                PageContentControl.Content = CreateHelpPage();
                LogMessage("Switched to Help page");
                break;
        }
    }

    private Avalonia.Controls.Control CreateToolsPage()
    {
        var scrollViewer = new ScrollViewer { Margin = new Avalonia.Thickness(8) };
        var stackPanel = new StackPanel { Spacing = 12 };
        
        stackPanel.Children.Add(new TextBlock 
        { 
            Text = "Tools", 
            Foreground = Avalonia.Media.Brushes.White, 
            FontSize = 14, 
        });

      
        var toolsStack = new StackPanel { Spacing = 8 };
        toolsStack.Children.Add(new Button { Content = "Pad 1", Width = 60, Height = 60, Background = Avalonia.Media.Brushes.DarkGray, Foreground = Avalonia.Media.Brushes.White, BorderThickness = new Avalonia.Thickness(0), CornerRadius = new Avalonia.CornerRadius(4), HorizontalContentAlignment = Avalonia.Layout.HorizontalAlignment.Center, VerticalContentAlignment = Avalonia.Layout.VerticalAlignment.Center });
        toolsStack.Children.Add(new Button { Content = "Pad 2", Width = 60, Height = 60, Background = Avalonia.Media.Brushes.DarkGray, Foreground = Avalonia.Media.Brushes.White, BorderThickness = new Avalonia.Thickness(0), CornerRadius = new Avalonia.CornerRadius(4), HorizontalContentAlignment = Avalonia.Layout.HorizontalAlignment.Center, VerticalContentAlignment = Avalonia.Layout.VerticalAlignment.Center });
        toolsStack.Children.Add(new Button { Content = "Pad 3", Width = 60, Height = 60, Background = Avalonia.Media.Brushes.DarkGray, Foreground = Avalonia.Media.Brushes.White, BorderThickness = new Avalonia.Thickness(0), CornerRadius = new Avalonia.CornerRadius(4), HorizontalContentAlignment = Avalonia.Layout.HorizontalAlignment.Center, VerticalContentAlignment = Avalonia.Layout.VerticalAlignment.Center });
        
        stackPanel.Children.Add(toolsStack);
        
        // Directory Selection Section
        var directorySection = new StackPanel { Spacing = 8, Margin = new Avalonia.Thickness(0, 16, 0, 0) };
        
        // Browse button only
        var browseButton = new Button 
        { 
            Content = "Browse Directory", 
            Width = 150, 
            Height = 30,
            Background = Avalonia.Media.Brushes.DarkGray,
            Foreground = Avalonia.Media.Brushes.White,
            BorderThickness = new Avalonia.Thickness(0),
            CornerRadius = new Avalonia.CornerRadius(4),
            HorizontalContentAlignment = Avalonia.Layout.HorizontalAlignment.Center,
            VerticalContentAlignment = Avalonia.Layout.VerticalAlignment.Center
        };
        
        // Directory listing
        var directoryListBox = new ListBox 
        { 
            Width = double.NaN, // Full width
            Height = 200,
            Background = Avalonia.Media.Brushes.DarkGray,
            Foreground = Avalonia.Media.Brushes.White,
            BorderBrush = Avalonia.Media.Brushes.Gray,
            BorderThickness = new Avalonia.Thickness(1),
            CornerRadius = new Avalonia.CornerRadius(4),
            Margin = new Avalonia.Thickness(0, 8, 0, 0)
        };
        
        // Add click handler for browse button (after ListBox is declared)
        browseButton.Click += async (_, __) => await BrowseDirectory(directoryListBox);
        
        // No sample items - ready for actual directory contents
        
        directorySection.Children.Add(browseButton);
        directorySection.Children.Add(directoryListBox);
        
        stackPanel.Children.Add(directorySection);
        scrollViewer.Content = stackPanel;
        
        return scrollViewer;
    }

    private async Task BrowseDirectory(ListBox directoryListBox)
    {
        try
        {
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel?.StorageProvider is not { } storageProvider)
            {
                LogMessage("Unable to access file system - storage provider not available");
                return;
            }

            // Configure folder picker options
            var folderPickerOptions = new FolderPickerOpenOptions
            {
                Title = "Select Directory",
                AllowMultiple = false
            };

            LogMessage("Opening folder dialog...");
            var folders = await storageProvider.OpenFolderPickerAsync(folderPickerOptions);

            if (folders.Count > 0)
            {
                var selectedFolder = folders[0];
                var folderPath = selectedFolder.Path.LocalPath;
                
                // Load directory contents
                LoadDirectoryContents(folderPath, directoryListBox);
                
                LogMessage($"Selected directory: {folderPath}");
            }
            else
            {
                LogMessage("No directory selected");
            }
        }
        catch (Exception ex)
        {
            LogMessage($"Error browsing directory: {ex.Message}");
        }
    }

    private void LoadDirectoryContents(string directoryPath, ListBox directoryListBox)
    {
        try
        {
            // Clear existing items
            directoryListBox.Items.Clear();
            
            if (!Directory.Exists(directoryPath))
            {
                LogMessage($"Directory does not exist: {directoryPath}");
                return;
            }

            // Get directories first
            var directories = Directory.GetDirectories(directoryPath)
                .Select(Path.GetFileName)
                .OrderBy(name => name)
                .Select(name => $"📁 {name}")
                .ToList();

            // Get files
            var files = Directory.GetFiles(directoryPath)
                .Select(Path.GetFileName)
                .Where(name => !string.IsNullOrEmpty(name))
                .OrderBy(name => name)
                .Select(name => GetFileIcon(name ?? "") + " " + name)
                .ToList();

            // Add directories first, then files
            foreach (var dir in directories)
            {
                directoryListBox.Items.Add(dir);
            }
            
            foreach (var file in files)
            {
                directoryListBox.Items.Add(file);
            }

            LogMessage($"Loaded {directories.Count} directories and {files.Count} files");
        }
        catch (Exception ex)
        {
            LogMessage($"Error loading directory contents: {ex.Message}");
        }
    }

    private string GetFileIcon(string fileName)
    {
        if (string.IsNullOrEmpty(fileName))
            return "📄";
            
        var extension = Path.GetExtension(fileName).ToLower();
        return extension switch
        {
            ".jpg" or ".jpeg" or ".png" or ".gif" or ".bmp" or ".webp" => "🖼️",
            ".mp4" or ".avi" or ".mov" or ".mkv" or ".wmv" => "🎥",
            ".mp3" or ".wav" or ".flac" or ".aac" => "🎵",
            ".pdf" => "📄",
            ".doc" or ".docx" => "📝",
            ".txt" => "📄",
            ".zip" or ".rar" or ".7z" => "📦",
            ".exe" => "⚙️",
            _ => "📄"
        };
    }

    private Avalonia.Controls.Control CreateSettingsPage()
    {
        var scrollViewer = new ScrollViewer { Margin = new Avalonia.Thickness(8) };
        var stackPanel = new StackPanel { Spacing = 12 };
        
        stackPanel.Children.Add(new TextBlock 
        { 
            Text = "Settings", 
            Foreground = Avalonia.Media.Brushes.White, 
            FontSize = 14, 
        });
        scrollViewer.Content = stackPanel;
        
        return scrollViewer;
    }

    private Avalonia.Controls.Control CreateHelpPage()
    {
        var scrollViewer = new ScrollViewer { Margin = new Avalonia.Thickness(8) };
        var stackPanel = new StackPanel { Spacing = 12 };
        
        stackPanel.Children.Add(new TextBlock 
        { 
            Text = "Help & Documentation", 
            Foreground = Avalonia.Media.Brushes.White, 
            FontSize = 14, 
        });
        
      
        scrollViewer.Content = stackPanel;
        
        return scrollViewer;
    }

}


