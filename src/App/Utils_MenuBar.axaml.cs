using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.VisualTree;
using Avalonia;
using System.Linq;

namespace Diffracta;

/// <summary>
/// Helper class for MenuBar styling, hover effects, and popup styling similar to Utils_ComboBox
/// </summary>
public static class Utils_MenuBar
{
    /// <summary>
    /// Wires up MenuBar styling, hover effects, and popup styling similar to Utils_ComboBox
    /// </summary>
    public static void WireUpMenuBarStyling(Menu? menuBar)
    {
        if (menuBar == null) return;
        
        // Apply Menu styling programmatically (moved from XAML)
        menuBar.Background = new SolidColorBrush(Color.Parse("#2d2d2d"));
        menuBar.Foreground = new SolidColorBrush(Color.Parse("White"));
        menuBar.BorderBrush = new SolidColorBrush(Color.Parse("#555555"));
        menuBar.BorderThickness = new Avalonia.Thickness(0);
        menuBar.FontSize = 12;
        menuBar.Height = 26;
        
        // Wire up pointer events for hover effect on all MenuItems
        menuBar.Loaded += (_, __) =>
        {
            // Style Menu > Panel
            var menuPanel = menuBar.GetVisualDescendants()
                .OfType<Panel>()
                .FirstOrDefault(p => p.Parent == menuBar);
            if (menuPanel != null)
            {
                menuPanel.Background = new SolidColorBrush(Color.Parse("#2d2d2d"));
            }
            
            WireUpMenuItemEvents(menuBar);
        };
        
        // Also wire up when menu items are added dynamically
        menuBar.AttachedToVisualTree += (_, __) =>
        {
            WireUpMenuItemEvents(menuBar);
        };
    }
    
    private static void WireUpMenuItemEvents(Menu? menuBar)
    {
        if (menuBar == null) return;
        
        var menuItems = menuBar.GetVisualDescendants()
            .OfType<MenuItem>()
            .ToList();
        
        foreach (var menuItem in menuItems)
        {
            // Remove existing handlers to avoid duplicates
            menuItem.PointerEntered -= OnMenuItemPointerEntered;
            menuItem.PointerExited -= OnMenuItemPointerExited;
            
            // Apply MenuItem styling programmatically (moved from XAML)
            ApplyMenuItemStyling(menuItem);
            
            // Wire up hover events to ensure grey background on hover
            menuItem.PointerEntered += OnMenuItemPointerEntered;
            menuItem.PointerExited += OnMenuItemPointerExited;
            
            // Hook into submenu opened event to style the popup
            menuItem.SubmenuOpened -= OnSubmenuOpened;
            menuItem.SubmenuOpened += OnSubmenuOpened;
        }
    }
    
    /// <summary>
    /// Applies styling to a MenuItem programmatically (moved from XAML)
    /// </summary>
    private static void ApplyMenuItemStyling(MenuItem menuItem)
    {
        // MenuItem styling - grey background, white text
        menuItem.Background = new SolidColorBrush(Color.Parse("#2d2d2d"));
        menuItem.Foreground = new SolidColorBrush(Color.Parse("White"));
        menuItem.BorderBrush = new SolidColorBrush(Colors.Transparent);
        menuItem.BorderThickness = new Avalonia.Thickness(0);
        menuItem.FontSize = 12;
        menuItem.Padding = new Avalonia.Thickness(8, 4);
        menuItem.Margin = new Avalonia.Thickness(0);
        
        // MenuItem ContentPresenter styling
        var contentPresenter = menuItem.GetVisualDescendants()
            .OfType<Panel>()
            .FirstOrDefault(c => c.GetType().Name == "ContentPresenter");
        if (contentPresenter != null)
        {
            contentPresenter.Margin = new Avalonia.Thickness(0);
            contentPresenter.Background = new SolidColorBrush(Color.Parse("#2d2d2d"));
        }
    }
    
    private static void OnMenuItemPointerEntered(object? sender, PointerEventArgs e)
    {
        if (sender is MenuItem menuItem)
        {
            // Check if it's a submenu item (inside popup) or top-level menu item
            var isSubmenuItem = menuItem.Parent is Popup;
            
            var hoverColor = isSubmenuItem ? "#2d2d2d" : "#3d3d3d";
            var hoverBrush = new SolidColorBrush(Color.Parse(hoverColor));
            
            // Clear style bindings first, then force set MenuItem background
            menuItem.ClearValue(MenuItem.BackgroundProperty);
            menuItem.SetValue(MenuItem.BackgroundProperty, hoverBrush);
            menuItem.Background = hoverBrush;
            
            // Style ALL visual descendants that could have backgrounds
            var allVisuals = menuItem.GetVisualDescendants().ToList();
            foreach (var visual in allVisuals)
            {
                if (visual is Border border)
                {
                    border.ClearValue(Border.BackgroundProperty);
                    border.SetValue(Border.BackgroundProperty, hoverBrush);
                    border.Background = hoverBrush;
                }
                else if (visual is Panel panel && panel.GetType().Name == "ContentPresenter")
                {
                    panel.ClearValue(Panel.BackgroundProperty);
                    panel.SetValue(Panel.BackgroundProperty, hoverBrush);
                    panel.Background = hoverBrush;
                }
                else if (visual is Button button)
                {
                    // Style any Button elements inside MenuItem
                    button.ClearValue(Button.BackgroundProperty);
                    button.SetValue(Button.BackgroundProperty, hoverBrush);
                    button.Background = hoverBrush;
                }
            }
        }
    }
    
    private static void OnMenuItemPointerExited(object? sender, PointerEventArgs e)
    {
        if (sender is MenuItem menuItem)
        {
            // Restore default background color
            var isSubmenuItem = menuItem.Parent is Popup;
            var defaultColor = isSubmenuItem ? "#1c1c1c" : "#2d2d2d";
            var defaultBrush = new SolidColorBrush(Color.Parse(defaultColor));
            
            // Clear style bindings first, then force set MenuItem background
            menuItem.ClearValue(MenuItem.BackgroundProperty);
            menuItem.SetValue(MenuItem.BackgroundProperty, defaultBrush);
            menuItem.Background = defaultBrush;
            
            // Style ALL visual descendants
            var allVisuals = menuItem.GetVisualDescendants().ToList();
            foreach (var visual in allVisuals)
            {
                if (visual is Border border)
                {
                    border.ClearValue(Border.BackgroundProperty);
                    border.SetValue(Border.BackgroundProperty, defaultBrush);
                    border.Background = defaultBrush;
                }
                else if (visual is Panel panel && panel.GetType().Name == "ContentPresenter")
                {
                    panel.ClearValue(Panel.BackgroundProperty);
                    panel.SetValue(Panel.BackgroundProperty, defaultBrush);
                    panel.Background = defaultBrush;
                }
                else if (visual is Button button)
                {
                    // Restore Button background
                    button.ClearValue(Button.BackgroundProperty);
                    button.SetValue(Button.BackgroundProperty, defaultBrush);
                    button.Background = defaultBrush;
                }
            }
        }
    }
    
    private static void OnSubmenuOpened(object? sender, RoutedEventArgs e)
    {
        if (sender is MenuItem menuItem)
        {
            // Style immediately (like ComboBox approach)
            StyleSubmenuPopup(menuItem);
        }
    }
    
    private static void StyleSubmenuPopup(MenuItem menuItem)
    {
        // Try to find and style the PopupBorder (like ComboBox)
        var popupBorder = menuItem.FindControl<Border>("PopupBorder");
        if (popupBorder != null)
        {
            var darkBrush = new SolidColorBrush(Color.Parse("#1c1c1c"));
            var borderBrush = new SolidColorBrush(Color.Parse("#555555"));
            popupBorder.SetValue(Border.BackgroundProperty, darkBrush);
            popupBorder.SetValue(Border.BorderBrushProperty, borderBrush);
            popupBorder.SetValue(Border.BorderThicknessProperty, new Avalonia.Thickness(1, 0, 1, 0));
            popupBorder.SetValue(Border.CornerRadiusProperty, new Avalonia.CornerRadius(0));
            popupBorder.SetValue(Border.PaddingProperty, new Avalonia.Thickness(0));
            popupBorder.SetValue(Border.MarginProperty, new Avalonia.Thickness(0));
            popupBorder.Background = darkBrush;
            popupBorder.BorderBrush = borderBrush;
            popupBorder.BorderThickness = new Avalonia.Thickness(1, 0, 1, 0);
            popupBorder.CornerRadius = new Avalonia.CornerRadius(0);
            popupBorder.Padding = new Avalonia.Thickness(0);
            popupBorder.Margin = new Avalonia.Thickness(0);
        }
        
        // Try multiple ways to find the Popup - from MenuItem, from parent, or from visual root
        Popup? popup = null;
        
        // Method 1: From MenuItem's descendants (original approach)
        popup = menuItem.GetVisualDescendants().OfType<Popup>().FirstOrDefault();
        
        // Method 2: If not found, try from MenuItem's parent
        if (popup == null && menuItem.Parent is Visual parentVisual)
        {
            popup = parentVisual.GetVisualDescendants().OfType<Popup>().FirstOrDefault();
        }
        
        // Method 3: Try from visual root
        if (popup == null)
        {
            var visualRoot = menuItem.GetVisualRoot();
            if (visualRoot is Visual rootVisual)
            {
                popup = rootVisual.GetVisualDescendants().OfType<Popup>()
                    .FirstOrDefault(p => p.IsVisible && p.Child != null);
            }
        }
        
        // Method 4: Try finding via MenuItem's template parent
        if (popup == null)
        {
            var templatedParent = menuItem.TemplatedParent;
            if (templatedParent is Visual templateVisual)
            {
                popup = templateVisual.GetVisualDescendants().OfType<Popup>().FirstOrDefault();
            }
        }
            
        if (popup == null) return;
        
        // Hook into Popup.Opened to ensure styling is applied after popup is fully rendered
        popup.Opened -= OnPopupOpened;
        popup.Opened += OnPopupOpened;
        
        // Apply styling immediately
        ApplyPopupStyling(popup);
    }
    
    private static void OnPopupOpened(object? sender, EventArgs e)
    {
        if (sender is Popup popup)
        {
            ApplyPopupStyling(popup);
        }
    }
    
    private static void ApplyPopupStyling(Popup popup)
    {
        var darkBrush = new SolidColorBrush(Color.Parse("#1c1c1c"));
        var borderBrush = new SolidColorBrush(Color.Parse("#555555"));
        
        // Hook into LayoutUpdated to catch borders created after initial render
        void OnLayoutUpdated(object? sender, EventArgs e)
        {
            StyleAllBordersInPopup(popup, darkBrush, borderBrush);
        }
        
        if (popup.Child is Control childControl)
        {
            childControl.LayoutUpdated -= OnLayoutUpdated;
            childControl.LayoutUpdated += OnLayoutUpdated;
        }
        
        // Style the popup's direct child - could be Border, Panel, Grid, etc.
        if (popup.Child is Border border)
        {
            // Clear any style bindings first, then set values
            border.ClearValue(Border.BackgroundProperty);
            border.ClearValue(Border.BorderBrushProperty);
            border.ClearValue(Border.BorderThicknessProperty);
            border.ClearValue(Border.CornerRadiusProperty);
            border.ClearValue(Border.PaddingProperty);
            border.ClearValue(Border.MarginProperty);
            
            // Use SetValue to override any styles, then set directly
            border.SetValue(Border.BackgroundProperty, darkBrush);
            border.SetValue(Border.BorderBrushProperty, borderBrush);
            border.SetValue(Border.BorderThicknessProperty, new Avalonia.Thickness(1, 0, 1, 0));
            border.SetValue(Border.CornerRadiusProperty, new Avalonia.CornerRadius(0));
            border.SetValue(Border.PaddingProperty, new Avalonia.Thickness(0));
            border.SetValue(Border.MarginProperty, new Avalonia.Thickness(0));
            border.Background = darkBrush;
            border.BorderBrush = borderBrush;
            border.BorderThickness = new Avalonia.Thickness(1, 0, 1, 0);
            border.CornerRadius = new Avalonia.CornerRadius(0);
            border.Padding = new Avalonia.Thickness(0);
            border.Margin = new Avalonia.Thickness(0);
        }
        else if (popup.Child is Panel panel)
        {
            // CRITICAL: If child is a Panel/Grid directly, style it to remove white background
            panel.ClearValue(Panel.BackgroundProperty);
            panel.ClearValue(Panel.MarginProperty);
            panel.SetValue(Panel.BackgroundProperty, darkBrush);
            panel.SetValue(Panel.MarginProperty, new Avalonia.Thickness(0));
            panel.Background = darkBrush;
            panel.Margin = new Avalonia.Thickness(0);
        }
        else if (popup.Child is Control control)
        {
            // Try to set background on controls that have Background property
            // Only specific control types have BackgroundProperty, so we check the type
            if (control is Border borderControl)
            {
                borderControl.ClearValue(Border.BackgroundProperty);
                borderControl.SetValue(Border.BackgroundProperty, darkBrush);
                borderControl.Background = darkBrush;
            }
            else if (control is Panel panelControl)
            {
                panelControl.ClearValue(Panel.BackgroundProperty);
                panelControl.SetValue(Panel.BackgroundProperty, darkBrush);
                panelControl.Background = darkBrush;
            }
            else if (control is Button buttonControl)
            {
                buttonControl.ClearValue(Button.BackgroundProperty);
                buttonControl.SetValue(Button.BackgroundProperty, darkBrush);
                buttonControl.Background = darkBrush;
            }
        }
        
        // Apply styling to all borders
        StyleAllBordersInPopup(popup, darkBrush, borderBrush);
    }
    
    private static void StyleAllBordersInPopup(Popup popup, SolidColorBrush darkBrush, SolidColorBrush borderBrush)
    {
        
        // Also style from popup's parent perspective
        if (popup.Parent is Visual parentVisual)
        {
            var parentBorders = parentVisual.GetVisualDescendants().OfType<Border>()
                .Where(b => b.Parent == popup || (b.Parent is Popup && b.Parent == popup))
                .ToList();
            foreach (var b in parentBorders)
            {
                StyleBorder(b, darkBrush, borderBrush);
            }
        }
        
        // Style ALL borders in the popup - CRITICAL to remove white borders
        // Include borders that are children of the popup's child
        var allBorders = new List<Border>();
        if (popup.Child != null)
        {
            if (popup.Child is Visual childVisualForBorders)
            {
                allBorders.AddRange(childVisualForBorders.GetVisualDescendants().OfType<Border>());
            }
            if (popup.Child is Border childBorder)
            {
                allBorders.Add(childBorder);
            }
        }
        if (popup is Visual popupVisual)
        {
            allBorders.AddRange(popupVisual.GetVisualDescendants().OfType<Border>());
        }
        
        foreach (var b in allBorders.Distinct())
        {
            StyleBorder(b, darkBrush, borderBrush);
        }
        
        // Style ALL panels in the popup (this prevents white background)
        var allPanels = new List<Panel>();
        if (popup.Child != null && popup.Child is Visual childVisualForPanels)
        {
            allPanels.AddRange(childVisualForPanels.GetVisualDescendants().OfType<Panel>());
        }
        if (popup is Visual popupVisual2)
        {
            allPanels.AddRange(popupVisual2.GetVisualDescendants().OfType<Panel>());
        }
        foreach (var panel in allPanels.Distinct())
        {
            panel.ClearValue(Panel.BackgroundProperty);
            panel.ClearValue(Panel.MarginProperty);
            panel.SetValue(Panel.BackgroundProperty, darkBrush);
            panel.SetValue(Panel.MarginProperty, new Avalonia.Thickness(0));
            panel.Background = darkBrush;
            panel.Margin = new Avalonia.Thickness(0);
        }
        
        // Style ScrollViewer if present
        var scrollViewers = new List<ScrollViewer>();
        if (popup.Child != null && popup.Child is Visual childVisualForScrollViewers)
        {
            scrollViewers.AddRange(childVisualForScrollViewers.GetVisualDescendants().OfType<ScrollViewer>());
        }
        if (popup is Visual popupVisual4)
        {
            scrollViewers.AddRange(popupVisual4.GetVisualDescendants().OfType<ScrollViewer>());
        }
        foreach (var scrollViewer in scrollViewers.Distinct())
        {
            scrollViewer.ClearValue(ScrollViewer.BackgroundProperty);
            scrollViewer.ClearValue(ScrollViewer.MarginProperty);
            scrollViewer.ClearValue(ScrollViewer.PaddingProperty);
            scrollViewer.SetValue(ScrollViewer.BackgroundProperty, darkBrush);
            scrollViewer.SetValue(ScrollViewer.MarginProperty, new Avalonia.Thickness(0));
            scrollViewer.SetValue(ScrollViewer.PaddingProperty, new Avalonia.Thickness(0));
            scrollViewer.Background = darkBrush;
            scrollViewer.Margin = new Avalonia.Thickness(0);
            scrollViewer.Padding = new Avalonia.Thickness(0);
        }
        
        // Wire up submenu items
        WireUpSubmenuItems(popup, darkBrush);
        
        // Also wire up DropDownButton elements in the popup
        WireUpDropDownButtons(popup, darkBrush, borderBrush);
    }
    
    private static void WireUpDropDownButtons(Popup popup, SolidColorBrush darkBrush, SolidColorBrush borderBrush)
    {
        // Find all DropDownButton elements in the popup
        var dropDownButtons = new List<DropDownButton>();
        if (popup.Child != null && popup.Child is Visual childVisual)
        {
            dropDownButtons.AddRange(childVisual.GetVisualDescendants().OfType<DropDownButton>());
        }
        if (popup is Visual popupVisual)
        {
            dropDownButtons.AddRange(popupVisual.GetVisualDescendants().OfType<DropDownButton>());
        }
        
        foreach (var dropDownButton in dropDownButtons.Distinct())
        {
            // Style the DropDownButton itself
            dropDownButton.ClearValue(DropDownButton.BackgroundProperty);
            dropDownButton.ClearValue(DropDownButton.ForegroundProperty);
            dropDownButton.ClearValue(DropDownButton.BorderBrushProperty);
            dropDownButton.ClearValue(DropDownButton.BorderThicknessProperty);
            dropDownButton.SetValue(DropDownButton.BackgroundProperty, darkBrush);
            dropDownButton.SetValue(DropDownButton.ForegroundProperty, new SolidColorBrush(Color.Parse("White")));
            dropDownButton.SetValue(DropDownButton.BorderBrushProperty, new SolidColorBrush(Colors.Transparent));
            dropDownButton.SetValue(DropDownButton.BorderThicknessProperty, new Avalonia.Thickness(0));
            dropDownButton.Background = darkBrush;
            dropDownButton.Foreground = new SolidColorBrush(Color.Parse("White"));
            dropDownButton.BorderBrush = new SolidColorBrush(Colors.Transparent);
            dropDownButton.BorderThickness = new Avalonia.Thickness(0);
            
            // Wire up hover events for DropDownButton
            dropDownButton.PointerEntered -= OnDropDownButtonPointerEntered;
            dropDownButton.PointerExited -= OnDropDownButtonPointerExited;
            dropDownButton.PointerEntered += OnDropDownButtonPointerEntered;
            dropDownButton.PointerExited += OnDropDownButtonPointerExited;
        }
    }
    
    private static void OnDropDownButtonPointerEntered(object? sender, PointerEventArgs e)
    {
        if (sender is DropDownButton dropDownButton)
        {
            var hoverBrush = new SolidColorBrush(Color.Parse("#2d2d2d"));
            dropDownButton.ClearValue(DropDownButton.BackgroundProperty);
            dropDownButton.SetValue(DropDownButton.BackgroundProperty, hoverBrush);
            dropDownButton.Background = hoverBrush;
            
            // Style ContentPresenter
            var contentPresenter = dropDownButton.GetVisualDescendants()
                .OfType<Panel>()
                .FirstOrDefault(c => c.GetType().Name == "ContentPresenter");
            if (contentPresenter != null)
            {
                contentPresenter.ClearValue(Panel.BackgroundProperty);
                contentPresenter.SetValue(Panel.BackgroundProperty, hoverBrush);
                contentPresenter.Background = hoverBrush;
            }
        }
    }
    
    private static void OnDropDownButtonPointerExited(object? sender, PointerEventArgs e)
    {
        if (sender is DropDownButton dropDownButton)
        {
            var defaultBrush = new SolidColorBrush(Color.Parse("#1c1c1c"));
            dropDownButton.ClearValue(DropDownButton.BackgroundProperty);
            dropDownButton.SetValue(DropDownButton.BackgroundProperty, defaultBrush);
            dropDownButton.Background = defaultBrush;
            
            // Style ContentPresenter
            var contentPresenter = dropDownButton.GetVisualDescendants()
                .OfType<Panel>()
                .FirstOrDefault(c => c.GetType().Name == "ContentPresenter");
            if (contentPresenter != null)
            {
                contentPresenter.ClearValue(Panel.BackgroundProperty);
                contentPresenter.SetValue(Panel.BackgroundProperty, defaultBrush);
                contentPresenter.Background = defaultBrush;
            }
        }
    }
    
    private static void StyleBorder(Border b, SolidColorBrush darkBrush, SolidColorBrush borderBrush)
    {
        // Clear any style bindings first
        b.ClearValue(Border.BackgroundProperty);
        b.ClearValue(Border.BorderBrushProperty);
        b.ClearValue(Border.BorderThicknessProperty);
        b.ClearValue(Border.CornerRadiusProperty);
        b.ClearValue(Border.PaddingProperty);
        b.ClearValue(Border.MarginProperty);
        
        // Use SetValue to override any styles, then set directly
        b.SetValue(Border.BackgroundProperty, darkBrush);
        b.SetValue(Border.BorderBrushProperty, borderBrush);
        b.SetValue(Border.BorderThicknessProperty, new Avalonia.Thickness(1, 0, 1, 0));
        b.SetValue(Border.CornerRadiusProperty, new Avalonia.CornerRadius(0));
        b.SetValue(Border.PaddingProperty, new Avalonia.Thickness(0));
        b.SetValue(Border.MarginProperty, new Avalonia.Thickness(0));
        b.Background = darkBrush;
        b.BorderBrush = borderBrush;
        b.BorderThickness = new Avalonia.Thickness(1, 0, 1, 0);
        b.CornerRadius = new Avalonia.CornerRadius(0);
        b.Padding = new Avalonia.Thickness(0);
        b.Margin = new Avalonia.Thickness(0);
    }
    
    private static void WireUpSubmenuItems(Popup popup, SolidColorBrush darkBrush)
    {
        // Style and wire up hover events for submenu items
        var submenuItems = popup?.GetVisualDescendants().OfType<MenuItem>().ToList();
        if (submenuItems != null)
        {
            foreach (var submenuItem in submenuItems)
            {
                // Set initial styling
                submenuItem.Background = new SolidColorBrush(Color.Parse("#1c1c1c"));
                submenuItem.Foreground = new SolidColorBrush(Color.Parse("White"));
                
                // Style all Borders inside
                var itemBorders = submenuItem.GetVisualDescendants().OfType<Border>().ToList();
                foreach (var itemBorder in itemBorders)
                {
                    itemBorder.Background = new SolidColorBrush(Color.Parse("#1c1c1c"));
                    itemBorder.CornerRadius = new Avalonia.CornerRadius(0);
                }
                
                // Style ContentPresenter
                var contentPresenter = submenuItem.GetVisualDescendants()
                    .OfType<Panel>()
                    .FirstOrDefault(c => c.GetType().Name == "ContentPresenter");
                if (contentPresenter != null)
                {
                    contentPresenter.Background = new SolidColorBrush(Color.Parse("#1c1c1c"));
                }
                
                // Wire up hover events
                submenuItem.PointerEntered -= OnMenuItemPointerEntered;
                submenuItem.PointerExited -= OnMenuItemPointerExited;
                submenuItem.PointerEntered += OnMenuItemPointerEntered;
                submenuItem.PointerExited += OnMenuItemPointerExited;
            }
        }
    }
}



