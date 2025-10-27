using Avalonia.Controls;

namespace Diffracta;

public partial class SettingsPage : UserControl
{
    private MainWindow? _parentWindow;
    
    public SettingsPage()
    {
        InitializeComponent();
    }
    
    public void SetParentWindow(MainWindow parent)
    {
        _parentWindow = parent;
        WireUpControls();
    }
    
    private void WireUpControls()
    {
        // Wire up Load Shader button
        var loadShaderButton = this.FindControl<Button>("LoadShaderButton");
        if (loadShaderButton != null)
        {
            loadShaderButton.Click += async (_, __) => {
                if (_parentWindow != null)
                {
                    await _parentWindow.LoadShaderFiles();
                }
            };
        }
    }
}

