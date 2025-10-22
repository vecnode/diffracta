using System.ComponentModel;

namespace Diffracta
{
    public class MainTempo : INotifyPropertyChanged
    {
        private int _seconds;

        public event PropertyChangedEventHandler? PropertyChanged;

        public int Seconds
        {
            get => _seconds;
            set
            {
                if (_seconds != value)
                {
                    _seconds = value;
                    OnPropertyChanged(nameof(Seconds));
                    OnPropertyChanged(nameof(TimeDisplay));
                }
            }
        }

        public string TimeDisplay => TimeSpan.FromSeconds(_seconds).ToString(@"hh\:mm\:ss");

        public MainTempo()
        {
            _seconds = 0;
        }

        public void Increment()
        {
            Seconds++;
        }

        public void Reset()
        {
            Seconds = 0;
        }

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}