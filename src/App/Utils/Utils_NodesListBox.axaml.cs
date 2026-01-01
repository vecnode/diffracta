using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Media;
using Diffracta.Graphics;

namespace Diffracta;

/// <summary>
/// UserControl for displaying and managing shader processing nodes visualization
/// </summary>
public partial class Utils_NodesListBox : UserControl
{
    private ShaderSurface? _surface;

    /// <summary>
    /// Sets the ShaderSurface reference for this control
    /// </summary>
    public ShaderSurface? Surface
    {
        get => _surface;
        set
        {
            _surface = value;
            if (_surface != null)
            {
                WireUpProcessingNodeControls();
            }
        }
    }

    public Utils_NodesListBox()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Wires up click handlers for processing node rectangles and value change handlers for sliders
    /// </summary>
    private void WireUpProcessingNodeControls()
    {
        if (_surface == null) 
        {
            System.Diagnostics.Debug.WriteLine("WireUpProcessingNodeControls: Surface is null");
            return;
        }
        
        try
        {
            // Wire up all 6 VFX processing nodes (0-5)
            for (int i = 0; i < 6; i++)
            {
                int slotIndex = i; // Capture for closure
                
                // Wire up rectangle border click to toggle active state
                try
                {
                    var rectButton = this.FindControl<Border>($"Node{i + 1}RectButton");
                    if (rectButton != null)
                    {
                        rectButton.PointerPressed += (s, e) => {
                            try
                            {
                                System.Diagnostics.Debug.WriteLine($"Node{i + 1}RectButton clicked! Position: {e.GetPosition(rectButton)}");
                                e.Handled = true; // Mark as handled
                                if (_surface != null)
                                {
                                    // Allow toggling even if shader not loaded (for UI feedback)
                                    bool currentState = _surface.GetSlotActive(slotIndex);
                                    bool newState = !currentState;
                                    _surface.SetSlotActive(slotIndex, newState);
                                    System.Diagnostics.Debug.WriteLine($"Toggled node {slotIndex} from {currentState} to {newState}");
                                    
                                    // Immediately update the visualization to show/hide subrow
                                    UpdateShaderNodesVisualization();
                                }
                                else
                                {
                                    System.Diagnostics.Debug.WriteLine($"Node{i + 1}RectButton clicked but _surface is null!");
                                }
                            }
                            catch (Exception ex)
                            {
                                System.Diagnostics.Debug.WriteLine($"Error toggling node {slotIndex}: {ex.Message}\n{ex.StackTrace}");
                            }
                        };
                        
                        // Make sure it's hit-testable (visibility is always true, set in XAML)
                        rectButton.IsHitTestVisible = true;
                        System.Diagnostics.Debug.WriteLine($"Wired up Node{i + 1}RectButton - IsHitTestVisible: {rectButton.IsHitTestVisible}");
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"Node{i + 1}RectButton not found!");
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error finding Node{i + 1}RectButton: {ex.Message}");
                }
                
                // Wire up slider value changes
                try
                {
                    var slider = this.FindControl<Slider>($"Node{i + 1}Slider");
                    var valueText = this.FindControl<TextBlock>($"Node{i + 1}Value");
                    if (slider != null)
                    {
                        slider.ValueChanged += (_, e) => {
                            try
                            {
                                if (_surface != null && e.NewValue is double value)
                                {
                                    float floatValue = (float)value;
                                    _surface.SetSlotValue(slotIndex, floatValue);
                                    
                                    // Update the value display immediately for real-time feedback
                                    if (valueText != null)
                                    {
                                        valueText.Text = floatValue.ToString("F2");
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                System.Diagnostics.Debug.WriteLine($"Error setting node {slotIndex} value: {ex.Message}");
                            }
                        };
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error finding Node{i + 1}Slider: {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error in WireUpProcessingNodeControls: {ex.Message}\n{ex.StackTrace}");
            throw; // Re-throw to be caught by caller
        }
    }
    
    /// <summary>
    /// Updates the shader nodes visualization
    /// Shows the processing pipeline as layers: Main Shader -> Processing nodes
    /// Reads directly from runtime shader state (IsMainShaderLoaded, GetSlotActive, etc.)
    /// Shows/hides subrows with sliders when nodes are active
    /// </summary>
    public void UpdateShaderNodesVisualization()
    {
        if (_surface == null) return;

        // Node 0: Main Shader (Global Texture)
        var node0Border = this.FindControl<Border>("Node0Border");
        var node0Rect = this.FindControl<Rectangle>("Node0Rect");
        var node0Text = this.FindControl<TextBlock>("Node0Text");
        
        bool isMainShaderLoaded = _surface.IsMainShaderLoaded;
        
        // Update Node 0 visualization
        if (node0Rect != null)
        {
            node0Rect.Fill = isMainShaderLoaded
                ? SolidColorBrush.Parse("#ff8c00") 
                : SolidColorBrush.Parse("#666666");
        }
        if (node0Text != null)
        {
            node0Text.Text = isMainShaderLoaded 
                ? "Node 0: Global Texture" 
                : "Node 0: Global Texture (No Shader)";
        }
        if (node0Border != null)
        {
            node0Border.BorderBrush = isMainShaderLoaded
                ? SolidColorBrush.Parse("#ff8c00") 
                : SolidColorBrush.Parse("#666666");
        }

        // Track previous layer state for arrow visibility
        bool previousLayerActive = isMainShaderLoaded;
        
        // Update all 6 VFX processing nodes
        // Always show all nodes, but indicate their state (loaded/unloaded, active/inactive)
        for (int i = 0; i < 6; i++)
        {
            var nodeRectButton = this.FindControl<Border>($"Node{i + 1}RectButton");
            var nodeRect = this.FindControl<Rectangle>($"Node{i + 1}Rect");
            var nodeBorder = this.FindControl<Border>($"Node{i + 1}Border");
            var nodeText = this.FindControl<TextBlock>($"Node{i + 1}Text");
            var nodeArrow = this.FindControl<Avalonia.Controls.Shapes.Path>($"Node{i + 1}Arrow");
            var nodeSubrow = this.FindControl<Border>($"Node{i + 1}Subrow");
            var nodeSlider = this.FindControl<Slider>($"Node{i + 1}Slider");
            var nodeValue = this.FindControl<TextBlock>($"Node{i + 1}Value");
            
            bool isShaderLoaded = _surface.IsProcessingNodeShaderLoaded(i);
            bool isSlotActive = _surface.GetSlotActive(i);
            string shaderName = _surface.GetProcessingNodeShaderName(i);
            float slotValue = _surface.GetSlotValue(i);
            
            // All nodes are always visible (set in XAML)
            
            // Update rectangle color based on loaded and active state
            var rectColor = isShaderLoaded && isSlotActive
                ? SolidColorBrush.Parse("#ff8c00") 
                : isShaderLoaded
                    ? SolidColorBrush.Parse("#888888") // Loaded but inactive
                    : SolidColorBrush.Parse("#666666"); // Not loaded
            
            if (nodeRect != null)
            {
                nodeRect.Fill = rectColor;
            }
            if (nodeBorder != null)
            {
                nodeBorder.BorderBrush = isShaderLoaded && isSlotActive
                    ? SolidColorBrush.Parse("#ff8c00") 
                    : isShaderLoaded
                        ? SolidColorBrush.Parse("#888888")
                        : SolidColorBrush.Parse("#666666");
                // Visibility is always true (set in XAML)
            }
            if (nodeText != null)
            {
                // Show node name, or "Not Available" if no shader name
                string displayName = !string.IsNullOrEmpty(shaderName) ? shaderName : $"Processing Node {i + 1}";
                nodeText.Text = $"Node {i + 1}: {displayName}";
            }
            if (nodeArrow != null)
            {
                // Arrow is always visible (set in XAML)
                nodeArrow.Stroke = (previousLayerActive && isShaderLoaded && isSlotActive)
                    ? SolidColorBrush.Parse("#ff8c00")
                    : SolidColorBrush.Parse("#666666");
            }
            
            // Update subrow visibility based on active state
            if (nodeSubrow != null)
            {
                bool shouldShowSubrow = isShaderLoaded && isSlotActive;
                bool wasVisible = nodeSubrow.IsVisible;
                nodeSubrow.IsVisible = shouldShowSubrow;
                // Only log when visibility changes to avoid spam
                if (wasVisible != shouldShowSubrow)
                {
                    System.Diagnostics.Debug.WriteLine($"Node {i + 1} subrow visibility changed: {wasVisible} -> {shouldShowSubrow} (shaderLoaded: {isShaderLoaded}, slotActive: {isSlotActive})");
                }
            }
            
            // Update slider and value display
            if (nodeSlider != null && isShaderLoaded && isSlotActive)
            {
                // Only update if value changed to avoid feedback loop
                if (Math.Abs(nodeSlider.Value - slotValue) > 0.001)
                {
                    nodeSlider.Value = slotValue;
                }
            }
            if (nodeValue != null && isShaderLoaded && isSlotActive)
            {
                nodeValue.Text = slotValue.ToString("F2");
            }
            
            // Update previous layer state for next iteration
            previousLayerActive = isShaderLoaded && isSlotActive;
        }
        
        // Node 7: Output Texture (MASTER) - always show orange border when main shader is loaded (like Node 0)
        var node7Border = this.FindControl<Border>("Node7Border");
        var node7Rect = this.FindControl<Rectangle>("Node7Rect");
        
        if (node7Border != null)
        {
            node7Border.BorderBrush = isMainShaderLoaded
                ? SolidColorBrush.Parse("#ff8c00") 
                : SolidColorBrush.Parse("#666666");
        }
        if (node7Rect != null)
        {
            node7Rect.Fill = isMainShaderLoaded
                ? SolidColorBrush.Parse("#ff8c00") 
                : SolidColorBrush.Parse("#666666");
        }
    }
}

