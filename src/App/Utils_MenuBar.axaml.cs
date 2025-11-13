using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Presenters;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.VisualTree;
using Avalonia;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Diffracta;

/// <summary>
/// Helper class for MenuBar styling
/// </summary>
public static class Utils_MenuBar
{
    /// <summary>
    /// Wires up MenuBar styling
    /// </summary>
    public static void WireUpMenuBarStyling(Menu? menuBar)
    {
        if (menuBar == null)
        {
            return;
        }
        
        // Apply Menu styling programmatically
        menuBar.Background = new SolidColorBrush(Color.Parse("#2d2d2d"));
        menuBar.Foreground = new SolidColorBrush(Color.Parse("White"));
        menuBar.BorderBrush = new SolidColorBrush(Color.Parse("#555555"));
        menuBar.BorderThickness = new Avalonia.Thickness(0);
        menuBar.FontSize = 12;
        menuBar.Height = 26;
        
        // Hook into Popups to detect when dropdowns open
        HookIntoPopups(menuBar);
        
        // Wire up when menu is loaded
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
            
            // Wire up hover to keep text white for top-level menu items
            var menuItems = menuBar.GetVisualDescendants()
                .OfType<MenuItem>()
                .ToList();
            
            var whiteBrush = new SolidColorBrush(Color.Parse("White"));
            
            foreach (var menuItem in menuItems)
            {
                
                // Wire up hover events for top-level menu items to keep text white
                // Only wire up if parent is Menu (top-level items)
                if (menuItem.Parent is Menu)
                {
                    menuItem.PointerEntered += (s, e) =>
                    {
                        menuItem.Foreground = whiteBrush;
                        // Also ensure text elements stay white
                        var accessTexts = menuItem.GetVisualDescendants().OfType<AccessText>().ToList();
                        foreach (var accessText in accessTexts)
                        {
                            accessText.Foreground = whiteBrush;
                        }
                        var textBlocks = menuItem.GetVisualDescendants().OfType<TextBlock>().ToList();
                        foreach (var textBlock in textBlocks)
                        {
                            textBlock.Foreground = whiteBrush;
                        }
                    };
                    
                    menuItem.PointerExited += (s, e) =>
                    {
                        menuItem.Foreground = whiteBrush;
                        // Also ensure text elements stay white
                        var accessTexts = menuItem.GetVisualDescendants().OfType<AccessText>().ToList();
                        foreach (var accessText in accessTexts)
                        {
                            accessText.Foreground = whiteBrush;
                        }
                        var textBlocks = menuItem.GetVisualDescendants().OfType<TextBlock>().ToList();
                        foreach (var textBlock in textBlocks)
                        {
                            textBlock.Foreground = whiteBrush;
                        }
                    };
                }
            }
        };
        
        // Also wire up when menu items are added dynamically
        menuBar.AttachedToVisualTree += (_, __) =>
        {
            var menuPanel = menuBar.GetVisualDescendants()
                .OfType<Panel>()
                .FirstOrDefault(p => p.Parent == menuBar);
            if (menuPanel != null)
            {
                menuPanel.Background = new SolidColorBrush(Color.Parse("#2d2d2d"));
            }
            
            // Wire up hover to keep text white for top-level menu items
        var menuItems = menuBar.GetVisualDescendants()
            .OfType<MenuItem>()
            .ToList();
        
            var whiteBrush = new SolidColorBrush(Color.Parse("White"));
            
            foreach (var menuItem in menuItems)
            {
                
                // Wire up hover events for top-level menu items to keep text white
                // Only wire up if parent is Menu (top-level items)
                if (menuItem.Parent is Menu)
                {
                    menuItem.PointerEntered += (s, e) =>
                    {
                        menuItem.Foreground = whiteBrush;
                        // Also ensure text elements stay white
                        var accessTexts = menuItem.GetVisualDescendants().OfType<AccessText>().ToList();
                        foreach (var accessText in accessTexts)
                        {
                            accessText.Foreground = whiteBrush;
                        }
                        var textBlocks = menuItem.GetVisualDescendants().OfType<TextBlock>().ToList();
                        foreach (var textBlock in textBlocks)
                        {
                            textBlock.Foreground = whiteBrush;
                        }
                    };
                    
                    menuItem.PointerExited += (s, e) =>
                    {
                        menuItem.Foreground = whiteBrush;
                        // Also ensure text elements stay white
                        var accessTexts = menuItem.GetVisualDescendants().OfType<AccessText>().ToList();
                        foreach (var accessText in accessTexts)
                        {
                            accessText.Foreground = whiteBrush;
                        }
                        var textBlocks = menuItem.GetVisualDescendants().OfType<TextBlock>().ToList();
                        foreach (var textBlock in textBlocks)
                        {
                            textBlock.Foreground = whiteBrush;
                        }
                    };
                }
            }
        };
    }
    
    /// <summary>
    /// Hooks into Popups to detect when dropdown menus open and style nested MenuItems
    /// </summary>
    private static void HookIntoPopups(Menu menuBar)
    {
        try
        {
            // Find all Popups in the menu
            var popups = menuBar.GetVisualDescendants()
                .OfType<Popup>()
                .ToList();
            
            foreach (var popup in popups)
            {
                // Hook into Popup.Opened event
                popup.Opened += (s, e) =>
                {
                    PrintPopupMenuItems(popup);
                };
            }
            
            // Also hook into MenuItem.SubmenuOpened to catch when dropdowns open
            var menuItems = menuBar.GetVisualDescendants()
                .OfType<MenuItem>()
                .ToList();
            
            foreach (var menuItem in menuItems)
            {
                menuItem.SubmenuOpened += (s, e) =>
                {
                    // Find the Popup associated with this MenuItem
                    var popup = menuItem.GetVisualDescendants()
                        .OfType<Popup>()
                        .FirstOrDefault();
                    
                    if (popup != null)
                    {
                        PrintPopupMenuItems(popup);
                    }
                };
            }
        }
        catch (Exception ex)
        {
            // Silently handle errors
        }
    }
    
    /// <summary>
    /// Styles MenuItems inside a Popup
    /// </summary>
    private static void PrintPopupMenuItems(Popup popup)
    {
        try
        {
            // Get all MenuItems inside the popup
            var menuItems = new List<MenuItem>();
            if (popup.Child is Control childControl)
            {
                menuItems.AddRange(childControl.GetVisualDescendants().OfType<MenuItem>());
            }
            
            // Apply white foreground to all MenuItems in popup and their text elements
            var whiteBrush = new SolidColorBrush(Color.Parse("White"));
            var transparentBrush = Brushes.Transparent;
            var defaultBgBrush = new SolidColorBrush(Color.Parse("#2d2d2d"));
            var hoverBgBrush = new SolidColorBrush(Color.Parse("#4d4d4d")); // Medium gray for nested menu items hover
            
            foreach (var menuItem in menuItems)
            {
                menuItem.Foreground = whiteBrush;
                menuItem.Background = defaultBgBrush;
                
                // Remove background from inner Border elements (like PART_LayoutRoot) so only MenuItem background shows
                var borders = menuItem.GetVisualDescendants().OfType<Border>().ToList();
                foreach (var border in borders)
                {
                    if (border.Name == "PART_LayoutRoot" || border.Background != null)
                    {
                        border.Background = transparentBrush;
                    }
                }
                
                // Remove background from inner Panel/Grid elements
                var panels = menuItem.GetVisualDescendants().OfType<Panel>().ToList();
                foreach (var panel in panels)
                {
                    if (panel.Background != null && panel.Background != transparentBrush)
                    {
                        panel.Background = transparentBrush;
                    }
                }
                
                // Style text elements inside MenuItem
                var accessTexts = menuItem.GetVisualDescendants().OfType<AccessText>().ToList();
                foreach (var accessText in accessTexts)
                {
                    accessText.Foreground = whiteBrush;
                }
                
                var textBlocks = menuItem.GetVisualDescendants().OfType<TextBlock>().ToList();
                foreach (var textBlock in textBlocks)
                {
                    textBlock.Foreground = whiteBrush;
                }
                
                var contentPresenters = menuItem.GetVisualDescendants().OfType<ContentPresenter>().ToList();
                foreach (var contentPresenter in contentPresenters)
                {
                    contentPresenter.Foreground = whiteBrush;
                }
                
                // Wire up hover effects - create event handlers that will be reused
                // Store references to ensure they persist
                var menuItemRef = menuItem; // Capture for closure
                
                void OnMenuItemPointerEntered(object? s, PointerEventArgs e)
                {
                    // Set background on MenuItem
                    menuItemRef.Background = hoverBgBrush;
                    // Also try setting on PART_LayoutRoot Border (the actual visual element)
                    var layoutRoot = menuItemRef.GetVisualDescendants().OfType<Border>().FirstOrDefault(b => b.Name == "PART_LayoutRoot");
                    if (layoutRoot != null)
                    {
                        layoutRoot.Background = hoverBgBrush;
                        
                        // Check for child elements that might need styling
                        var childPanels = layoutRoot.GetVisualDescendants().OfType<Panel>().ToList();
                        var childGrids = layoutRoot.GetVisualDescendants().OfType<Grid>().ToList();
                        
                        // Set background on child Panels and Grids if they have backgrounds
                        foreach (var panel in childPanels)
                        {
                            if (panel.Background != null && panel.Background != transparentBrush)
                            {
                                panel.Background = hoverBgBrush;
                            }
                        }
                        
                        foreach (var grid in childGrids)
                        {
                            if (grid.Background != null && grid.Background != transparentBrush)
                            {
                                grid.Background = hoverBgBrush;
                            }
                        }
                    }
                    // Ensure text stays white on hover
                    menuItemRef.Foreground = whiteBrush;
                    var accessTexts = menuItemRef.GetVisualDescendants().OfType<AccessText>().ToList();
                    foreach (var accessText in accessTexts)
                    {
                        accessText.Foreground = whiteBrush;
                    }
                    var textBlocks = menuItemRef.GetVisualDescendants().OfType<TextBlock>().ToList();
                    foreach (var textBlock in textBlocks)
                    {
                        textBlock.Foreground = whiteBrush;
                    }
                }
                
                void OnMenuItemPointerExited(object? s, PointerEventArgs e)
                {
                    // Set background on MenuItem
                    menuItemRef.Background = defaultBgBrush;
                    // Also reset PART_LayoutRoot Border to transparent
                    var layoutRoot = menuItemRef.GetVisualDescendants().OfType<Border>().FirstOrDefault(b => b.Name == "PART_LayoutRoot");
                    if (layoutRoot != null)
                    {
                        layoutRoot.Background = transparentBrush;
                        
                        // Reset child elements to transparent as well
                        var childPanels = layoutRoot.GetVisualDescendants().OfType<Panel>().ToList();
                        var childGrids = layoutRoot.GetVisualDescendants().OfType<Grid>().ToList();
                        
                        foreach (var panel in childPanels)
                        {
                            if (panel.Background == hoverBgBrush)
                            {
                                panel.Background = transparentBrush;
                            }
                        }
                        
                        foreach (var grid in childGrids)
                        {
                            if (grid.Background == hoverBgBrush)
                            {
                                grid.Background = transparentBrush;
                            }
                        }
                    }
                    // Ensure text stays white
                    menuItemRef.Foreground = whiteBrush;
                    var accessTexts = menuItemRef.GetVisualDescendants().OfType<AccessText>().ToList();
                    foreach (var accessText in accessTexts)
                    {
                        accessText.Foreground = whiteBrush;
                    }
                    var textBlocks = menuItemRef.GetVisualDescendants().OfType<TextBlock>().ToList();
                    foreach (var textBlock in textBlocks)
                    {
                        textBlock.Foreground = whiteBrush;
                    }
                }
                
                // Wire up on MenuItem itself
                menuItem.PointerEntered += OnMenuItemPointerEntered;
                menuItem.PointerExited += OnMenuItemPointerExited;
                
                // Wire up on hit-testable elements that could intercept pointer events
                // This ensures hover works even if child elements capture the pointer
                foreach (var border in borders)
                {
                    border.PointerEntered += OnMenuItemPointerEntered;
                    border.PointerExited += OnMenuItemPointerExited;
                }
                
                foreach (var panel in panels)
                {
                    panel.PointerEntered += OnMenuItemPointerEntered;
                    panel.PointerExited += OnMenuItemPointerExited;
                }
                
                var grids = menuItem.GetVisualDescendants().OfType<Grid>().ToList();
                foreach (var grid in grids)
                {
                    grid.PointerEntered += OnMenuItemPointerEntered;
                    grid.PointerExited += OnMenuItemPointerExited;
                }
                
                // Also wire up on ContentPresenter elements
                foreach (var contentPresenter in contentPresenters)
                {
                    contentPresenter.PointerEntered += OnMenuItemPointerEntered;
                    contentPresenter.PointerExited += OnMenuItemPointerExited;
                }
            }
            
            // Style Popup Border and Panel containers
            if (popup.Child is Border popupBorder)
            {
                // Apply dark theme styling to Popup Border
                popupBorder.Background = new SolidColorBrush(Color.Parse("#2d2d2d"));
                popupBorder.CornerRadius = new Avalonia.CornerRadius(0);
                popupBorder.BorderThickness = new Avalonia.Thickness(1);
                popupBorder.BorderBrush = new SolidColorBrush(Color.Parse("#555555"));
                
                // Also check for Panel containers inside the Border
                var panels = popupBorder.GetVisualDescendants().OfType<Panel>().ToList();
                foreach (var panel in panels)
                {
                    if (panel.Background == null || panel.Background.ToString() == "#fff2f2f2" || panel.Background.ToString() == "#FFF2F2F2")
                    {
                        panel.Background = new SolidColorBrush(Color.Parse("#2d2d2d"));
                    }
                }
            }
            else if (popup.Child is Panel popupPanel)
            {
                // Apply dark theme styling to Popup Panel
                if (popupPanel.Background == null || popupPanel.Background.ToString() == "#fff2f2f2" || popupPanel.Background.ToString() == "#FFF2F2F2")
                {
                    popupPanel.Background = new SolidColorBrush(Color.Parse("#2d2d2d"));
                }
            }
        }
        catch (Exception ex)
        {
            // Silently handle errors
        }
    }
}


