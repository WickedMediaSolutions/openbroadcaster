using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using OpenBroadcaster.Core.Automation;
using OpenBroadcaster.Avalonia.ViewModels;

namespace OpenBroadcaster.Avalonia.Views
{
    public partial class RotationDialog : Window
    {
        private readonly RotationDialogViewModel _viewModel;
        private readonly SimpleRotation _rotation;

        public RotationDialog(SimpleRotation rotation, List<string> categoryOptions, IEnumerable<string>? existingNames = null)
        {
            InitializeComponent();
            _rotation = rotation ?? throw new ArgumentNullException(nameof(rotation));
            _viewModel = new RotationDialogViewModel(rotation, categoryOptions, existingNames ?? Array.Empty<string>());
            DataContext = _viewModel;
        }

        private void OnSaveClick(object? sender, RoutedEventArgs e)
        {
            try
            {
                if (!_viewModel.TryCommit(out var error))
                {
                    var msg = new SimpleMessageWindow("Rotation", error);
                    msg.ShowDialog(this);
                    return;
                }

                // apply values back to the original rotation object
                _viewModel.ApplyTo(_rotation);

                Close(true);
            }
            catch (Exception ex)
            {
                var msg = new SimpleMessageWindow("Rotation", $"Unable to save rotation: {ex.Message}");
                msg.ShowDialog(this);
            }
        }

        private void OnCancelClick(object? sender, RoutedEventArgs e)
        {
            Close(false);
        }
    }

    internal sealed class RotationDialogViewModel : INotifyPropertyChanged
    {
        private string _name;
        private bool _enabled;
        private bool _isActive;
        private readonly HashSet<string> _existingNames;

        public RotationDialogViewModel(SimpleRotation rotation, IReadOnlyList<string> categoryOptions, IEnumerable<string> existingNames)
        {
            _name = rotation?.Name ?? string.Empty;
            _enabled = rotation?.Enabled ?? true;
            _isActive = rotation?.IsActive ?? false;
            CategoryOptions = categoryOptions ?? Array.Empty<string>();
            _existingNames = new HashSet<string>((existingNames ?? Array.Empty<string>()).Where(n => !string.IsNullOrWhiteSpace(n)), StringComparer.OrdinalIgnoreCase);
            _existingNames.Remove(_name);

            var slots = (rotation?.Slots?.Count > 0)
                ? rotation.Slots.Select(slot => new SimpleRotationSlot(slot.CategoryName ?? string.Empty) { CategoryId = slot.CategoryId, Weight = slot.Weight }).ToList()
                : BuildLegacySlots(rotation);

            Slots = new ObservableCollection<SimpleRotationSlot>(slots.Any()
                ? slots
                : new List<SimpleRotationSlot> { new SimpleRotationSlot(CategoryOptions.FirstOrDefault() ?? string.Empty) });

            AddSlotCommand = new RelayCommand(_ => AddSlot());
            RemoveSlotCommand = new RelayCommand(slot => RemoveSlot(slot as SimpleRotationSlot), _ => Slots.Count > 1);
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        public string Name
        {
            get => _name;
            set
            {
                if (!string.Equals(_name, value, StringComparison.Ordinal))
                {
                    _name = value ?? string.Empty;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Name)));
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
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Enabled)));
                }
            }
        }

        public bool IsActive
        {
            get => _isActive;
            set
            {
                if (_isActive != value)
                {
                    _isActive = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsActive)));
                }
            }
        }

        public ObservableCollection<SimpleRotationSlot> Slots { get; }

        public IReadOnlyList<string> CategoryOptions { get; }

        public RelayCommand AddSlotCommand { get; }

        public RelayCommand RemoveSlotCommand { get; }

        public bool TryCommit(out string error)
        {
            error = string.Empty;
            if (string.IsNullOrWhiteSpace(Name))
            {
                error = "Give this rotation a name.";
                return false;
            }

            if (_existingNames.Contains(Name.Trim()))
            {
                error = "Rotation name must be unique.";
                return false;
            }

            var categories = Slots
                .Select(slot => slot.CategoryName?.Trim() ?? string.Empty)
                .Where(c => !string.IsNullOrWhiteSpace(c))
                .ToList();

            if (categories.Count == 0)
            {
                error = "Add at least one category slot.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        public void ApplyTo(SimpleRotation rotation)
        {
            rotation.Name = Name.Trim();
            rotation.Enabled = Enabled;
            rotation.IsActive = IsActive;
            rotation.CategoryIds = new List<string>();
            rotation.CategoryNames = Slots.Select(s => s.CategoryName?.Trim() ?? string.Empty).Where(s => !string.IsNullOrWhiteSpace(s)).ToList();
            rotation.Slots = Slots.Select(s => new OpenBroadcaster.Core.Automation.SimpleRotationSlot { CategoryId = s.CategoryId, CategoryName = s.CategoryName, Weight = s.Weight }).ToList();
        }

        private void AddSlot()
        {
            Slots.Add(new SimpleRotationSlot(CategoryOptions.FirstOrDefault() ?? string.Empty));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Slots)));
        }

        private void RemoveSlot(SimpleRotationSlot? slot)
        {
            if (slot == null) return;
            if (Slots.Count <= 1) return;
            Slots.Remove(slot);
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Slots)));
        }

        private static List<SimpleRotationSlot> BuildLegacySlots(SimpleRotation? rotation)
        {
            var slots = new List<SimpleRotationSlot>();
            if (rotation == null) return slots;

            if (rotation.CategoryIds != null && rotation.CategoryIds.Count > 0)
            {
                foreach (var id in rotation.CategoryIds)
                {
                    slots.Add(new SimpleRotationSlot(string.Empty)
                    {
                        CategoryId = Guid.TryParse(id, out var g) ? g : Guid.Empty,
                        CategoryName = string.Empty,
                        Weight = 1
                    });
                }
            }
            else if (rotation.CategoryNames != null && rotation.CategoryNames.Count > 0)
            {
                foreach (var name in rotation.CategoryNames)
                {
                    slots.Add(new SimpleRotationSlot(name) { CategoryName = name, CategoryId = Guid.Empty, Weight = 1 });
                }
            }

            return slots;
        }
    }

    // Lightweight slot viewmodel used inside the dialog
    internal sealed class SimpleRotationSlot : INotifyPropertyChanged
    {
        private string _categoryName;
        public Guid CategoryId { get; set; } = Guid.Empty;
        public int Weight { get; set; } = 1;

        public SimpleRotationSlot(string categoryName)
        {
            _categoryName = categoryName ?? string.Empty;
        }

        public string CategoryName
        {
            get => _categoryName;
            set
            {
                if (!string.Equals(_categoryName, value, StringComparison.Ordinal))
                {
                    _categoryName = value ?? string.Empty;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CategoryName)));
                }
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
    }
}
