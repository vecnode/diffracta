using Avalonia.Controls;
using Avalonia.Interactivity;
using System.ComponentModel;

namespace Diffracta;

public partial class ChildWindow : Window, INotifyPropertyChanged
{
    private string _sharedMessage = "This data is shared with the main window in real-time!";
    private MainTempo? _sharedTempo;

    /// <summary>
    /// PropertyChanged event for INotifyPropertyChanged implementation.
    /// Uses 'new' keyword to explicitly hide the inherited AvaloniaObject.PropertyChanged event.
    /// </summary>
    public new event PropertyChangedEventHandler? PropertyChanged;

    public string SharedMessage
    {
        get => _sharedMessage;
        set
        {
            if (_sharedMessage != value)
            {
                _sharedMessage = value;
                OnPropertyChanged(nameof(SharedMessage));
            }
        }
    }

    public string TempoDisplay => _sharedTempo?.TimeDisplay ?? "00:00:00";

    public ChildWindow()
    {
        InitializeComponent();
        DataContext = this;
    }

    /// <summary>
    /// Set the shared tempo object to enable real-time data sharing
    /// </summary>
    public void SetSharedTempo(MainTempo tempo)
    {
        if (_sharedTempo != null)
        {
            _sharedTempo.PropertyChanged -= OnTempoPropertyChanged;
        }
        
        _sharedTempo = tempo;
        
        if (_sharedTempo != null)
        {
            _sharedTempo.PropertyChanged += OnTempoPropertyChanged;
            OnPropertyChanged(nameof(TempoDisplay));
        }
    }

    private void OnTempoPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainTempo.TimeDisplay))
        {
            OnPropertyChanged(nameof(TempoDisplay));
        }
    }

    private void CloseButton_Click(object? sender, RoutedEventArgs e)
    {
        Close();
    }

    protected override void OnClosed(EventArgs e)
    {
        // Clean up event handlers
        if (_sharedTempo != null)
        {
            _sharedTempo.PropertyChanged -= OnTempoPropertyChanged;
        }
        base.OnClosed(e);
    }

    protected virtual void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

