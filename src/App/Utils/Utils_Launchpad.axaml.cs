using Avalonia.Controls;
using Avalonia.Interactivity;
using System.Collections.Generic;

namespace Diffracta;

/// <summary>
/// UserControl for displaying and managing launchpad pads
/// </summary>
public partial class Utils_Launchpad : UserControl
{
    // Pad to row/column mapping (row, column) - rows and columns are 0-indexed internally
    private readonly Dictionary<Button, (int row, int col)> _padMapping = new();
    
    /// <summary>
    /// Event fired when a pad is clicked. Passes the pad number (1-32)
    /// </summary>
    public event EventHandler<int>? PadClicked;
    
    public Utils_Launchpad()
    {
        InitializeComponent();
        WireUpPadButtons();
    }
    
    /// <summary>
    /// Wires up click handlers for all pad buttons
    /// </summary>
    private void WireUpPadButtons()
    {
        // Map all 32 pads (4 rows x 8 columns) to their row/column positions
        // Row 0: P01-P08
        _padMapping[Pad01Button] = (0, 0); Pad01Button.Click += OnPadClicked;
        _padMapping[Pad02Button] = (0, 1); Pad02Button.Click += OnPadClicked;
        _padMapping[Pad03Button] = (0, 2); Pad03Button.Click += OnPadClicked;
        _padMapping[Pad04Button] = (0, 3); Pad04Button.Click += OnPadClicked;
        _padMapping[Pad05Button] = (0, 4); Pad05Button.Click += OnPadClicked;
        _padMapping[Pad06Button] = (0, 5); Pad06Button.Click += OnPadClicked;
        _padMapping[Pad07Button] = (0, 6); Pad07Button.Click += OnPadClicked;
        _padMapping[Pad08Button] = (0, 7); Pad08Button.Click += OnPadClicked;
        
        // Row 1: P09-P16
        _padMapping[Pad09Button] = (1, 0); Pad09Button.Click += OnPadClicked;
        _padMapping[Pad10Button] = (1, 1); Pad10Button.Click += OnPadClicked;
        _padMapping[Pad11Button] = (1, 2); Pad11Button.Click += OnPadClicked;
        _padMapping[Pad12Button] = (1, 3); Pad12Button.Click += OnPadClicked;
        _padMapping[Pad13Button] = (1, 4); Pad13Button.Click += OnPadClicked;
        _padMapping[Pad14Button] = (1, 5); Pad14Button.Click += OnPadClicked;
        _padMapping[Pad15Button] = (1, 6); Pad15Button.Click += OnPadClicked;
        _padMapping[Pad16Button] = (1, 7); Pad16Button.Click += OnPadClicked;
        
        // Row 2: P17-P24
        _padMapping[Pad17Button] = (2, 0); Pad17Button.Click += OnPadClicked;
        _padMapping[Pad18Button] = (2, 1); Pad18Button.Click += OnPadClicked;
        _padMapping[Pad19Button] = (2, 2); Pad19Button.Click += OnPadClicked;
        _padMapping[Pad20Button] = (2, 3); Pad20Button.Click += OnPadClicked;
        _padMapping[Pad21Button] = (2, 4); Pad21Button.Click += OnPadClicked;
        _padMapping[Pad22Button] = (2, 5); Pad22Button.Click += OnPadClicked;
        _padMapping[Pad23Button] = (2, 6); Pad23Button.Click += OnPadClicked;
        _padMapping[Pad24Button] = (2, 7); Pad24Button.Click += OnPadClicked;
        
        // Row 3: P25-P32
        _padMapping[Pad25Button] = (3, 0); Pad25Button.Click += OnPadClicked;
        _padMapping[Pad26Button] = (3, 1); Pad26Button.Click += OnPadClicked;
        _padMapping[Pad27Button] = (3, 2); Pad27Button.Click += OnPadClicked;
        _padMapping[Pad28Button] = (3, 3); Pad28Button.Click += OnPadClicked;
        _padMapping[Pad29Button] = (3, 4); Pad29Button.Click += OnPadClicked;
        _padMapping[Pad30Button] = (3, 5); Pad30Button.Click += OnPadClicked;
        _padMapping[Pad31Button] = (3, 6); Pad31Button.Click += OnPadClicked;
        _padMapping[Pad32Button] = (3, 7); Pad32Button.Click += OnPadClicked;
    }
    
    /// <summary>
    /// Handles pad button clicks
    /// </summary>
    private void OnPadClicked(object? sender, RoutedEventArgs e)
    {
        if (sender is Button button && _padMapping.TryGetValue(button, out var position))
        {
            int row = position.row;
            int col = position.col;
            
            // Calculate pad number (1-32): padNumber = row * 8 + col + 1
            int padNumber = row * 8 + col + 1;
            
            // Fire the event to notify subscribers
            PadClicked?.Invoke(this, padNumber);
        }
    }
}

