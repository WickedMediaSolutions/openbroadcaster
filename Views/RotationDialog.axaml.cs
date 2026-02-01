using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using OpenBroadcaster.Core.Automation;
using OpenBroadcaster.ViewModels;

namespace OpenBroadcaster.Views
{
    public sealed partial class RotationDialog : Window
    {
        private readonly RotationDialogViewModel _viewModel;

        public RotationDialog(SimpleRotation rotation, List<string> categoryOptions, IEnumerable<string>? existingNames = null)
        {
            InitializeComponent();
            _viewModel = new RotationDialogViewModel(rotation, categoryOptions, existingNames ?? Array.Empty<string>());
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

    internal sealed class RotationDialogViewModel : ViewModelBase
    {
        private readonly SimpleRotation _rotation;
        private readonly HashSet<string> _existingNames;
        private string _name;
        private bool _isActive;
        private string _errorMessage = string.Empty;

        public RotationDialogViewModel(SimpleRotation rotation, IReadOnlyList<string> categoryOptions, IEnumerable<string> existingNames)
        {
            _rotation = rotation ?? throw new ArgumentNullException(nameof(rotation));
            _existingNames = new HashSet<string>(existingNames ?? Array.Empty<string>(), StringComparer.OrdinalIgnoreCase);
            _name = rotation.Name;
            _isActive = rotation.IsActive;

            CategoryOptions = new ObservableCollection<RotationCategoryOption>(
                categoryOptions.Select(name => new RotationCategoryOption(name, rotation.CategoryNames.Contains(name))));
        }

        public string Name
        {
            get => _name;
            set => SetProperty(ref _name, value ?? string.Empty);
        }

        public bool IsActive
        {
            get => _isActive;
            set => SetProperty(ref _isActive, value);
        }

        public ObservableCollection<RotationCategoryOption> CategoryOptions { get; }

        public string ErrorMessage
        {
            get => _errorMessage;
            set => SetProperty(ref _errorMessage, value ?? string.Empty);
        }

        public bool TryApply()
        {
            var trimmed = Name.Trim();
            if (string.IsNullOrWhiteSpace(trimmed))
            {
                ErrorMessage = "Rotation name is required.";
                return false;
            }

            if (_existingNames.Contains(trimmed))
            {
                ErrorMessage = "A rotation with that name already exists.";
                return false;
            }

            _rotation.Name = trimmed;
            _rotation.IsActive = IsActive;
            _rotation.CategoryNames = CategoryOptions.Where(c => c.IsSelected).Select(c => c.Name).ToList();
            ErrorMessage = string.Empty;
            return true;
        }
    }

    internal sealed class RotationCategoryOption : ViewModelBase
    {
        private bool _isSelected;

        public RotationCategoryOption(string name, bool isSelected)
        {
            Name = name;
            _isSelected = isSelected;
        }

        public string Name { get; }

        public bool IsSelected
        {
            get => _isSelected;
            set => SetProperty(ref _isSelected, value);
        }
    }
}
