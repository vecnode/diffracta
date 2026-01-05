using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.VisualTree;
using System;
using System.Linq;
using System.Collections.Generic;

namespace Diffracta;

public partial class Page2 : UserControl
{
    private MainWindow? _parentWindow;
    private readonly HashSet<string> _convertedDirectories = new();
    
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
            
            // Refresh converted directories background after items are loaded
            mediaListBox.LayoutUpdated += (_, __) =>
            {
                RefreshConvertedDirectoriesBackground(mediaListBox);
            };
            
            // Wire up selection changed to show files from selected directory
            mediaListBox.SelectionChanged += (_, __) =>
            {
                var selectedDir = mediaListBox.SelectedItem as string;
                UpdateMediaItemsList(selectedDir);
                
                // Reapply orange background if this directory was converted
                if (selectedDir != null && _convertedDirectories.Contains(selectedDir))
                {
                    UpdateListBoxItemBackground(mediaListBox, selectedDir, "#ff8c00");
                }
            };
        }
        
        // Wire up Convert button
        var convertButton = this.FindControl<Button>("ConvertButton");
        if (convertButton != null && parent != null)
        {
            convertButton.Click += (_, __) =>
            {
                HandleConvertButtonClick(parent, mediaListBox);
            };
        }
    }
    
    /// <summary>
    /// Handles the Convert button click - scans directory for videos and adds to library
    /// </summary>
    private void HandleConvertButtonClick(MainWindow parent, ListBox? mediaListBox)
    {
        if (mediaListBox == null)
        {
            parent.LogMessage("Media directories list box not found");
            return;
        }
        
        var selectedDirectory = mediaListBox.SelectedItem as string;
        
        if (string.IsNullOrWhiteSpace(selectedDirectory))
        {
            parent.LogMessage("Please select a directory from the list");
            return;
        }
        
        if (!System.IO.Directory.Exists(selectedDirectory))
        {
            parent.LogMessage($"Directory does not exist: {selectedDirectory}");
            return;
        }
        
        // Log start of conversion
        parent.LogMessage($"=== Converting directory: {selectedDirectory} ===");
        
        // Scan directory for video files
        var foundVideos = parent.VideoLibrary.ScanDirectory(selectedDirectory, parent.LogMessage);
        
        // Mark directory as converted
        _convertedDirectories.Add(selectedDirectory);
        
        // Change the selected row background to orange
        UpdateListBoxItemBackground(mediaListBox, selectedDirectory, "#ff8c00");
        
        // Log summary
        if (foundVideos.Count > 0)
        {
            var totalFrames = foundVideos.Sum(v => v.FrameCount);
            parent.LogMessage($"Conversion complete: {foundVideos.Count} video(s) added, {totalFrames} total frames");
        }
        else
        {
            parent.LogMessage("No video files found in directory");
        }
    }
    
    /// <summary>
    /// Updates the background color of a ListBoxItem for a specific directory
    /// </summary>
    private void UpdateListBoxItemBackground(ListBox listBox, string directoryPath, string colorHex)
    {
        try
        {
            // Find the container for the selected item
            var container = listBox.ContainerFromItem(directoryPath) as ListBoxItem;
            if (container != null)
            {
                var brush = SolidColorBrush.Parse(colorHex);
                container.Background = brush;
                
                // Also update the ContentPresenter and Border in the template
                var contentPresenter = container.GetVisualDescendants()
                    .OfType<ContentPresenter>()
                    .FirstOrDefault();
                if (contentPresenter != null)
                {
                    contentPresenter.Background = brush;
                }
                
                var border = container.GetVisualDescendants()
                    .OfType<Avalonia.Controls.Border>()
                    .FirstOrDefault();
                if (border != null)
                {
                    border.Background = brush;
                    border.BorderBrush = brush;
                }
            }
        }
        catch (Exception ex)
        {
            // Silently fail if we can't update the background
            System.Diagnostics.Debug.WriteLine($"Error updating ListBoxItem background: {ex.Message}");
        }
    }
    
    /// <summary>
    /// Updates the background colors for all converted directories in the list
    /// </summary>
    private void RefreshConvertedDirectoriesBackground(ListBox listBox)
    {
        if (listBox == null) return;
        
        foreach (var directory in _convertedDirectories)
        {
            UpdateListBoxItemBackground(listBox, directory, "#ff8c00");
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

