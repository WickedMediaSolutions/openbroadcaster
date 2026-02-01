using Avalonia.Controls;
using System;
using Avalonia.Interactivity;
using OpenBroadcaster.Core.Services;
using OpenBroadcaster.Core.Models;

namespace OpenBroadcaster.Avalonia.Views
{
    public partial class SettingsWindow : Window
    {
        public SettingsWindow()
        {
            InitializeComponent();
            var btnSave = this.FindControl<Button>("BtnSave");
            var btnCancel = this.FindControl<Button>("BtnCancel");
            btnSave.Click += BtnSave_Click;
            btnCancel.Click += BtnCancel_Click;
        }

        private void BtnCancel_Click(object? sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void BtnSave_Click(object? sender, RoutedEventArgs e)
        {
            try
            {
                if (this.DataContext is OpenBroadcaster.Avalonia.ViewModels.SettingsViewModel svm)
                {
                    svm.Save();
                }
                else if (this.DataContext is AppSettings settings)
                {
                    var store = new AppSettingsStore();
                    store.Save(settings);
                }
            }
            catch { }
            this.Close();
        }
    }
}
