using Avalonia.Controls;
using Avalonia;

namespace Diffracta;

public partial class Page1 : UserControl
{
    private bool _isSplit = false;

    public Page1()
    {
        InitializeComponent();
    }

    private void OnSplitVerticalClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (!_isSplit)
        {
            // Split the grid: make row 1 take 50% and add row 2
            var row1 = MainGrid.RowDefinitions[1];
            var row2 = MainGrid.RowDefinitions[2];
            
            // Set row 1 to 50% height
            row1.Height = new GridLength(0.5, GridUnitType.Star);
            
            // Show and set row 2 to 50% height
            row2.Height = new GridLength(0.5, GridUnitType.Star);
            SplitGrid.IsVisible = true;
            
            _isSplit = true;
            SplitVerticalButton.Content = "Unsplit";
        }
        else
        {
            // Unsplit: restore original layout
            var row1 = MainGrid.RowDefinitions[1];
            var row2 = MainGrid.RowDefinitions[2];
            
            // Restore row 1 to full height
            row1.Height = new GridLength(1, GridUnitType.Star);
            
            // Hide row 2
            row2.Height = new GridLength(0);
            SplitGrid.IsVisible = false;
            
            _isSplit = false;
            SplitVerticalButton.Content = "Split Vertical";
        }
    }
}

