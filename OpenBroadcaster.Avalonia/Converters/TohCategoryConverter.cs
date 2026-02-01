using System;
using Avalonia.Data.Converters;
using OpenBroadcaster.Avalonia.ViewModels;

namespace OpenBroadcaster.Avalonia.Converters
{
    public class TohCategoryConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
        {
            if (value is Guid categoryId)
            {
                return TohCategoryOptionsProvider.Find(categoryId);
            }
            return null;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
        {
            // value should be a TohCategoryOption which is SettingsViewModel.TohCategoryOption
            if (value != null && value.GetType().Name == "TohCategoryOption")
            {
                var categoryIdProp = value.GetType().GetProperty("CategoryId");
                if (categoryIdProp?.GetValue(value) is Guid categoryId)
                {
                    return categoryId;
                }
            }
            return Guid.Empty;
        }
    }
}
