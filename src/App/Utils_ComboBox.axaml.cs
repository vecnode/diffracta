using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
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
    private Border? _comboBoxBorder;
    private Panel? _comboBoxContentPresenter;
    
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
        // Find the Border and ContentPresenter elements in the ComboBox template
        _comboBoxBorder = ComboBoxControl.GetVisualDescendants()
            .OfType<Border>()
            .FirstOrDefault(b => b.Name != "PopupBorder");
            
        // Find ContentPresenter by looking for Panel controls (ContentPresenter inherits from Panel)
        _comboBoxContentPresenter = ComboBoxControl.GetVisualDescendants()
            .OfType<Panel>()
            .FirstOrDefault(c => c.GetType().Name == "ContentPresenter");
        
        // Wire up pointer events for hover effect
        if (_comboBoxBorder != null)
        {
            _comboBoxBorder.PointerEntered += OnComboBoxPointerEntered;
            _comboBoxBorder.PointerExited += OnComboBoxPointerExited;
        }
        
        if (_comboBoxContentPresenter != null)
        {
            _comboBoxContentPresenter.PointerEntered += OnComboBoxPointerEntered;
            _comboBoxContentPresenter.PointerExited += OnComboBoxPointerExited;
        }
        
        // Also wire up on the ComboBox itself as fallback
        ComboBoxControl.PointerEntered += OnComboBoxPointerEntered;
        ComboBoxControl.PointerExited += OnComboBoxPointerExited;
    }
    
    private void OnComboBoxPointerEntered(object? sender, PointerEventArgs e)
    {
        // Set hover background color
        if (_comboBoxBorder != null)
        {
            _comboBoxBorder.Background = new SolidColorBrush(Color.Parse("#3d3d3d"));
        }
        
        if (_comboBoxContentPresenter != null)
        {
            _comboBoxContentPresenter.Background = new SolidColorBrush(Color.Parse("#3d3d3d"));
        }
        
        ComboBoxControl.Background = new SolidColorBrush(Color.Parse("#3d3d3d"));
    }
    
    private void OnComboBoxPointerExited(object? sender, PointerEventArgs e)
    {
        // Restore default background color
        if (_comboBoxBorder != null)
        {
            _comboBoxBorder.Background = new SolidColorBrush(Color.Parse("#2d2d2d"));
        }
        
        if (_comboBoxContentPresenter != null)
        {
            _comboBoxContentPresenter.Background = new SolidColorBrush(Color.Parse("#1c1c1c"));
        }
        
        ComboBoxControl.Background = new SolidColorBrush(Color.Parse("#2d2d2d"));
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

