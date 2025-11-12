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
using Melanchall.DryWetMidi.Multimedia;
using Melanchall.DryWetMidi.Core;

namespace Diffracta;

// ============================================================================
// MAIN WINDOW - Primary application window for Diffracta shader application
// ============================================================================
// This class manages the main UI, shader loading, post-processing pipeline,
// MIDI integration, file management, and performance mode.
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
    
    // Performance mode state
    private bool _isPerformanceMode = false;
    
    // Tempo/Clock management
    private MainTempo _globalTempoNumber;
    private bool _isTempoRunning = false;
    
    // Child window management
    private ChildWindow1? _childWindow1;
    private ChildWindow2? _childWindow2;
    
    // ========================================================================
    // CENTRALIZED TIMER MANAGEMENT - Single timer for all periodic updates
    // ========================================================================
    private DispatcherTimer? _globalUpdateTimer;
    private readonly List<Action> _timerCallbacks = new();
    private int _tempoTickCounter = 0; // Count ticks for 1-second tempo updates (10 ticks at 100ms)
    private const int GLOBAL_TIMER_INTERVAL_MS = 100; // 10 updates per second base rate
    private const int TEMPO_TICKS_PER_SECOND = 10; // 1000ms / 100ms = 10 ticks
    
    // MIDI device state
    private InputDevice? _activeMidiDevice;
    private readonly ObservableCollection<string> _midiInEvents = new();
    
    // Post-process slot state management (3 slots for shader effects)
    private readonly bool[] _slotActiveStates = new bool[3];
    private readonly float[] _slotValues = new float[3];
    
    // Directory browsing state
    private string _currentDirectoryPath = string.Empty;
    private readonly List<string> _fullDirectoryItems = new();
    
    // ========================================================================
    // PUBLIC EVENTS
    // ========================================================================
    
    public new event PropertyChangedEventHandler? PropertyChanged;
    
    // ========================================================================
    // CONSTRUCTOR - Initialize window and wire up event handlers
    // ========================================================================
    
    public MainWindow() {
        InitializeComponent();
        
        // Initialize global tempo number
        _globalTempoNumber = new MainTempo();
        
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
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("WARNING: Surface is null in Loaded event");
                }
                
                SetupWatcher();
                UpdateTabContent();
                
                // Wire MIDI UI when available
                var midiInList = this.FindControl<ListBox>("MidiInEventsList");
                if (midiInList != null) midiInList.ItemsSource = _midiInEvents;
                var midiList = this.FindControl<ListBox>("MidiDevicesList");
                if (midiList != null) midiList.SelectionChanged += OnMidiDeviceSelected;
                
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
                
                // Initialize with controls page
                SwitchToPage(1);
                
                // Auto-load default shader after page is loaded
                var defaultShader = "001_organic_noise_1.glsl";
                var defaultShaderPath = System.IO.Path.Combine(_shaderDir, defaultShader);
                if (File.Exists(defaultShaderPath) && Surface != null)
                {
                    Surface.LoadFragmentShaderFromFile(defaultShaderPath, out var message);
                    LogMessage($"Auto-loaded default shader: {defaultShader}");
                    
                }
                else if (!File.Exists(defaultShaderPath))
                {
                    LogMessage($"Default shader not found: {defaultShaderPath}");
                }
                
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
                catch { }
            }
        };
        
        Closed += (_, __) => {
            // Clean up all timers on window close
            StopGlobalUpdateTimer();
        };

        // Toolbar button handlers
        PerformanceButton.Click += (_, __) => {
            TogglePerformanceMode();
        };

        LogsButton.Click += (_, __) => {
            ToggleLogPanel();
        };

        // Log panel button handlers
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

        // Page navigation event handlers
        Page1Button.Click += (_, __) => SwitchToPage(1);
        Page2Button.Click += (_, __) => SwitchToPage(2);
        Page3Button.Click += (_, __) => SwitchToPage(3);
        Page4Button.Click += (_, __) => SwitchToPage(4);

        // Child window menu items
        ChildWindow1MenuItem.Click += (_, __) => ToggleChildWindow1();
        ChildWindow2MenuItem.Click += (_, __) => ToggleChildWindow2();

        // Handle Escape key to exit performance mode
        KeyDown += (_, e) => {
            if (e.Key == Avalonia.Input.Key.Escape && _isPerformanceMode)
            {
                ExitPerformanceMode();
            }
        };
    }
    
    // ========================================================================
    // MENUBAR STYLING - Apply custom styling and hover effects
    // ========================================================================
    
    /// <summary>
    /// Wires up MenuBar styling, hover effects, and popup styling similar to Utils_ComboBox
    /// </summary>
    private void WireUpMenuBarStyling()
    {
        Utils_MenuBar.WireUpMenuBarStyling(MenuBar);
    }
    
    // ========================================================================
    // SHADER MANAGEMENT - File operations and shader loading
    // ========================================================================
    
    /// <summary>
    /// Populates the shader picker dropdown with available .glsl files
    /// </summary>
    private void PopulatePicker(Page1? page = null) {
        var items = Directory.EnumerateFiles(_shaderDir, "*.glsl")
            .OrderBy(p => System.IO.Path.GetFileName(p))
            .Select(p => System.IO.Path.GetFileName(p))
            .ToList();

        if (page != null) {
            var shaderPicker = page.FindControl<Utils_ComboBox>("ShaderPicker");
            if (shaderPicker != null) {
                shaderPicker.ItemsSource = items;
                if (items.Count > 0) {
                    shaderPicker.SelectedIndex = 0;
                }
            }
            
            // Populate pad picker with pad names (P01-P32)
            var slotPicker = page.FindControl<Utils_ComboBox>("SlotPicker");
            if (slotPicker != null) {
                var slotNames = Enumerable.Range(1, 32)
                    .Select(i => $"S{i:D2}")
                    .ToList();
                slotPicker.ItemsSource = slotNames;
                if (slotNames.Count > 0) {
                    slotPicker.SelectedIndex = 0;
                }
            }
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
    /// Loads shader files from file picker dialog and imports them to shader directory
    /// </summary>
    public async Task LoadShaderFiles()
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
                    var destinationPath = System.IO.Path.Combine(_shaderDir, fileName);

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
                
                // Refresh the current controls page if it exists
                if (PageContentControl.Content is Page1 controlsPage)
                {
                    PopulatePicker(controlsPage);
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
    // PERFORMANCE MODE - Fullscreen shader rendering mode
    // ========================================================================
    
    /// <summary>
    /// Toggles between normal and performance mode
    /// </summary>
    private void TogglePerformanceMode() {
        if (_isPerformanceMode) {
            ExitPerformanceMode();
        } else {
            EnterPerformanceMode();
        }
    }

    /// <summary>
    /// Enters performance mode: hides all UI, goes fullscreen, spans shader across entire viewport
    /// </summary>
    private void EnterPerformanceMode() {
        _isPerformanceMode = true;
        PerformanceButton.Content = "Exit Performance";
        
        // Hide all UI panels but keep the shader surface visible
        MenuBar.IsVisible = false;
        LeftSidebar.IsVisible = false;
        TopToolbar.IsVisible = false;
        ControlsPanel.IsVisible = false;
        VerticalSplitter.IsVisible = false;
        HorizontalSplitter.IsVisible = false;
        
        // Make TopRightPanel span the entire viewport and hide the tabbed panel
        TopRightPanel.SetValue(Grid.RowProperty, 0);
        TopRightPanel.SetValue(Grid.ColumnProperty, 0);
        TopRightPanel.SetValue(Grid.RowSpanProperty, 4); // Spans all 4 rows
        TopRightPanel.SetValue(Grid.ColumnSpanProperty, 3);
        
        // Hide the tabbed panel part (row 2) and splitter (row 1), show only shader surface (row 0)
        // We'll do this by making the shader surface row take all space
        var topRightGrid = TopRightPanel as Grid;
        if (topRightGrid != null && topRightGrid.RowDefinitions.Count >= 3)
        {
            topRightGrid.RowDefinitions[0].Height = new GridLength(1, GridUnitType.Star);
            topRightGrid.RowDefinitions[1].Height = new GridLength(0);
            topRightGrid.RowDefinitions[2].Height = new GridLength(0);
        }
        
        // Make the shader surface fill the available space
        // Surface is inside: innerBorder -> outerBorder -> Grid
        var innerBorder = Surface.Parent as Border;
        if (innerBorder != null)
        {
            innerBorder.HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch;
            innerBorder.VerticalAlignment = Avalonia.Layout.VerticalAlignment.Stretch;
            innerBorder.Width = double.NaN;
            innerBorder.Height = double.NaN;
            
            var outerBorder = innerBorder.Parent as Border;
            if (outerBorder != null)
            {
                outerBorder.HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch;
                outerBorder.VerticalAlignment = Avalonia.Layout.VerticalAlignment.Stretch;
                outerBorder.Padding = new Thickness(0);
                outerBorder.Margin = new Thickness(0);
            }
        }
        Surface.Width = double.NaN;
        Surface.Height = double.NaN;
        Surface.HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch;
        Surface.VerticalAlignment = Avalonia.Layout.VerticalAlignment.Stretch;
        
        // Go fullscreen for Performance mode to use full viewport
        WindowState = WindowState.FullScreen;
        
        // Hide mouse cursor in performance mode
        Cursor = Avalonia.Input.Cursor.Parse("None");
        
        LogMessage("Entered performance mode - Full viewport shader, Press Escape to exit");
        UpdateTabContent();
    }

    /// <summary>
    /// Exits performance mode: restores UI panels and normal window layout
    /// </summary>
    private void ExitPerformanceMode() {
        _isPerformanceMode = false;
        PerformanceButton.Content = "Performance";
        
        // Return to windowed mode
        WindowState = WindowState.Normal;
        
        // Restore mouse cursor
        Cursor = Avalonia.Input.Cursor.Parse("Arrow");
        
        // Restore TopRightPanel to normal position
        TopRightPanel.SetValue(Grid.RowProperty, 2);
        TopRightPanel.SetValue(Grid.ColumnProperty, 2);
        TopRightPanel.SetValue(Grid.RowSpanProperty, 2);
        TopRightPanel.SetValue(Grid.ColumnSpanProperty, 1);
        
        // Restore the grid row definitions
        var topRightGrid = TopRightPanel as Grid;
        if (topRightGrid != null && topRightGrid.RowDefinitions.Count >= 3)
        {
            topRightGrid.RowDefinitions[0].Height = new GridLength(1, GridUnitType.Star);
            topRightGrid.RowDefinitions[1].Height = new GridLength(3);
            topRightGrid.RowDefinitions[2].Height = new GridLength(3, GridUnitType.Star);
        }
        
        // Restore shader surface to original size and position
        // Surface is inside: innerBorder -> outerBorder -> Grid
        var innerBorder = Surface.Parent as Border;
        if (innerBorder != null)
        {
            innerBorder.HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left;
            innerBorder.VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top;
            innerBorder.Width = 202;
            innerBorder.Height = 114;
            
            var outerBorder = innerBorder.Parent as Border;
            if (outerBorder != null)
            {
                outerBorder.HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch;
                outerBorder.VerticalAlignment = Avalonia.Layout.VerticalAlignment.Stretch;
                outerBorder.Padding = new Thickness(6);
                outerBorder.Margin = new Thickness(0, 0, 0, 2);
            }
        }
        Surface.Width = 200;
        Surface.Height = 112;
        Surface.HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch;
        Surface.VerticalAlignment = Avalonia.Layout.VerticalAlignment.Stretch;
        
        // Show all panels again
        MenuBar.IsVisible = true;
        LeftSidebar.IsVisible = true;
        TopToolbar.IsVisible = true;
        ControlsPanel.IsVisible = true;
        VerticalSplitter.IsVisible = true;
        HorizontalSplitter.IsVisible = true;
        
        LogMessage("Exited performance mode");
        UpdateTabContent();
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
    
    // ========================================================================
    // TEMPO/CLOCK MANAGEMENT - Global tempo tracking and display
    // ========================================================================
    
    /// <summary>
    /// Handles touchpad button click (placeholder for future functionality)
    /// </summary>
    private void OnTouchpadClicked(object? sender, RoutedEventArgs e)
    {
        LogMessage("Touchpad button clicked!");
        Console.WriteLine("Touchpad button clicked!");
    }

    /// <summary>
    /// Handles tempo button press - starts or stops the global tempo clock
    /// </summary>
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

    /// <summary>
    /// Handles reset button click - stops tempo and resets counter
    /// </summary>
    private void OnResetButtonClicked(object? sender, RoutedEventArgs e)
    {
        StopTempo();
        _globalTempoNumber.Reset();
        LogMessage("Tempo reset");
    }

    /// <summary>
    /// Starts the tempo - uses global timer, no separate timer needed
    /// </summary>
    private void StartTempo()
    {
        _isTempoRunning = true;
        _tempoTickCounter = 0; // Reset counter
        
        // Ensure global timer is running
        if (_globalUpdateTimer == null)
        {
            StartGlobalUpdateTimer();
        }
        
        // Notify UI of button state changes
        OnPropertyChanged(nameof(TempoButtonText));
        OnPropertyChanged(nameof(TempoButtonBackground));
        
        LogMessage("Tempo started");
    }

    /// <summary>
    /// Stops the tempo - no cleanup needed, just set flag
    /// </summary>
    private void StopTempo()
    {
        _isTempoRunning = false;
        
        // Notify UI of button state changes
        OnPropertyChanged(nameof(TempoButtonText));
        OnPropertyChanged(nameof(TempoButtonBackground));
        
        LogMessage($"Tempo stopped - Total time: {_globalTempoNumber.TimeDisplay}");
    }

    // Properties for data binding
    public MainTempo Tempo => _globalTempoNumber;
    public string TempoButtonText => _isTempoRunning ? "Stop Clock" : "Start Clock";
    public string TempoButtonBackground => _isTempoRunning ? "#ff8c00" : "#d3d3d3";
    
    // ========================================================================
    // UI UPDATES - Tab content and shader nodes visualization
    // ========================================================================
    
    /// <summary>
    /// Updates the content of tabs (Global, MIDI) with current information
    /// </summary>
    private void UpdateTabContent()
    {
        // Update Info tab - get current shader from the controls page
        if (PageContentControl.Content is Page1 controlsPage)
        {
            var shaderPicker = controlsPage.FindControl<Utils_ComboBox>("ShaderPicker");
            var shaderInfoText = this.FindControl<TextBlock>("ShaderInfoText");
            
            if (shaderPicker?.SelectedItem is string selectedShader && shaderInfoText != null)
            {
                var fullPath = System.IO.Path.Combine(_shaderDir, selectedShader);
                var fileInfo = new FileInfo(fullPath);
                shaderInfoText.Text = $"Current: {selectedShader}\nSize: {fileInfo.Length} bytes\nModified: {fileInfo.LastWriteTime:yyyy-MM-dd HH:mm:ss}";
            }
            else if (shaderInfoText != null)
            {
                shaderInfoText.Text = "No shader loaded";
            }
        }

        // Populate MIDI devices list in Global tab
        var midiList = this.FindControl<ListBox>("MidiDevicesList");
        if (midiList != null)
        {
            try
            {
                var names = new List<string>();
                foreach (var device in InputDevice.GetAll())
                {
                    try { names.Add(device.Name); }
                    finally { device.Dispose(); } // Ensure disposal even on exception
                }

                if (names.Count == 0)
                {
                    midiList.ItemsSource = new[] { "None" };
                }
                else
                {
                    midiList.ItemsSource = names.OrderBy(n => n).ToList();
                    // auto-select first if none selected
                    if (midiList.SelectedIndex < 0)
                        midiList.SelectedIndex = 0;
                }
            }
            catch (Exception ex)
            {
                midiList.ItemsSource = new[] { $"Error: {ex.Message}" };
            }
        }
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
                
                // Handle tempo updates (every 10 ticks = 1 second)
                if (_isTempoRunning)
                {
                    _tempoTickCounter++;
                    if (_tempoTickCounter >= TEMPO_TICKS_PER_SECOND)
                    {
                        _tempoTickCounter = 0;
                        _globalTempoNumber.Increment();
                        LogMessage($"Global Tempo: {_globalTempoNumber.TimeDisplay} (Seconds: {_globalTempoNumber.Seconds})");
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
        _tempoTickCounter = 0;
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
    
    /// <summary>
    /// Unregisters a callback from the timer
    /// </summary>
    private void UnregisterTimerCallback(Action callback)
    {
        _timerCallbacks.Remove(callback);
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
            case 3:
                var settingsPage = new Page3();
                PageContentControl.Content = settingsPage;
                settingsPage.SetParentWindow(this);
                LogMessage("Switched to Settings page");
                break;
            case 4:
                PageContentControl.Content = new Page4();
                LogMessage("Switched to Help page");
                break;
        }
    }

    /// <summary>
    /// Wires up event handlers for Page1 (Controls page) - shader selection, tempo, slots
    /// </summary>
    private void Page1_WireUp(Page1 page)
    {
        // Find controls and wire up events
        var shaderPicker = page.FindControl<Utils_ComboBox>("ShaderPicker");
        var tempoButton = page.FindControl<Button>("TempoButton");
        var resetButton = page.FindControl<Button>("ResetButton");
        var touchpadButton = page.FindControl<Button>("TouchpadButton");
        
        if (shaderPicker != null)
        {
            shaderPicker.SelectionChanged += (_, __) => {
                if (shaderPicker.SelectedItem is string filename) {
                    var fullPath = System.IO.Path.Combine(_shaderDir, filename);
                    if (File.Exists(fullPath)) {
                        Surface.LoadFragmentShaderFromFile(fullPath, out var message);
                        
                        // Sync Child Window 2 if open - reload shader and sync state
                        if (_childWindow2 != null && _childWindow2.IsVisible)
                        {
                            _childWindow2.LoadShaderFromFile(fullPath);
                            _childWindow2.SyncShaderState();
                        }
                        
                        UpdateTabContent();
                    }
                }
            };
        }
        
        if (tempoButton != null)
        {
            tempoButton.Click += OnTempoButtonPressed;
        }
        
        if (resetButton != null)
        {
            resetButton.Click += OnResetButtonClicked;
        }
        
        if (touchpadButton != null)
        {
            touchpadButton.Click += OnTouchpadClicked;
        }
        
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
    }
    
    /// <summary>
    /// Wires up event handlers for Page2 (Tools page) - directory browsing
    /// </summary>
    private void WireUpToolsPage(Page2 page)
    {
        var browseButton = page.FindControl<Button>("BrowseButton");
        var upButton = page.FindControl<Button>("UpButton");
        var openButton = page.FindControl<Button>("OpenButton");
        var directoryListBox = page.FindControl<ListBox>("DirectoryListBox");
        var directoryPathTextBox = page.FindControl<TextBox>("DirectoryPathTextBox");
        
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

            // Get files
            var files = Directory.GetFiles(directoryPath)
                .Select(System.IO.Path.GetFileName)
                .Where(name => !string.IsNullOrEmpty(name))
                .Select(name => name!)
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
    // MIDI DEVICE MANAGEMENT - MIDI input device handling
    // ========================================================================
    
    /// <summary>
    /// Handles MIDI device selection from the dropdown list
    /// </summary>
    private void OnMidiDeviceSelected(object? sender, SelectionChangedEventArgs e)
    {
        var list = sender as ListBox;
        var name = list?.SelectedItem as string;
        if (string.IsNullOrWhiteSpace(name) || name == "None") return;
        TryOpenMidiDevice(name);
    }

    /// <summary>
    /// Attempts to open a MIDI input device by name
    /// Safely disposes previous device before opening new one
    /// </summary>
    private void TryOpenMidiDevice(string name)
    {
        try
        {
            // Close previous device safely
            if (_activeMidiDevice != null)
            {
                _activeMidiDevice.EventReceived -= OnMidiEventReceived;
                if (_activeMidiDevice.IsListeningForEvents) _activeMidiDevice.StopEventsListening();
                _activeMidiDevice.Dispose();
                _activeMidiDevice = null;
            }

            var dev = InputDevice.GetByName(name);
            if (dev == null)
            {
                _midiInEvents.Clear();
                _midiInEvents.Add($"Device not found: {name}");
                return;
            }

            _midiInEvents.Clear();
            _midiInEvents.Add($"Opened: {name}");
            dev.EventReceived += OnMidiEventReceived;
            dev.StartEventsListening();
            _activeMidiDevice = dev;
        }
        catch (Exception ex)
        {
            _midiInEvents.Add($"Error opening device: {ex.Message}");
        }
    }

    /// <summary>
    /// Handles MIDI events received from the active input device
    /// Thread-safe: Uses Dispatcher to update UI from MIDI thread
    /// Filters out noisy real-time messages
    /// </summary>
    private void OnMidiEventReceived(object? sender, MidiEventReceivedEventArgs e)
    {
        try
        {
            // Filter noisy real-time messages
            if (e.Event is SystemRealTimeEvent)
                return;

            string msg = FormatMidiEvent(e.Event);
            
            // Add to UI list, fail silently if too fast
            Dispatcher.UIThread.Post(() =>
            {
                try
                {
                    _midiInEvents.Add(msg);
                    
                    // Keep only last 50 messages to prevent memory issues
                    while (_midiInEvents.Count > 50)
                        _midiInEvents.RemoveAt(0);
                    
                    // Auto-scroll to bottom
                    var midiInList = this.FindControl<ListBox>("MidiInEventsList");
                    if (midiInList != null && _midiInEvents.Count > 0)
                    {
                        midiInList.SelectedIndex = _midiInEvents.Count - 1;
                        if (midiInList.SelectedItem != null)
                            midiInList.ScrollIntoView(midiInList.SelectedItem);
                    }
                }
                catch
                {
                    // If UI update fails, just skip this message (fail gracefully)
                }
            }, DispatcherPriority.Background);
        }
        catch
        {
            // Swallow exceptions from MIDI thread to prevent crashes
        }
    }

    /// <summary>
    /// Formats a MIDI event as a human-readable string with timestamp
    /// </summary>
    private static string FormatMidiEvent(Melanchall.DryWetMidi.Core.MidiEvent ev)
    {
        string ts = DateTime.Now.ToString("HH:mm:ss.fff");
        if (ev is NoteOnEvent no)
        {
            return $"[{ts}] NoteOn ch {(int)no.Channel + 1} note {no.NoteNumber} vel {no.Velocity}";
        }
        if (ev is NoteOffEvent nf)
        {
            return $"[{ts}] NoteOff ch {(int)nf.Channel + 1} note {nf.NoteNumber} vel {nf.Velocity}";
        }
        if (ev is ControlChangeEvent cc)
        {
            return $"[{ts}] CC ch {(int)cc.Channel + 1} ctrl {(int)cc.ControlNumber} val {cc.ControlValue}";
        }
        if (ev is PitchBendEvent pb)
        {
            return $"[{ts}] PitchBend ch {(int)pb.Channel + 1} val {pb.PitchValue}";
        }
        if (ev is ProgramChangeEvent pc)
        {
            return $"[{ts}] Program ch {(int)pc.Channel + 1} prog {(int)pc.ProgramNumber}";
        }
        if (ev is ChannelAftertouchEvent ca)
        {
            return $"[{ts}] Aftertouch ch {(int)ca.Channel + 1} val {ca.AftertouchValue}";
        }
        // Fallback for unknown event types
        return $"[{ts}] {ev.GetType().Name}";
    }
    
    // ========================================================================
    // CHILD WINDOW MANAGEMENT - Floating tempo display and viewport windows
    // ========================================================================
    
    /// <summary>
    /// Toggles Child Window 1 (tempo display window)
    /// Opens if closed, closes if open
    /// </summary>
    private void ToggleChildWindow1()
    {
        // If window exists and is visible, close it
        if (_childWindow1 != null && _childWindow1.IsVisible)
        {
            _childWindow1.Close();
            _childWindow1 = null;
            LogMessage("Child Window 1 closed");
            return;
        }

        // Create new child window if it doesn't exist or was closed
        if (_childWindow1 == null || !_childWindow1.IsVisible)
        {
            _childWindow1 = new ChildWindow1();
            
            // Share the tempo object for real-time data binding
            _childWindow1.SetSharedTempo(_globalTempoNumber);
            
            // Handle window closing to clean up
            _childWindow1.Closed += (_, __) =>
            {
                _childWindow1 = null;
                LogMessage("Child Window 1 closed");
            };
            
            // Show the window (non-modal, floating)
            _childWindow1.Show(this);
            LogMessage("Child Window 1 opened");
        }
    }
    
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
                var shaderPicker = controlsPage.FindControl<Utils_ComboBox>("ShaderPicker");
                if (shaderPicker?.SelectedItem is string selectedShader)
                {
                    var fullPath = System.IO.Path.Combine(_shaderDir, selectedShader);
                    if (File.Exists(fullPath))
                    {
                        _childWindow2.LoadShaderFromFile(fullPath);
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


