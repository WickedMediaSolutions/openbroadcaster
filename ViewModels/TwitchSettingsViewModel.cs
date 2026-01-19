using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using OpenBroadcaster.Core.Models;

namespace OpenBroadcaster.ViewModels
{
    public sealed class TwitchSettingsViewModel : INotifyPropertyChanged
    {
        private readonly TwitchSettings _settings;

        public TwitchSettingsViewModel(TwitchSettings settings)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            SaveCommand = new RelayCommand(_ => { }, _ => IsValid);
        }

        public string UserName
        {
            get => _settings.UserName;
            set
            {
                if (_settings.UserName != value)
                {
                    _settings.UserName = value ?? string.Empty;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(IsValid));
                }
            }
        }

        public string OAuthToken
        {
            get => _settings.OAuthToken;
            set
            {
                if (_settings.OAuthToken != value)
                {
                    _settings.OAuthToken = value ?? string.Empty;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(IsValid));
                }
            }
        }

        public string Channel
        {
            get => _settings.Channel;
            set
            {
                if (_settings.Channel != value)
                {
                    _settings.Channel = value ?? string.Empty;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(IsValid));
                }
            }
        }

        public string PointsName
        {
            get => _settings.PointsName;
            set
            {
                if (_settings.PointsName != value)
                {
                    _settings.PointsName = string.IsNullOrWhiteSpace(value) ? "Sheckles" : value;
                    OnPropertyChanged();
                }
            }
        }

        public int RequestCost
        {
            get => _settings.RequestCost;
            set
            {
                if (_settings.RequestCost != value)
                {
                    _settings.RequestCost = Math.Max(0, value);
                    OnPropertyChanged();
                }
            }
        }

        public int PlayNextCost
        {
            get => _settings.PlayNextCost;
            set
            {
                if (_settings.PlayNextCost != value)
                {
                    _settings.PlayNextCost = Math.Max(0, value);
                    OnPropertyChanged();
                }
            }
        }

        public int SearchResultsLimit
        {
            get => _settings.SearchResultsLimit;
            set
            {
                var normalized = Math.Max(1, value);
                if (_settings.SearchResultsLimit != normalized)
                {
                    _settings.SearchResultsLimit = normalized;
                    OnPropertyChanged();
                }
            }
        }

        public int RequestCooldownSeconds
        {
            get => _settings.RequestCooldownSeconds;
            set
            {
                var normalized = Math.Max(0, value);
                if (_settings.RequestCooldownSeconds != normalized)
                {
                    _settings.RequestCooldownSeconds = normalized;
                    OnPropertyChanged();
                }
            }
        }

        public int ChatMessageAwardPoints
        {
            get => _settings.ChatMessageAwardPoints;
            set
            {
                var normalized = Math.Max(0, value);
                if (_settings.ChatMessageAwardPoints != normalized)
                {
                    _settings.ChatMessageAwardPoints = normalized;
                    OnPropertyChanged();
                }
            }
        }

        public int IdleAwardPoints
        {
            get => _settings.IdleAwardPoints;
            set
            {
                var normalized = Math.Max(0, value);
                if (_settings.IdleAwardPoints != normalized)
                {
                    _settings.IdleAwardPoints = normalized;
                    OnPropertyChanged();
                }
            }
        }

        public int IdleAwardIntervalMinutes
        {
            get => _settings.IdleAwardIntervalMinutes;
            set
            {
                var normalized = Math.Max(1, value);
                if (_settings.IdleAwardIntervalMinutes != normalized)
                {
                    _settings.IdleAwardIntervalMinutes = normalized;
                    OnPropertyChanged();
                }
            }
        }

        public bool IsValid =>
            !string.IsNullOrWhiteSpace(UserName) &&
            !string.IsNullOrWhiteSpace(OAuthToken) &&
            !string.IsNullOrWhiteSpace(Channel);

        public ICommand SaveCommand { get; }

        public TwitchSettings ToSettings() => _settings.Clone();

        public event PropertyChangedEventHandler? PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
