using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using System.ComponentModel;
using Diffracta.Graphics;

namespace Diffracta;

public partial class ChildWindow2 : Window, INotifyPropertyChanged
{
    private ShaderSurface? _mainSurface;
    private ShaderSurface? _childSurface;
    private DispatcherTimer? _syncTimer;

    /// <summary>
    /// PropertyChanged event for INotifyPropertyChanged implementation.
    /// Uses 'new' keyword to explicitly hide the inherited AvaloniaObject.PropertyChanged event.
    /// </summary>
    public new event PropertyChangedEventHandler? PropertyChanged;

    public ChildWindow2()
    {
        InitializeComponent();
        DataContext = this;
    }

    /// <summary>
    /// Sets the main shader surface to sync with for real-time rendering
    /// </summary>
    public void SetMainSurface(ShaderSurface mainSurface)
    {
        _mainSurface = mainSurface;
        
        // Get the child surface from XAML
        _childSurface = this.FindControl<ShaderSurface>("ChildSurface");
        
        if (_childSurface != null && _mainSurface != null)
        {
            // Sync initial state
            SyncShaderState();
            
            // Set up logging callback
            _childSurface.SetLogCallback((msg) => {
                // Optionally forward logs to main window
            });
            
            // Start continuous syncing timer to keep child window in sync with main window
            StartSyncTimer();
        }
    }
    
    /// <summary>
    /// Starts a timer to continuously sync the child surface with the main surface
    /// Uses high-frequency sync (~60fps) to minimize delay and ensure real-time synchronization
    /// </summary>
    private void StartSyncTimer()
    {
        if (_syncTimer != null) return; // Already running
        
        _syncTimer = new DispatcherTimer(DispatcherPriority.Render)
        {
            Interval = TimeSpan.FromMilliseconds(16) // Sync ~60 times per second (matches typical render rate)
        };
        
        _syncTimer.Tick += (_, __) => {
            try
            {
                SyncShaderState();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in sync timer: {ex.Message}");
            }
        };
        
        _syncTimer.Start();
    }
    
    /// <summary>
    /// Stops the sync timer
    /// </summary>
    private void StopSyncTimer()
    {
        if (_syncTimer != null)
        {
            _syncTimer.Stop();
            _syncTimer = null;
        }
    }

    /// <summary>
    /// Syncs the child surface's shader and processing node state with the main surface
    /// Optimized to only update values that have changed to reduce unnecessary work
    /// </summary>
    public void SyncShaderState()
    {
        if (_mainSurface == null || _childSurface == null) return;

        try
        {
            // Sync processing node states - only update if values differ to avoid unnecessary work
            for (int i = 0; i < 6; i++)
            {
                bool mainActive = _mainSurface.GetSlotActive(i);
                float mainValue = _mainSurface.GetSlotValue(i);
                
                // Only update if values differ (reduces redundant state changes)
                if (_childSurface.GetSlotActive(i) != mainActive)
                {
                    _childSurface.SetSlotActive(i, mainActive);
                }
                
                if (Math.Abs(_childSurface.GetSlotValue(i) - mainValue) > 0.0001f)
                {
                    _childSurface.SetSlotValue(i, mainValue);
                }
            }

            // Note: Shader loading is handled automatically when the main surface loads a shader
            // MainWindow calls LoadShaderFromFile when shader changes occur
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error syncing shader state: {ex.Message}");
        }
    }

    /// <summary>
    /// Loads the same shader file that the main surface is using
    /// </summary>
    public void LoadShaderFromFile(string shaderPath)
    {
        if (_childSurface != null)
        {
            _childSurface.LoadFragmentShaderFromFile(shaderPath, out var message);
        }
    }

    private void CloseButton_Click(object? sender, RoutedEventArgs e)
    {
        Close();
    }

    protected override void OnClosed(EventArgs e)
    {
        // Stop sync timer
        StopSyncTimer();
        
        // Clean up
        _mainSurface = null;
        _childSurface = null;
        base.OnClosed(e);
    }

    protected virtual void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

