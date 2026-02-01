using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;

namespace OpenBroadcaster.Core.Models
{
    public sealed class CartPad : INotifyPropertyChanged
    {
        private string _label;
        private string _filePath = string.Empty;
        private bool _isPlaying;
        private string _colorHex;
        private string _hotkey = string.Empty;
        private bool _loopEnabled;
        private TimeSpan _remainingTime = TimeSpan.Zero;
        private TimeSpan _duration = TimeSpan.Zero;

        public CartPad(int id, string label, string colorHex)
        {
            if (id < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(id));
            }

            Id = id;
            _label = string.IsNullOrWhiteSpace(label) ? $"Cart {id + 1}" : label;
            _colorHex = string.IsNullOrWhiteSpace(colorHex) ? "#FF151C29" : colorHex;
        }

        public int Id { get; }

        public string Label
        {
            get => _label;
            set => SetProperty(ref _label, string.IsNullOrWhiteSpace(value) ? $"Cart {Id + 1}" : value);
        }

        public string FilePath
        {
            get => _filePath;
            set
            {
                if (SetProperty(ref _filePath, value ?? string.Empty))
                {
                    OnPropertyChanged(nameof(HasAudio));
                    OnPropertyChanged(nameof(IsPlayable));
                    OnPropertyChanged(nameof(StatusMessage));
                    OnPropertyChanged(nameof(EmphasisOpacity));
                }
            }
        }

        public bool IsPlaying
        {
            get => _isPlaying;
            set
            {
                if (SetProperty(ref _isPlaying, value))
                {
                    OnPropertyChanged(nameof(RemainingTimeDisplay));
                }
            }
        }

        public string ColorHex
        {
            get => _colorHex;
            set => SetProperty(ref _colorHex, string.IsNullOrWhiteSpace(value) ? "#FF151C29" : value);
        }

        public string Hotkey
        {
            get => _hotkey;
            set => SetProperty(ref _hotkey, value ?? string.Empty);
        }

        public bool LoopEnabled
        {
            get => _loopEnabled;
            set => SetProperty(ref _loopEnabled, value);
        }

        public TimeSpan RemainingTime
        {
            get => _remainingTime;
            set
            {
                if (SetProperty(ref _remainingTime, value))
                {
                    OnPropertyChanged(nameof(RemainingTimeDisplay));
                }
            }
        }

        public TimeSpan Duration
        {
            get => _duration;
            set
            {
                if (SetProperty(ref _duration, value))
                {
                    OnPropertyChanged(nameof(RemainingTimeDisplay));
                }
            }
        }

        public string RemainingTimeDisplay => IsPlaying && Duration.TotalSeconds > 0
            ? $"{(int)RemainingTime.TotalSeconds}s"
            : string.Empty;

        public bool HasAudio => !string.IsNullOrWhiteSpace(FilePath);
        public bool IsPlayable => HasAudio && File.Exists(FilePath);
        public double EmphasisOpacity => IsPlayable ? 1.0 : 0.65;

        public string StatusMessage
        {
            get
            {
                if (!HasAudio)
                {
                    return "Assign an audio file for this cart.";
                }

                return IsPlayable
                    ? $"Ready: {Path.GetFileName(FilePath)}"
                    : "File missing. Update the path.";
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private bool SetProperty<T>(ref T storage, T value, [CallerMemberName] string? propertyName = null)
        {
            if (Equals(storage, value))
            {
                return false;
            }

            storage = value;
            OnPropertyChanged(propertyName);
            return true;
        }
    }
}
