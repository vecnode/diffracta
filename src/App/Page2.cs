using Avalonia.Controls;
using Avalonia.Interactivity;
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
    }
}

