using Avalonia.Controls;
using Avalonia.Interactivity;
using System;

namespace Diffracta;

public partial class Page2 : UserControl
{
    private MainWindow? _parentWindow;
    
    public Page2()
    {
        InitializeComponent();
    }
    
    public void SetParentWindow(MainWindow parent)
    {
        _parentWindow = parent;
        
        // Wire up the DirectoryBox to access the parent window
        var directoryBox = this.FindControl<Utils_DirectoryBox>("DirectoryBox");
        if (directoryBox != null)
        {
            directoryBox.SetParentWindow(parent);
        }
        
        
        // Set up the media directories list visualizer
        var mediaListBox = this.FindControl<ListBox>("MediaDirectoriesListBox");
        if (mediaListBox != null && parent != null)
        {
            mediaListBox.ItemsSource = parent.MediaDirectories;
        }
    }
}

