using System;
using System.Collections.Generic;
using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Interactivity;
using OpenBroadcaster.Core.Automation;
using System.Linq;
using OpenBroadcaster.Avalonia.ViewModels;

namespace OpenBroadcaster.Avalonia.Views
{
    public partial class SchedulerDialog : Window
    {
        private readonly SchedulerDialogViewModel _viewModel;
        private readonly SimpleSchedulerEntry _entry;

        public SchedulerDialog(SimpleSchedulerEntry entry, List<SimpleRotation> rotations)
        {
            InitializeComponent();
            _entry = entry ?? throw new ArgumentNullException(nameof(entry));
            _viewModel = new SchedulerDialogViewModel(entry, rotations);
            DataContext = _viewModel;
        }

        private void OnSaveClick(object? sender, RoutedEventArgs e)
        {
            if (!_viewModel.TryCommit(out var error))
            {
                var msg = new SimpleMessageWindow("Schedule", error);
                msg.ShowDialog(this);
                return;
            }

            // apply values back to the original entry
            _viewModel.ApplyTo(_entry);

            Close(true);
        }

        private void OnCancelClick(object? sender, RoutedEventArgs e)
        {
            Close(false);
        }
    }

    internal sealed class SchedulerDialogViewModel : INotifyPropertyChanged
    {
        private DayOfWeek _day;
        private TimeSpan _start;
        private TimeSpan _end;
        private bool _enabled;
        private List<SimpleRotation> _rotations;
        private SimpleRotation? _selectedRotation;

        public SchedulerDialogViewModel(SimpleSchedulerEntry entry, List<SimpleRotation> rotations)
        {
            _rotations = rotations ?? new List<SimpleRotation>();
            Day = entry.Day ?? DayOfWeek.Monday;
            StartTime = entry.StartTime;
            EndTime = entry.EndTime;
            Enabled = entry.Enabled;
            SelectedRotation = rotations.FirstOrDefault(r => r.Id == entry.RotationId) ?? rotations.FirstOrDefault();
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        public DayOfWeek Day
        {
            get => _day;
            set { if (_day != value) { _day = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Day))); } }
        }

        public TimeSpan StartTime
        {
            get => _start;
            set { if (_start != value) { _start = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(StartTime))); } }
        }

        public TimeSpan EndTime
        {
            get => _end;
            set { if (_end != value) { _end = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(EndTime))); } }
        }

        public bool Enabled
        {
            get => _enabled;
            set { if (_enabled != value) { _enabled = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Enabled))); } }
        }

        public IReadOnlyList<SimpleRotation> Rotations => _rotations;

        public SimpleRotation? SelectedRotation
        {
            get => _selectedRotation;
            set { if (!ReferenceEquals(_selectedRotation, value)) { _selectedRotation = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SelectedRotation))); } }
        }

        public string StartTimeString
        {
            get => StartTime.ToString(@"hh\:mm");
            set
            {
                if (TimeSpan.TryParse(value, out var t))
                {
                    StartTime = t;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(StartTimeString)));
                }
            }
        }

        public string EndTimeString
        {
            get => EndTime.ToString(@"hh\:mm");
            set
            {
                if (TimeSpan.TryParse(value, out var t))
                {
                    EndTime = t;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(EndTimeString)));
                }
            }
        }

        public bool TryCommit(out string error)
        {
            error = string.Empty;
            if (SelectedRotation == null)
            {
                error = "Choose a rotation.";
                return false;
            }

            if (EndTime <= StartTime)
            {
                error = "End time must be after start time.";
                return false;
            }

            return true;
        }

        public void ApplyTo(SimpleSchedulerEntry entry)
        {
            entry.Day = Day;
            entry.StartTime = StartTime;
            entry.EndTime = EndTime;
            entry.Enabled = Enabled;
            if (SelectedRotation != null)
            {
                entry.RotationId = SelectedRotation.Id;
                entry.RotationName = SelectedRotation.Name ?? string.Empty;
            }
        }
    }
}
