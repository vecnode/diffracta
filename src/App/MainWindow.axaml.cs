using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.Platform.Storage;
using Avalonia.Styling;
using Avalonia.Controls.Templates;
using Avalonia.VisualTree;
using System.IO;
using System.Linq;
using System.Text;
using System.ComponentModel;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Concurrent;
namespace Diffracta;

// ============================================================================
// MAIN WINDOW - Primary application window for Diffracta shader application
// ============================================================================
// This class manages the main UI, shader loading, post-processing pipeline,
    // file management.
// ============================================================================
public partial class MainWindow : Window, INotifyPropertyChanged {
    
    // ========================================================================
    // PRIVATE FIELDS - Application State
    // ========================================================================
    
    // File system and shader management
    private FileSystemWatcher? _watcher;
    private string _shaderDir = System.IO.Path.Combine(AppContext.BaseDirectory, "Shaders");
    
    // Logging system
    private readonly StringBuilder _logBuffer = new();
    private bool _isLogPanelVisible = false;
    
    // Child window management
    private ChildWindow2? _childWindow2;

    // Media and project state
    private readonly ObservableCollection<string> _mediaDirectories = new();
    private readonly MainTempo _globalTempoNumber = new();
    private readonly bool[] _slotActiveStates = new bool[3];
    private readonly float[] _slotValues = new float[3];
    private readonly List<string> _fullDirectoryItems = new();
    private string _currentDirectoryPath = string.Empty;
    private int _projectWidth = 1920;
    private int _projectHeight = 1080;

    // Global timer state
    private const int GLOBAL_TIMER_INTERVAL_MS = 16;
    private DispatcherTimer? _globalUpdateTimer;
    private readonly List<Action> _timerCallbacks = new();

    // INotifyPropertyChanged event
    public new event PropertyChangedEventHandler? PropertyChanged;
    
    /// <summary>
    /// Gets the global list of media directory paths (accessible from all pages).
    /// </summary>
    public ObservableCollection<string> MediaDirectories => _mediaDirectories;
    
    /// <summary>
    /// Legacy tempo binding source for compiled controls.
    /// </summary>
    public MainTempo Tempo => _globalTempoNumber;
    
    /// <summary>
    /// Adds a directory path to the media directories list if it doesn't already exist.
    /// </summary>
    public void AddMediaDirectory(string directoryPath)
    {
        if (!string.IsNullOrWhiteSpace(directoryPath) && 
            System.IO.Directory.Exists(directoryPath))
        {
            if (_mediaDirectories.Contains(directoryPath))
            {
                LogMessage($"Directory already in list: {directoryPath}");
                return;
            }
            
            _mediaDirectories.Add(directoryPath);
            LogMessage($"Added media directory: {directoryPath} (Total: {_mediaDirectories.Count})");
            
            // Force UI update notification
            OnPropertyChanged(nameof(MediaDirectories));
        }
        else
        {
            LogMessage($"Invalid directory path: {directoryPath ?? "null"}");
        }
    }
    
    // ========================================================================
    // CONSTRUCTOR - Initialize window and wire up event handlers
    // ========================================================================
    
    public MainWindow() {
        InitializeComponent();
        
        // Set up data binding
        DataContext = this;

        // Window lifecycle events
        Loaded += (_, __) => {
            try
            {
                Directory.CreateDirectory(_shaderDir);
                LogMessage("Application started");
                LogMessage($"Shader directory: {_shaderDir}");
                
                if (Surface != null)
                {
                    Surface.SetLogCallback(LogMessage);
                    // Initialize project size
                    Surface.SetProjectSize(_projectWidth, _projectHeight);
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("WARNING: Surface is null in Loaded event");
                }
                
                SetupWatcher();
                UpdateTabContent();
                
                // Start centralized global update timer
                StartGlobalUpdateTimer();
                
                // Wire up processing node controls (clickable rectangles and sliders)
                try
                {
                    // Wire up processing node controls via the UserControl
                    var nodesListBox = this.FindControl<Utils_NodesListBox>("NodesListBox");
                    if (nodesListBox != null)
                    {
                        nodesListBox.Surface = Surface;
                    }
                }
                catch (Exception ex)
                {
                    LogMessage($"Error wiring up processing node controls: {ex.Message}");
                    System.Diagnostics.Debug.WriteLine($"Error wiring up processing node controls: {ex}");
                }
                
                // Wire up MenuBar styling and hover effects
                WireUpMenuBarStyling();
                WireUpMainWindowControls();
                
                // Initialize with controls page
                SwitchToPage(1);
                
                LogMessage("Ready - Select a shader from the dropdown");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"FATAL ERROR in Loaded event: {ex.Message}\n{ex.StackTrace}");
                // Try to log to file
                try
                {
                    var logPath = System.IO.Path.Combine(AppContext.BaseDirectory, "error.log");
                    System.IO.File.AppendAllText(logPath, $"[{DateTime.Now}] FATAL ERROR in Loaded: {ex.Message}\n{ex.StackTrace}\n\n");
                }
                catch (Exception logEx)
                {
                    System.Diagnostics.Debug.WriteLine($"Could not write to error log: {logEx.Message}");
                }
            }
        };
    }

    /// <summary>
    /// Populates media picker with shader files only.
    /// </summary>
    private void PopulatePicker(Page1 page)
    {
        var shaderItems = Directory.GetFiles(_shaderDir, "*.glsl")
            .Select(System.IO.Path.GetFileName)
            .Where(name => !string.IsNullOrEmpty(name))
            .Select(name => name!)
            .OrderBy(name => name)
            .ToList();

        var mediaPicker = page.FindControl<Utils_ComboBox>("MediaPicker");
        if (mediaPicker != null)
        {
            mediaPicker.ItemsSource = shaderItems;
            if (shaderItems.Count > 0)
            {
                mediaPicker.SelectedIndex = 0;
            }
        }
    }

    /// <summary>
    /// Wires up top-level window controls and menu items.
    /// </summary>
    private void WireUpMainWindowControls()
    {
        var logsButton = this.FindControl<Button>("LogsButton");
        if (logsButton != null)
        {
            logsButton.Click += (_, __) => ToggleLogPanel();
        }

        var clearLogButton = this.FindControl<Button>("ClearLogButton");
        if (clearLogButton != null)
        {
            clearLogButton.Click += (_, __) =>
            {
                _logBuffer.Clear();
                LogTextBox.Text = string.Empty;
            };
        }

        var copyLogButton = this.FindControl<Button>("CopyLogButton");
        if (copyLogButton != null)
        {
            copyLogButton.Click += async (_, __) =>
            {
                var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
                if (clipboard != null)
                {
                    await clipboard.SetTextAsync(_logBuffer.ToString());
                    LogMessage("Copied logs to clipboard");
                }
                else
                {
                    LogMessage("Clipboard not available");
                }
            };
        }

        var page1Button = this.FindControl<Button>("Page1Button");
        if (page1Button != null)
        {
            page1Button.Click += (_, __) => SwitchToPage(1);
        }

        var page2Button = this.FindControl<Button>("Page2Button");
        if (page2Button != null)
        {
            page2Button.Click += (_, __) => SwitchToPage(2);
        }

        var childWindow2MenuItem = this.FindControl<MenuItem>("ChildWindow2MenuItem");
        if (childWindow2MenuItem != null)
        {
            childWindow2MenuItem.Click += (_, __) => ToggleChildWindow2();
        }
    }

    /// <summary>
    /// Sets up file system watcher to monitor shader directory for changes
    /// </summary>
    private void SetupWatcher() {
        _watcher = new FileSystemWatcher(_shaderDir, "*.glsl") {
            IncludeSubdirectories = false,
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.FileName
        };
        _watcher.Created += (_, __) => Dispatcher.UIThread.Post(() => RefreshCurrentPage());
        _watcher.Deleted += (_, __) => Dispatcher.UIThread.Post(() => RefreshCurrentPage());
        _watcher.Renamed += (_, __) => Dispatcher.UIThread.Post(() => RefreshCurrentPage());
        _watcher.EnableRaisingEvents = true;
    }
    
    /// <summary>
    /// Refreshes the current page when shader files change
    /// </summary>
    private void RefreshCurrentPage()
    {
        // Refresh the current page (typically the controls page)
        if (PageContentControl.Content is Page1 controlsPage)
        {
            PopulatePicker(controlsPage);
        }
    }

    /// <summary>
    /// Applies menu bar visual behavior.
    /// </summary>
    private void WireUpMenuBarStyling()
    {
        // Intentionally left minimal; menu behavior is driven primarily by XAML styles.
    }

    
    
    // ========================================================================
    // LOGGING SYSTEM - Message logging and log panel management
    // ========================================================================
    
    /// <summary>
    /// Logs a message with timestamp to the log buffer and updates UI
    /// Thread-safe: Uses Dispatcher to update UI from any thread
    /// </summary>
    public void LogMessage(string message) {
        var timestamp = DateTime.Now.ToString("HH:mm:ss");
        var logEntry = $"[{timestamp}] {message}";
        
        _logBuffer.AppendLine(logEntry);
        
        Dispatcher.UIThread.Post(() => {
            LogTextBox.Text = _logBuffer.ToString();
            // Auto-scroll to bottom
            LogScrollViewer.ScrollToEnd();
        });
    }

    /// <summary>
    /// Toggles the visibility of the log panel
    /// </summary>
    private void ToggleLogPanel()
    {
        _isLogPanelVisible = !_isLogPanelVisible;
        LogPopupPanel.IsVisible = _isLogPanelVisible;
        
        if (_isLogPanelVisible)
        {
            LogMessage("Log panel opened");
        }
        else
        {
            LogMessage("Log panel closed");
        }
    }
    
    // ========================================================================
    // POST-PROCESS SLOT MANAGEMENT - Shader effect slot controls
    // ========================================================================
    
    // Event handler wrappers for slot toggles
    private void OnSlot1ToggleClicked(object? sender, RoutedEventArgs e) => OnSlotToggleClicked(0, sender, e);
    private void OnSlot2ToggleClicked(object? sender, RoutedEventArgs e) => OnSlotToggleClicked(1, sender, e);
    private void OnSlot3ToggleClicked(object? sender, RoutedEventArgs e) => OnSlotToggleClicked(2, sender, e);

    // Event handler wrappers for slot value changes
    private void OnSlot1ValueChanged(object? sender, Avalonia.Controls.Primitives.RangeBaseValueChangedEventArgs e) => OnSlotValueChanged(0, sender, e);
    private void OnSlot2ValueChanged(object? sender, Avalonia.Controls.Primitives.RangeBaseValueChangedEventArgs e) => OnSlotValueChanged(1, sender, e);
    private void OnSlot3ValueChanged(object? sender, Avalonia.Controls.Primitives.RangeBaseValueChangedEventArgs e) => OnSlotValueChanged(2, sender, e);

    /// <summary>
    /// Handles slot toggle button clicks - activates/deactivates post-process shader slots
    /// </summary>
    private void OnSlotToggleClicked(int slot, object? sender, RoutedEventArgs e)
    {
        if (Surface != null)
        {
            bool newState = !Surface.GetSlotActive(slot);
            Surface.SetSlotActive(slot, newState);
            _slotActiveStates[slot] = newState; // Store state
            LogMessage($"Slot {slot + 1} shader {(newState ? "activated" : "deactivated")}");
            
            // Update button appearance
            var button = sender as Button;
            if (button != null)
            {
                button.Content = newState ? "ON" : "OFF";
                button.Background = newState ? 
                    Avalonia.Media.SolidColorBrush.Parse("#ff8c00") : Avalonia.Media.SolidColorBrush.Parse("#d3d3d3");
            }
            
            // Sync Child Window 2 if open
            SyncChildWindow2();
            
            UpdateTabContent();
        }
    }

    /// <summary>
    /// Handles slot slider value changes - updates post-process shader parameter values
    /// </summary>
    private void OnSlotValueChanged(int slot, object? sender, Avalonia.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        if (Surface != null && e.NewValue is double value)
        {
            Surface.SetSlotValue(slot, (float)value);
            _slotValues[slot] = (float)value; // Store value
            LogMessage($"Slot {slot + 1} value changed to {value:F2}");
            
            // Update the UI text block to show the current value from Page1
            if (PageContentControl.Content is Page1 controlsPage)
            {
                string textBlockName = $"Slot{slot + 1}Value";
                var textBlock = controlsPage.FindControl<TextBlock>(textBlockName);
                if (textBlock != null)
                {
                    textBlock.Text = value.ToString("F2");
                }
            }
            
            // Sync Child Window 2 if open
            SyncChildWindow2();
            
            UpdateTabContent();
        }
    }
    
    /// <summary>
    /// Gets the active state of a post-process slot (for state restoration)
    /// </summary>
    public bool GetSlotActive(int slot) => _slotActiveStates[slot];
    
    /// <summary>
    /// Gets the value of a post-process slot (for state restoration)
    /// </summary>
    public float GetSlotValue(int slot) => _slotValues[slot];
    
    /// <summary>
    /// Project width for consistent output resolution (globally changeable)
    /// </summary>
    public string ProjectWidth
    {
        get => _projectWidth.ToString();
        set
        {
            // Allow any positive integer value
            if (int.TryParse(value, out int width) && width > 0)
            {
                if (_projectWidth != width)
                {
                    _projectWidth = width;
                    OnPropertyChanged(nameof(ProjectWidth));
                    // Update shader surface with new project size
                    if (Surface != null)
                    {
                        Surface.SetProjectSize(_projectWidth, _projectHeight);
                        LogMessage($"Project width changed to: {_projectWidth}");
                    }
                }
            }
        }
    }
    
    /// <summary>
    /// Project height for consistent output resolution (globally changeable)
    /// </summary>
    public string ProjectHeight
    {
        get => _projectHeight.ToString();
        set
        {
            // Allow any positive integer value
            if (int.TryParse(value, out int height) && height > 0)
            {
                if (_projectHeight != height)
                {
                    _projectHeight = height;
                    OnPropertyChanged(nameof(ProjectHeight));
                    // Update shader surface with new project size
                    if (Surface != null)
                    {
                        Surface.SetProjectSize(_projectWidth, _projectHeight);
                        LogMessage($"Project height changed to: {_projectHeight}");
                    }
                }
            }
        }
    }
    
    /// <summary>
    /// Gets the current project width as integer
    /// </summary>
    public int ProjectWidthInt => _projectWidth;
    
    /// <summary>
    /// Gets the current project height as integer
    /// </summary>
    public int ProjectHeightInt => _projectHeight;
    
    // ========================================================================
    // UI UPDATES - Tab content and shader nodes visualization
    // ========================================================================
    
    /// <summary>
    /// Updates the content of tabs with current information
    /// </summary>
    private void UpdateTabContent()
    {
    }

    /// <summary>
    /// Starts the centralized global update timer
    /// All periodic updates use this single timer to prevent thread leaks and improve efficiency
    /// </summary>
    private void StartGlobalUpdateTimer()
    {
        if (_globalUpdateTimer != null) return; // Already running
        
        _globalUpdateTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(GLOBAL_TIMER_INTERVAL_MS)
        };
        
        _globalUpdateTimer.Tick += (_, __) => {
            try
            {
                // Execute all registered callbacks
                foreach (var callback in _timerCallbacks)
                {
                    try
                    {
                        callback();
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Timer callback error: {ex.Message}");
                    }
                }
                
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Global timer error: {ex.Message}");
            }
        };
        
        // Register shader nodes visualization update (runs every tick = 10 times per second)
        RegisterTimerCallback(() => {
            var nodesListBox = this.FindControl<Utils_NodesListBox>("NodesListBox");
            nodesListBox?.UpdateShaderNodesVisualization();
        });
        
        _globalUpdateTimer.Start();
        System.Diagnostics.Debug.WriteLine("Global update timer started");
    }

    /// <summary>
    /// Stops the global update timer and cleans up all callbacks
    /// </summary>
    private void StopGlobalUpdateTimer()
    {
        if (_globalUpdateTimer != null)
        {
            _globalUpdateTimer.Stop();
            _globalUpdateTimer = null;
        }
        
        _timerCallbacks.Clear();
        System.Diagnostics.Debug.WriteLine("Global update timer stopped and cleaned up");
    }
    
    /// <summary>
    /// Registers a callback to be executed on each timer tick
    /// </summary>
    private void RegisterTimerCallback(Action callback)
    {
        if (callback != null && !_timerCallbacks.Contains(callback))
        {
            _timerCallbacks.Add(callback);
        }
    }
    

    
    // ========================================================================
    // PAGE NAVIGATION - Multi-page UI management
    // ========================================================================
    
    /// <summary>
    /// Switches to the specified page number (1-4)
    /// </summary>
    private void SwitchToPage(int pageNumber)
    {
        switch (pageNumber)
        {
            case 1:
                var controlsPage = new Page1();
                PageContentControl.Content = controlsPage;
                Page1_WireUp(controlsPage);
                PopulatePicker(controlsPage);
                LogMessage("Switched to Controls page");
                break;
            case 2:
                var toolsPage = new Page2();
                PageContentControl.Content = toolsPage;
                toolsPage.SetParentWindow(this);
                WireUpToolsPage(toolsPage);
                LogMessage("Switched to Tools page");
                break;
        }
    }

    /// <summary>
    /// Wires up event handlers for Page1 (Controls page) - media selection, tempo, slots
    /// </summary>
    private void Page1_WireUp(Page1 page)
    {
        // Find controls and wire up events
        var mediaPicker = page.FindControl<Utils_ComboBox>("MediaPicker");
        var applyButton = page.FindControl<Button>("ApplyButton");
        var previewSurface = page.FindControl<Diffracta.Graphics.ShaderSurface>("Page1MainTexturePreview");
        var projectWidthInput = page.FindControl<TextBox>("ProjectWidthInput");
        var projectHeightInput = page.FindControl<TextBox>("ProjectHeightInput");

        if (previewSurface != null)
        {
            previewSurface.SetProjectSize(_projectWidth, _projectHeight);
        }
        
        // Wire up project size inputs - changes are handled via data binding
        // The TextBox controls are bound to ProjectWidth/ProjectHeight properties
        // which automatically update the shader surface when changed
        
        // Wire up slot controls
        var slot1Toggle = page.FindControl<Button>("Slot1Toggle");
        var slot2Toggle = page.FindControl<Button>("Slot2Toggle");
        var slot3Toggle = page.FindControl<Button>("Slot3Toggle");
        
        if (slot1Toggle != null) slot1Toggle.Click += OnSlot1ToggleClicked;
        if (slot2Toggle != null) slot2Toggle.Click += OnSlot2ToggleClicked;
        if (slot3Toggle != null) slot3Toggle.Click += OnSlot3ToggleClicked;
        
        var slot1Slider = page.FindControl<Slider>("Slot1Slider");
        var slot2Slider = page.FindControl<Slider>("Slot2Slider");
        var slot3Slider = page.FindControl<Slider>("Slot3Slider");
        
        if (slot1Slider != null) slot1Slider.ValueChanged += OnSlot1ValueChanged;
        if (slot2Slider != null) slot2Slider.ValueChanged += OnSlot2ValueChanged;
        if (slot3Slider != null) slot3Slider.ValueChanged += OnSlot3ValueChanged;

        if (applyButton != null)
        {
            applyButton.Click += (_, __) => ApplySelectedShader(page);
        }
    }

    /// <summary>
    /// Loads the selected shader from Page1 into the main surface.
    /// </summary>
    private void ApplySelectedShader(Page1 page)
    {
        var mediaPicker = page.FindControl<Utils_ComboBox>("MediaPicker");
        if (mediaPicker?.SelectedItem is not string selectedShader || string.IsNullOrWhiteSpace(selectedShader))
        {
            LogMessage("No shader selected");
            return;
        }

        var shaderPath = System.IO.Path.Combine(_shaderDir, selectedShader);
        if (!File.Exists(shaderPath))
        {
            LogMessage($"Shader file not found: {selectedShader}");
            return;
        }

        if (Surface == null)
        {
            LogMessage("Shader surface not available");
            return;
        }

        Surface.LoadFragmentShaderFromFile(shaderPath, out var message);
        LogMessage($"Applied shader: {selectedShader}");
        LogMessage(message);

        var previewSurface = page.FindControl<Diffracta.Graphics.ShaderSurface>("Page1MainTexturePreview");
        if (previewSurface != null)
        {
            previewSurface.SetProjectSize(_projectWidth, _projectHeight);
            previewSurface.LoadFragmentShaderFromFile(shaderPath, out _);
        }

        if (_childWindow2 != null && _childWindow2.IsVisible)
        {
            _childWindow2.LoadShaderFromFile(shaderPath);
            _childWindow2.SyncShaderState();
        }
    }
    
    /// <summary>
    /// Wires up event handlers for Page2 (Tools page) - directory browsing
    /// </summary>
    private void WireUpToolsPage(Page2 page)
    {
        var directoryBox = page.FindControl<Utils_DirectoryBox>("DirectoryBox");
        if (directoryBox == null) return;
        
        var browseButton = directoryBox.BrowseButton;
        var upButton = directoryBox.UpButton;
        var openButton = directoryBox.OpenButton;
        var directoryListBox = directoryBox.DirectoryListBox;
        var directoryPathTextBox = directoryBox.DirectoryPathTextBox;
        
        if (browseButton != null && directoryListBox != null)
        {
            browseButton.Click += (_, __) => BrowseDirectory(directoryListBox, directoryPathTextBox);
        }
        
        if (upButton != null && directoryListBox != null && directoryPathTextBox != null)
        {
            upButton.Click += (_, __) =>
            {
                var currentPath = directoryPathTextBox.Text?.Trim() ?? string.Empty;
                
                if (string.IsNullOrWhiteSpace(currentPath))
                {
                    LogMessage("No directory path to navigate from");
                    return;
                }
                
                if (!Directory.Exists(currentPath))
                {
                    LogMessage($"Current path does not exist: {currentPath}");
                    return;
                }
                
                // Get parent directory
                var parentPath = Directory.GetParent(currentPath)?.FullName;
                
                if (string.IsNullOrEmpty(parentPath))
                {
                    LogMessage("Already at root directory");
                    return;
                }
                
                // Update TextBox with parent path
                directoryPathTextBox.Text = parentPath;
                
                // Load parent directory contents
                LoadDirectoryContents(parentPath, directoryListBox);
                
                LogMessage($"Navigated to parent: {parentPath}");
            };
        }
        
        if (openButton != null && directoryPathTextBox != null)
        {
            openButton.Click += (_, __) =>
            {
                var currentPath = directoryPathTextBox.Text?.Trim() ?? string.Empty;
                
                if (string.IsNullOrWhiteSpace(currentPath))
                {
                    LogMessage("No directory path to open");
                    return;
                }
                
                if (!Directory.Exists(currentPath))
                {
                    LogMessage($"Directory does not exist: {currentPath}");
                    return;
                }
                
                // Open Windows Explorer with the current path
                try
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = currentPath,
                        UseShellExecute = true
                    });
                    LogMessage($"Opened in Explorer: {currentPath}");
                }
                catch (Exception ex)
                {
                    LogMessage($"Failed to open Explorer: {ex.Message}");
                }
            };
        }
        
        // Handle folder navigation when clicking on a directory item
        if (directoryListBox != null && directoryPathTextBox != null)
        {
            directoryListBox.DoubleTapped += (_, e) =>
            {
                var selectedItem = directoryListBox.SelectedItem as string;
                if (selectedItem != null && selectedItem.StartsWith("[DIR]"))
                {
                    // Extract folder name (remove the [DIR] prefix and space)
                    var folderName = selectedItem.Substring(5).Trim();
                    
                    // Build new path
                    var newPath = System.IO.Path.Combine(_currentDirectoryPath, folderName);
                    
                    // Update TextBox
                    directoryPathTextBox.Text = newPath;
                    
                    // Load directory contents
                    LoadDirectoryContents(newPath, directoryListBox);
                    
                    LogMessage($"Navigated to: {newPath}");
                }
            };
        }
        
        if (directoryPathTextBox != null && directoryListBox != null)
        {
            directoryPathTextBox.TextChanged += (_, __) => 
            {
                var text = directoryPathTextBox.Text ?? string.Empty;
                
                // Check if the text is a valid directory path
                if (!string.IsNullOrWhiteSpace(text) && Directory.Exists(text))
                {
                    // If it's a valid directory and different from current, load it
                    if (text != _currentDirectoryPath)
                    {
                        LoadDirectoryContents(text, directoryListBox);
                    }
                    else
                    {
                        // Same directory, show all items (no filter)
                        FilterDirectoryList(directoryListBox, string.Empty);
                    }
                }
                else if (!string.IsNullOrEmpty(_currentDirectoryPath) && Directory.Exists(_currentDirectoryPath))
                {
                    // We have a loaded directory, extract filter text
                    // If text starts with the current directory path, extract the part after it
                    string filterText = text;
                    if (text.StartsWith(_currentDirectoryPath, StringComparison.OrdinalIgnoreCase))
                    {
                        var remaining = text.Substring(_currentDirectoryPath.Length).TrimStart('\\', '/');
                        filterText = remaining;
                    }
                    
                    // Filter the items based on the extracted filter text
                    FilterDirectoryList(directoryListBox, filterText);
                }
                else
                {
                    // No valid directory, clear the list
                    directoryListBox.Items.Clear();
                }
            };
        }
    }
    
    // ========================================================================
    // DIRECTORY BROWSING - File system navigation utilities
    // ========================================================================
    
    /// <summary>
    /// Browses and loads a directory into the directory list box
    /// </summary>
    private void BrowseDirectory(ListBox directoryListBox, TextBox? directoryPathTextBox)
    {
        try
        {
            // Get path from TextBox
            var folderPath = directoryPathTextBox?.Text?.Trim() ?? string.Empty;
            
            LogMessage($"Browse button clicked. Path from TextBox: '{folderPath}'");
            
            if (string.IsNullOrWhiteSpace(folderPath))
            {
                LogMessage("Please enter a directory path in the text box");
                return;
            }
            
            if (directoryListBox == null)
            {
                LogMessage("DirectoryListBox is null!");
                return;
            }
            
            if (!Directory.Exists(folderPath))
            {
                LogMessage($"Directory does not exist: {folderPath}");
                return;
            }
            
            LogMessage($"Directory exists. Loading contents...");
            
            // Load directory contents
            LoadDirectoryContents(folderPath, directoryListBox);
            
            // Make ListBox visible
            directoryListBox.IsVisible = true;
            
            LogMessage($"Loaded directory: {folderPath}");
        }
        catch (Exception ex)
        {
            LogMessage($"Error browsing directory: {ex.Message}");
            LogMessage($"Stack trace: {ex.StackTrace}");
        }
    }

    /// <summary>
    /// Loads directory contents into the list box (directories and files)
    /// </summary>
    private void LoadDirectoryContents(string directoryPath, ListBox directoryListBox)
    {
        try
        {
            // Clear existing items
            directoryListBox.Items.Clear();
            _fullDirectoryItems.Clear();
            _currentDirectoryPath = directoryPath;
            
            if (!Directory.Exists(directoryPath))
            {
                LogMessage($"Directory does not exist: {directoryPath}");
                return;
            }

            // Get directories first
            var directories = Directory.GetDirectories(directoryPath)
                .Select(System.IO.Path.GetFileName)
                .OrderBy(name => name)
                .Select(name => $"[DIR] {name}")
                .ToList();

            // Get shader files only
            var files = Directory.GetFiles(directoryPath)
                .Select(System.IO.Path.GetFileName)
                .Where(name => !string.IsNullOrEmpty(name))
                .Select(name => name!)
                .Where(name => string.Equals(System.IO.Path.GetExtension(name), ".glsl", StringComparison.OrdinalIgnoreCase))
                .OrderBy(name => name)
                .ToList();

            // Store full list
            _fullDirectoryItems.AddRange(directories);
            _fullDirectoryItems.AddRange(files);

            // Add all items to list box
            foreach (var item in _fullDirectoryItems)
            {
                directoryListBox.Items.Add(item);
            }

            LogMessage($"Loaded {directories.Count} directories and {files.Count} files");
        }
        catch (Exception ex)
        {
            LogMessage($"Error loading directory contents: {ex.Message}");
        }
    }
    
    /// <summary>
    /// Filters the directory list based on search text (case-insensitive)
    /// </summary>
    private void FilterDirectoryList(ListBox directoryListBox, string filterText)
    {
        try
        {
            directoryListBox.Items.Clear();
            
            if (string.IsNullOrWhiteSpace(filterText))
            {
                // Show all items if filter is empty
                foreach (var item in _fullDirectoryItems)
                {
                    directoryListBox.Items.Add(item);
                }
            }
            else
            {
                // Filter items based on search text (case-insensitive)
                var filteredItems = _fullDirectoryItems
                    .Where(item => item.Contains(filterText, StringComparison.OrdinalIgnoreCase))
                    .ToList();
                
                foreach (var item in filteredItems)
                {
                    directoryListBox.Items.Add(item);
                }
            }
        }
        catch (Exception ex)
        {
            LogMessage($"Error filtering directory list: {ex.Message}");
        }
    }


    // ========================================================================
    // CHILD WINDOW MANAGEMENT - Floating tempo display and viewport windows
    // ========================================================================
    
    /// <summary>
    /// Toggles Child Window 2 (full-screen pipeline viewport)
    /// Opens if closed, closes if open
    /// </summary>
    private void ToggleChildWindow2()
    {
        // If window exists and is visible, close it
        if (_childWindow2 != null && _childWindow2.IsVisible)
        {
            _childWindow2.Close();
            _childWindow2 = null;
            LogMessage("Child Window 2 closed");
            return;
        }

        // Create new child window if it doesn't exist or was closed
        if (_childWindow2 == null || !_childWindow2.IsVisible)
        {
            if (Surface == null)
            {
                LogMessage("Cannot open Child Window 2: Shader surface not available");
                return;
            }
            
            _childWindow2 = new ChildWindow2();
            
            // Set the main surface reference for syncing
            _childWindow2.SetMainSurface(Surface);
            
            // Sync current shader if one is loaded
            if (PageContentControl.Content is Page1 controlsPage)
            {
                var mediaPicker = controlsPage.FindControl<Utils_ComboBox>("MediaPicker");
                if (mediaPicker?.SelectedItem is string selectedMedia)
                {
                    // Check if it's a shader
                    var shaderPath = System.IO.Path.Combine(_shaderDir, selectedMedia);
                    if (File.Exists(shaderPath))
                    {
                        _childWindow2.LoadShaderFromFile(shaderPath);
                        _childWindow2.SyncShaderState();
                    }
                }
            }
            
            // Handle window closing to clean up
            _childWindow2.Closed += (_, __) =>
            {
                _childWindow2 = null;
                LogMessage("Child Window 2 closed");
            };
            
            // Show the window (non-modal, floating)
            _childWindow2.Show(this);
            LogMessage("Child Window 2 opened - Full-screen pipeline viewport");
        }
    }
    
    /// <summary>
    /// Syncs Child Window 2's shader state when shader changes occur
    /// Called whenever the main shader or processing nodes are updated
    /// </summary>
    private void SyncChildWindow2()
    {
        if (_childWindow2 != null && _childWindow2.IsVisible)
        {
            _childWindow2.SyncShaderState();
        }
    }
    
    // ========================================================================
    // PROPERTY CHANGE NOTIFICATION - INotifyPropertyChanged implementation
    // ========================================================================
    
    /// <summary>
    /// Raises the PropertyChanged event for data binding updates
    /// </summary>
    protected virtual void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}


