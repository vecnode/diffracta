using Avalonia.Controls;
using System;

namespace Diffracta;

public partial class Page3 : UserControl
{
    private MainWindow? _parentWindow;
    
    public Page3()
    {
        try
        {
            System.Diagnostics.Debug.WriteLine("Page3: Constructor called");
            InitializeComponent();
            System.Diagnostics.Debug.WriteLine("Page3: InitializeComponent completed");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Page3 Constructor error: {ex.Message}\n{ex.StackTrace}");
            Console.WriteLine($"Page3 Constructor error: {ex.Message}");
            throw; // Re-throw to see the actual error
        }
    }
    
    public void SetParentWindow(MainWindow parent)
    {
        try
        {
            _parentWindow = parent;
            WireUpControls();
            System.Diagnostics.Debug.WriteLine("Page3: SetParentWindow completed");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Page3 SetParentWindow error: {ex.Message}\n{ex.StackTrace}");
            Console.WriteLine($"Page3 SetParentWindow error: {ex.Message}");
        }
    }
    
    private void WireUpControls()
    {
        try
        {
            // Check if TimelineEditor exists
            var timelineEditor = this.FindControl<TimelineControl.Utils_TimelineEditor>("TimelineEditorControl");
            System.Diagnostics.Debug.WriteLine($"Page3: TimelineEditor found = {timelineEditor != null}");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Page3 WireUpControls error: {ex.Message}\n{ex.StackTrace}");
            Console.WriteLine($"Page3 WireUpControls error: {ex.Message}");
        }
    }
}


