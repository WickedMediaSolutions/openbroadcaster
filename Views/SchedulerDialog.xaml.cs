using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using OpenBroadcaster.Core.Automation;
using MessageBox = System.Windows.MessageBox;

namespace OpenBroadcaster.Views
{
    public partial class SchedulerDialog : Window
    {
        private readonly SchedulerDialogViewModel _viewModel;

        public SimpleSchedulerEntry Entry { get; }
        public IReadOnlyList<SimpleRotation> RotationOptions { get; }

        public SchedulerDialog(SimpleSchedulerEntry entry, List<SimpleRotation> rotations)
        {
            InitializeComponent();
            Entry = entry ?? throw new ArgumentNullException(nameof(entry));
            RotationOptions = rotations ?? new List<SimpleRotation>();
            _viewModel = new SchedulerDialogViewModel(entry, RotationOptions);
            DataContext = _viewModel;
        }

        private void OnSaveClick(object sender, RoutedEventArgs e)
        {
            if (!_viewModel.TryCommit(Entry, out var error))
            {
                MessageBox.Show(this, error, "Schedule", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            DialogResult = true;
            Close();
        }

        private void OnCancelClick(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }

    internal sealed class SchedulerDialogViewModel : INotifyPropertyChanged
    {
        private string _startTime;
        private bool _enabled;
        private SimpleRotation? _selectedRotation;
        private DayOfWeek? _selectedDay;

        public SchedulerDialogViewModel(SimpleSchedulerEntry entry, IReadOnlyList<SimpleRotation> rotations)
        {
            _startTime = entry?.StartTimeString ?? "00:00";
            _enabled = entry?.Enabled ?? true;
            RotationOptions = rotations ?? Array.Empty<SimpleRotation>();
            _selectedRotation = RotationOptions.FirstOrDefault(r => r.Name == entry?.RotationName) ?? RotationOptions.FirstOrDefault();
            _selectedDay = entry?.Day;
            DayOptions = BuildDayOptions();
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        public IReadOnlyList<SimpleRotation> RotationOptions { get; }

        public IReadOnlyList<DayOption> DayOptions { get; }

        public SimpleRotation? SelectedRotation
        {
            get => _selectedRotation;
            set
            {
                if (!ReferenceEquals(_selectedRotation, value))
                {
                    _selectedRotation = value;
                    OnPropertyChanged(nameof(SelectedRotation));
                }
            }
        }

        public DayOfWeek? SelectedDay
        {
            get => _selectedDay;
            set
            {
                if (_selectedDay != value)
                {
                    _selectedDay = value;
                    OnPropertyChanged(nameof(SelectedDay));
                }
            }
        }

        public string StartTimeText
        {
            get => _startTime;
            set
            {
                if (!string.Equals(_startTime, value, StringComparison.Ordinal))
                {
                    _startTime = value ?? string.Empty;
                    OnPropertyChanged(nameof(StartTimeText));
                }
            }
        }

        public bool Enabled
        {
            get => _enabled;
            set
            {
                if (_enabled != value)
                {
                    _enabled = value;
                    OnPropertyChanged(nameof(Enabled));
                }
            }
        }

        public bool TryCommit(SimpleSchedulerEntry entry, out string error)
        {
            error = string.Empty;
            if (entry == null)
            {
                error = "Schedule entry is missing.";
                return false;
            }

            if (SelectedRotation == null)
            {
                error = "Pick a rotation to run.";
                return false;
            }

            if (!TryParseTime(StartTimeText, out var start))
            {
                error = "Start time must be HH:mm (24-hour).";
                return false;
            }

            entry.RotationId = SelectedRotation.Id;
            entry.RotationName = SelectedRotation.Name ?? string.Empty;
            entry.StartTime = start;
            // Schedules are start-only: each entry is active from its start
            // time through the rest of the day. The runtime scheduler will
            // pick the latest started entry at or before "now".
            entry.EndTime = TimeSpan.FromHours(24);
            entry.Enabled = Enabled;
            entry.Day = SelectedDay;

            return true;
        }

        private static bool TryParseTime(string value, out TimeSpan time)
        {
            return TimeSpan.TryParseExact(value?.Trim(), "hh\\:mm", null, out time) || TimeSpan.TryParse(value, out time);
        }

        private static List<DayOption> BuildDayOptions()
        {
            var options = new List<DayOption> { new DayOption("Every day", null) };
            options.AddRange(((DayOfWeek[])Enum.GetValues(typeof(DayOfWeek)))
                .Select(day => new DayOption(day.ToString(), day)));
            return options;
        }

        private void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    internal sealed record DayOption(string Label, DayOfWeek? Value);
}
