using Avalonia.Controls;
using System;
using System.Linq;

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
            
            // Wire up selection changed to show files from selected directory
            mediaListBox.SelectionChanged += (_, __) =>
            {
                var selectedDir = mediaListBox.SelectedItem as string;
                UpdateMediaItemsList(selectedDir);
            };
        }
        
    }
    
    /// <summary>
    /// Updates the Media Items ListBox with files from the selected directory
    /// </summary>
    private void UpdateMediaItemsList(string? selectedDirectory)
    {
        var mediaItemsListBox = this.FindControl<ListBox>("MediaItemsListBox");
        if (mediaItemsListBox == null) return;
        
        if (string.IsNullOrWhiteSpace(selectedDirectory) || !System.IO.Directory.Exists(selectedDirectory))
        {
            mediaItemsListBox.ItemsSource = null;
            return;
        }
        
        try
        {
            var files = System.IO.Directory.GetFiles(selectedDirectory)
                .Select(System.IO.Path.GetFileName)
                .Where(name => !string.IsNullOrEmpty(name))
                .Where(name => string.Equals(System.IO.Path.GetExtension(name), ".glsl", StringComparison.OrdinalIgnoreCase))
                .OrderBy(name => name)
                .ToList();
            
            mediaItemsListBox.ItemsSource = files;
        }
        catch (Exception ex)
        {
            mediaItemsListBox.ItemsSource = new[] { $"Error: {ex.Message}" };
        }
    }
}

