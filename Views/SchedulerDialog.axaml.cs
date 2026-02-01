using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using OpenBroadcaster.Core.Automation;
using OpenBroadcaster.ViewModels;

namespace OpenBroadcaster.Views
{
    public sealed partial class SchedulerDialog : Window
    {
        private readonly SchedulerDialogViewModel _viewModel;

        public SchedulerDialog(SimpleSchedulerEntry entry, List<SimpleRotation> rotations)
        {
            InitializeComponent();
            _viewModel = new SchedulerDialogViewModel(entry, rotations);
            DataContext = _viewModel;
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        private void OnSave(object? sender, RoutedEventArgs e)
        {
            if (_viewModel.TryApply())
            {
                Close(true);
            }
        }

        private void OnCancel(object? sender, RoutedEventArgs e)
        {
            Close(false);
        }
    }

    internal sealed class SchedulerDialogViewModel : ViewModelBase
    {
        private readonly SimpleSchedulerEntry _entry;
        private RotationOption? _selectedRotation;
        private string _startTime;
        private string _endTime;
        private bool _enabled;
        private string _errorMessage = string.Empty;

        public SchedulerDialogViewModel(SimpleSchedulerEntry entry, IReadOnlyList<SimpleRotation> rotations)
        {
            _entry = entry ?? throw new ArgumentNullException(nameof(entry));
            Rotations = new ObservableCollection<RotationOption>(rotations.Select(r => new RotationOption(r)));
            _selectedRotation = Rotations.FirstOrDefault(r => r.Id == entry.RotationId) ?? Rotations.FirstOrDefault();
            _startTime = entry.StartTime.ToString("hh\\:mm");
            _endTime = entry.EndTime.ToString("hh\\:mm");
            _enabled = entry.Enabled;
        }

        public ObservableCollection<RotationOption> Rotations { get; }

        public RotationOption? SelectedRotation
        {
            get => _selectedRotation;
            set => SetProperty(ref _selectedRotation, value);
        }

        public string StartTime
        {
            get => _startTime;
            set => SetProperty(ref _startTime, value ?? string.Empty);
        }

        public string EndTime
        {
            get => _endTime;
            set => SetProperty(ref _endTime, value ?? string.Empty);
        }

        public bool Enabled
        {
            get => _enabled;
            set => SetProperty(ref _enabled, value);
        }

        public string ErrorMessage
        {
            get => _errorMessage;
            set => SetProperty(ref _errorMessage, value ?? string.Empty);
        }

        public bool TryApply()
        {
            if (SelectedRotation == null)
            {
                ErrorMessage = "Select a rotation.";
                return false;
            }

            if (!TimeSpan.TryParseExact(StartTime.Trim(), "hh\\:mm", CultureInfo.InvariantCulture, out var start))
            {
                ErrorMessage = "Invalid start time.";
                return false;
            }

            if (!TimeSpan.TryParseExact(EndTime.Trim(), "hh\\:mm", CultureInfo.InvariantCulture, out var end))
            {
                ErrorMessage = "Invalid end time.";
                return false;
            }

            _entry.RotationId = SelectedRotation.Id;
            _entry.RotationName = SelectedRotation.Name;
            _entry.StartTime = start;
            _entry.EndTime = end;
            _entry.Enabled = Enabled;
            ErrorMessage = string.Empty;
            return true;
        }
    }

    internal sealed class RotationOption
    {
        public RotationOption(SimpleRotation rotation)
        {
            Id = rotation.Id;
            Name = rotation.Name;
        }

        public Guid Id { get; }
        public string Name { get; }

        public override string ToString() => Name;
    }
}
