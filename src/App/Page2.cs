using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Controls.Templates;
using Avalonia.Media;
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
        WireUpControls();
    }
    
    private void WireUpControls()
    {
        // Wire up toggle buttons and restore their state
        var slot1Toggle = this.FindControl<Button>("Slot1Toggle");
        var slot2Toggle = this.FindControl<Button>("Slot2Toggle");
        var slot3Toggle = this.FindControl<Button>("Slot3Toggle");
        
        if (slot1Toggle != null)
        {
            var isActive = _parentWindow?.GetSlotActive(0) ?? false;
            slot1Toggle.Content = isActive ? "ON" : "OFF";
            slot1Toggle.Background = isActive ? 
                Avalonia.Media.SolidColorBrush.Parse("#ff8c00") : Avalonia.Media.SolidColorBrush.Parse("#d3d3d3");
            slot1Toggle.Click += (s, e) => OnSlotToggleClicked(0, s, e);
        }
        
        if (slot2Toggle != null)
        {
            var isActive = _parentWindow?.GetSlotActive(1) ?? false;
            slot2Toggle.Content = isActive ? "ON" : "OFF";
            slot2Toggle.Background = isActive ? 
                Avalonia.Media.SolidColorBrush.Parse("#ff8c00") : Avalonia.Media.SolidColorBrush.Parse("#d3d3d3");
            slot2Toggle.Click += (s, e) => OnSlotToggleClicked(1, s, e);
        }
        
        if (slot3Toggle != null)
        {
            var isActive = _parentWindow?.GetSlotActive(2) ?? false;
            slot3Toggle.Content = isActive ? "ON" : "OFF";
            slot3Toggle.Background = isActive ? 
                Avalonia.Media.SolidColorBrush.Parse("#ff8c00") : Avalonia.Media.SolidColorBrush.Parse("#d3d3d3");
            slot3Toggle.Click += (s, e) => OnSlotToggleClicked(2, s, e);
        }
        
        // Wire up sliders and restore their values
        var slot1Slider = this.FindControl<Slider>("Slot1Slider");
        var slot2Slider = this.FindControl<Slider>("Slot2Slider");
        var slot3Slider = this.FindControl<Slider>("Slot3Slider");
        
        if (slot1Slider != null)
        {
            slot1Slider.Value = _parentWindow?.GetSlotValue(0) ?? 0.5f;
            slot1Slider.ValueChanged += (s, e) => OnSlotValueChanged(0, s, e);
        }
        
        if (slot2Slider != null)
        {
            slot2Slider.Value = _parentWindow?.GetSlotValue(1) ?? 0.5f;
            slot2Slider.ValueChanged += (s, e) => OnSlotValueChanged(1, s, e);
        }
        
        if (slot3Slider != null)
        {
            slot3Slider.Value = _parentWindow?.GetSlotValue(2) ?? 0.5f;
            slot3Slider.ValueChanged += (s, e) => OnSlotValueChanged(2, s, e);
        }
        
        // Restore value text displays
        UpdateValueDisplays();
        
        // Set up DirectoryListBox ItemTemplate with colored [DIR] text
        var directoryListBox = this.FindControl<ListBox>("DirectoryListBox");
        if (directoryListBox != null)
        {
            directoryListBox.ItemTemplate = new FuncDataTemplate<string>((item, _) =>
            {
                if (item != null && item.StartsWith("[DIR]"))
                {
                    // Create a horizontal stack panel with [DIR] in yellow and rest in white
                    var panel = new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal };
                    
                    var dirText = new TextBlock 
                    { 
                        Text = "[DIR]", 
                        Foreground = Brushes.Yellow 
                    };
                    
                    var rest = item.Substring(5).TrimStart();
                    var nameText = new TextBlock 
                    { 
                        Text = string.IsNullOrEmpty(rest) ? "" : " " + rest, 
                        Foreground = Brushes.White 
                    };
                    
                    panel.Children.Add(dirText);
                    panel.Children.Add(nameText);
                    
                    return panel;
                }
                else
                {
                    // Regular file, just white text
                    return new TextBlock 
                    { 
                        Text = item ?? "", 
                        Foreground = Brushes.White 
                    };
                }
            });
        }
    }
    
    private void UpdateValueDisplays()
    {
        if (_parentWindow != null)
        {
            for (int i = 0; i < 3; i++)
            {
                string textBlockName = $"Slot{i + 1}Value";
                var textBlock = this.FindControl<TextBlock>(textBlockName);
                if (textBlock != null)
                {
                    var value = _parentWindow.GetSlotValue(i);
                    textBlock.Text = value.ToString("F2");
                }
            }
        }
    }
    
    private void OnSlotToggleClicked(int slot, object? sender, RoutedEventArgs e)
    {
        if (_parentWindow?.Surface != null)
        {
            bool newState = !_parentWindow.Surface.GetSlotActive(slot);
            _parentWindow.Surface.SetSlotActive(slot, newState);
            _parentWindow.LogMessage($"Slot {slot + 1} shader {(newState ? "activated" : "deactivated")}");
            
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
        if (_parentWindow?.Surface != null && e.NewValue is double value)
        {
            _parentWindow.Surface.SetSlotValue(slot, (float)value);
            _parentWindow.LogMessage($"Slot {slot + 1} value changed to {value:F2}");
            
            // Update the UI text block to show the current value
            string textBlockName = $"Slot{slot + 1}Value";
            var textBlock = this.FindControl<TextBlock>(textBlockName);
            if (textBlock != null)
            {
                textBlock.Text = value.ToString("F2");
            }
        }
    }
}

