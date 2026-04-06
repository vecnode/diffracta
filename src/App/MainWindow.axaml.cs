using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using System.IO;
using System.Linq;
using System.Text;
using System.ComponentModel;

namespace AvaloniaGlslPipeline;

// Manages the main window, shader selection, post-processing pipeline,
// file watching, and logging.
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
    
    // Media and project state
    private readonly bool[] _slotActiveStates = new bool[3];
    private readonly float[] _slotValues = new float[3];

    // Global timer state
    private const int GLOBAL_TIMER_INTERVAL_MS = 16;
    private DispatcherTimer? _globalUpdateTimer;

    // INotifyPropertyChanged event
    public new event PropertyChangedEventHandler? PropertyChanged;
    
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
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("WARNING: Surface is null in Loaded event");
                }
                
                SetupWatcher();
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
                
                WireUpMainWindowControls();
                
                // Initialize with controls page
                var controlsPage = new Page1();
                PageContentControl.Content = controlsPage;
                Page1_WireUp(controlsPage);
                PopulatePicker(controlsPage);
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
            
        }
    }

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
            
        }
    }

    public float GetSlotValue(int slot) => _slotValues[slot];
    
    // ========================================================================
    // SHADER SURFACE UPDATE TIMER
    // ========================================================================

    /// <summary>
    /// Starts the centralized global update timer
    /// All periodic updates use this single timer to prevent thread leaks and improve efficiency
    /// </summary>
    private void StartGlobalUpdateTimer()
    {
        if (_globalUpdateTimer != null) return;
        
        _globalUpdateTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(GLOBAL_TIMER_INTERVAL_MS)
        };
        
        _globalUpdateTimer.Tick += (_, __) =>
        {
            try
            {
                this.FindControl<Utils_NodesListBox>("NodesListBox")?.UpdateShaderNodesVisualization();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Timer error: {ex.Message}");
            }
        };
        
        _globalUpdateTimer.Start();
    }
    

    
    // ========================================================================
    // PAGE WIRING
    // ========================================================================

    /// <summary>
    /// Wires up event handlers for Page1 (Controls page) - media selection, tempo, slots
    /// </summary>
    private void Page1_WireUp(Page1 page)
    {
        // Find controls and wire up events
        var mediaPicker = page.FindControl<Utils_ComboBox>("MediaPicker");
        var applyButton = page.FindControl<Button>("ApplyButton");
        var previewSurface = page.FindControl<AvaloniaGlslPipeline.Graphics.ShaderSurface>("Page1MainTexturePreview");
        
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

        var previewSurface = page.FindControl<AvaloniaGlslPipeline.Graphics.ShaderSurface>("Page1MainTexturePreview");
        if (previewSurface != null)
        {
            previewSurface.LoadFragmentShaderFromFile(shaderPath, out _);
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


