using Avalonia.Controls;
using Avalonia.Interactivity;
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
                UpdateMediaItemsList(mediaListBox.SelectedItem as string);
            };
        }
        
        // Wire up Convert button
        var convertButton = this.FindControl<Button>("ConvertButton");
        if (convertButton != null && parent != null)
        {
            convertButton.Click += (_, __) =>
            {
                parent.LogMessage("Here convert the directory from MP4, MPEG, MOV, AVI");
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

