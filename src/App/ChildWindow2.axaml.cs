using Avalonia.Controls;
using Avalonia.Interactivity;
using System.ComponentModel;
using Diffracta.Graphics;

namespace Diffracta;

public partial class ChildWindow2 : Window, INotifyPropertyChanged
{
    private ShaderSurface? _mainSurface;
    private ShaderSurface? _childSurface;

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
        }
    }

    /// <summary>
    /// Syncs the child surface's shader and processing node state with the main surface
    /// </summary>
    public void SyncShaderState()
    {
        if (_mainSurface == null || _childSurface == null) return;

        try
        {
            // Sync processing node states
            for (int i = 0; i < 6; i++)
            {
                _childSurface.SetSlotActive(i, _mainSurface.GetSlotActive(i));
                _childSurface.SetSlotValue(i, _mainSurface.GetSlotValue(i));
            }

            // Note: Shader loading is handled automatically when the main surface loads a shader
            // We'll need to listen for shader changes in MainWindow and reload here
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

