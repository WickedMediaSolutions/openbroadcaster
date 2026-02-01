using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Interactivity;

namespace OpenBroadcaster
{
    public static class UiServices
    {
        public static Window? MainWindow { get; set; }

        public enum MessageBoxButtons
        {
            Ok,
            YesNo
        }

        public enum MessageBoxResult
        {
            None,
            Ok,
            Yes,
            No
        }

        public static Task ShowInfoAsync(string title, string message)
        {
            return ShowMessageAsync(title, message, MessageBoxButtons.Ok);
        }

        public static Task ShowWarningAsync(string title, string message)
        {
            return ShowMessageAsync(title, message, MessageBoxButtons.Ok);
        }

        public static Task ShowErrorAsync(string title, string message)
        {
            return ShowMessageAsync(title, message, MessageBoxButtons.Ok);
        }

        public static async Task<bool> ShowConfirmAsync(string title, string message)
        {
            var result = await ShowMessageAsync(title, message, MessageBoxButtons.YesNo);
            return result == MessageBoxResult.Yes;
        }

        private static async Task<MessageBoxResult> ShowMessageAsync(string title, string message, MessageBoxButtons buttons)
        {
            if (MainWindow == null)
            {
                return MessageBoxResult.None;
            }

            var result = MessageBoxResult.None;
            var window = new Window
            {
                Title = title,
                Width = 420,
                Height = 220,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                CanResize = false
            };

            var textBlock = new TextBlock
            {
                Text = message,
                TextWrapping = TextWrapping.Wrap
            };

            var buttonPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Spacing = 8
            };

            void AddButton(string content, MessageBoxResult buttonResult)
            {
                var button = new Button { Content = content, MinWidth = 80 };
                button.Click += (_, _) =>
                {
                    result = buttonResult;
                    window.Close();
                };
                buttonPanel.Children.Add(button);
            }

            if (buttons == MessageBoxButtons.YesNo)
            {
                AddButton("Yes", MessageBoxResult.Yes);
                AddButton("No", MessageBoxResult.No);
            }
            else
            {
                AddButton("OK", MessageBoxResult.Ok);
            }

            var content = new StackPanel
            {
                Margin = new Thickness(16),
                Spacing = 12
            };
            content.Children.Add(textBlock);
            content.Children.Add(buttonPanel);
            window.Content = content;

            await window.ShowDialog(MainWindow);
            return result;
        }

        public static async Task<string[]?> OpenFilesAsync(string title, string filter, bool allowMultiple)
        {
            if (MainWindow == null)
            {
                return null;
            }

            var storageProvider = MainWindow.StorageProvider;
            if (storageProvider == null)
            {
                return null;
            }

            var fileTypes = new List<Avalonia.Platform.Storage.FilePickerFileType>();
            if (!string.IsNullOrWhiteSpace(filter))
            {
                var extensions = filter.Split(';')
                    .Select(ext => ext.Trim().TrimStart('*').TrimStart('.'))
                    .Where(ext => !string.IsNullOrWhiteSpace(ext))
                    .ToList();

                fileTypes.Add(new Avalonia.Platform.Storage.FilePickerFileType("Audio Files")
                {
                    Patterns = extensions.Select(ext => $"*.{ext}").ToList()
                });
            }

            var options = new Avalonia.Platform.Storage.FilePickerOpenOptions
            {
                Title = title,
                AllowMultiple = allowMultiple,
                FileTypeFilter = fileTypes.Count > 0 ? fileTypes : null
            };

            var result = await storageProvider.OpenFilePickerAsync(options);
            return result?.Select(f => f.Path.LocalPath).ToArray();
        }

        public static async Task<string?> OpenFolderAsync(string title)
        {
            if (MainWindow == null)
            {
                return null;
            }

            var storageProvider = MainWindow.StorageProvider;
            if (storageProvider == null)
            {
                return null;
            }

            var options = new Avalonia.Platform.Storage.FolderPickerOpenOptions
            {
                Title = title,
                AllowMultiple = false
            };

            var result = await storageProvider.OpenFolderPickerAsync(options);
            return result?.FirstOrDefault()?.Path.LocalPath;
        }
    }
}
