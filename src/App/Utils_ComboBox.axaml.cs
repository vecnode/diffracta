using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.VisualTree;
using System.Collections;
using System.ComponentModel;
using System.Linq;

namespace Diffracta;

/// <summary>
/// Custom styled ComboBox UserControl for shader selection
/// </summary>
public partial class Utils_ComboBox : UserControl
{
    public Utils_ComboBox()
    {
        InitializeComponent();
        
        // Hook into dropdown opened event to style the popup
        ComboBoxControl.DropDownOpened += OnDropDownOpened;
    }
    
    private void OnDropDownOpened(object? sender, EventArgs e)
    {
        // Try to find and style the PopupBorder
        var popupBorder = ComboBoxControl.FindControl<Border>("PopupBorder");
        if (popupBorder != null)
        {
            popupBorder.Background = new SolidColorBrush(Color.Parse("#1c1c1c"));
            // Remove top and bottom borders, keep only left and right
            popupBorder.BorderThickness = new Avalonia.Thickness(1, 0, 1, 0);
            popupBorder.Padding = new Avalonia.Thickness(0);
        }
        
        // Also try to find Popup via visual tree
        var popup = ComboBoxControl.GetVisualDescendants()
            .OfType<Popup>()
            .FirstOrDefault();
            
        if (popup?.Child is Border border)
        {
            border.Background = new SolidColorBrush(Color.Parse("#1c1c1c"));
            // Remove top and bottom borders, keep only left and right
            border.BorderThickness = new Avalonia.Thickness(1, 0, 1, 0);
            border.Padding = new Avalonia.Thickness(0);
        }
    }
    
    /// <summary>
    /// ItemsSource property - binds to the internal ComboBox
    /// </summary>
    public IEnumerable? ItemsSource
    {
        get => ComboBoxControl.ItemsSource;
        set => ComboBoxControl.ItemsSource = value;
    }
    
    /// <summary>
    /// SelectedItem property - binds to the internal ComboBox
    /// </summary>
    public object? SelectedItem
    {
        get => ComboBoxControl.SelectedItem;
        set => ComboBoxControl.SelectedItem = value;
    }
    
    /// <summary>
    /// SelectedIndex property - binds to the internal ComboBox
    /// </summary>
    public int SelectedIndex
    {
        get => ComboBoxControl.SelectedIndex;
        set => ComboBoxControl.SelectedIndex = value;
    }
    
    /// <summary>
    /// Width property - binds to the internal ComboBox
    /// </summary>
    public new double Width
    {
        get => ComboBoxControl.Width;
        set => ComboBoxControl.Width = value;
    }
    
    /// <summary>
    /// SelectionChanged event - forwards from the internal ComboBox
    /// </summary>
    public event EventHandler<SelectionChangedEventArgs>? SelectionChanged
    {
        add => ComboBoxControl.SelectionChanged += value;
        remove => ComboBoxControl.SelectionChanged -= value;
    }
}
