using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.VisualTree;
using System.Collections;
using System.Linq;

namespace Diffracta;

/// <summary>
/// Custom styled ComboBox UserControl for shader selection
/// </summary>
public partial class Utils_ComboBox : UserControl
{
    private Border? _comboBoxBorder;
    
    public Utils_ComboBox()
    {
        InitializeComponent();
        
        // Hook into dropdown opened event to style the popup
        ComboBoxControl.DropDownOpened += OnDropDownOpened;
        
        // Hook into loaded event to wire up hover effects
        ComboBoxControl.Loaded += OnComboBoxLoaded;
    }
    
    private void OnComboBoxLoaded(object? sender, RoutedEventArgs e)
    {
        // Find the Border element in the ComboBox template
        _comboBoxBorder = ComboBoxControl.GetVisualDescendants()
            .OfType<Border>()
            .FirstOrDefault(b => b.Name != "PopupBorder");
        
        // Initialize border brush to prevent default theme overrides
        if (_comboBoxBorder != null)
        {
            _comboBoxBorder.BorderBrush = new SolidColorBrush(Color.Parse("#555555"));
            _comboBoxBorder.PointerEntered += OnComboBoxPointerEntered;
            _comboBoxBorder.PointerExited += OnComboBoxPointerExited;
        }
        
        ComboBoxControl.BorderBrush = new SolidColorBrush(Color.Parse("#555555"));
        
        // Wire up pointer events for hover effect
        ComboBoxControl.PointerEntered += OnComboBoxPointerEntered;
        ComboBoxControl.PointerExited += OnComboBoxPointerExited;
    }
    
    private void OnComboBoxPointerEntered(object? sender, PointerEventArgs e)
    {
        // Set hover background color and black border
        if (_comboBoxBorder != null)
        {
            _comboBoxBorder.Background = new SolidColorBrush(Color.Parse("#3d3d3d"));
            _comboBoxBorder.BorderBrush = new SolidColorBrush(Colors.Black);
        }
        
        ComboBoxControl.Background = new SolidColorBrush(Color.Parse("#3d3d3d"));
        ComboBoxControl.BorderBrush = new SolidColorBrush(Colors.Black);
    }
    
    private void OnComboBoxPointerExited(object? sender, PointerEventArgs e)
    {
        // Restore default background color and border
        if (_comboBoxBorder != null)
        {
            _comboBoxBorder.Background = new SolidColorBrush(Color.Parse("#2d2d2d"));
            _comboBoxBorder.BorderBrush = new SolidColorBrush(Color.Parse("#555555"));
        }
        
        ComboBoxControl.Background = new SolidColorBrush(Color.Parse("#2d2d2d"));
        ComboBoxControl.BorderBrush = new SolidColorBrush(Color.Parse("#555555"));
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

