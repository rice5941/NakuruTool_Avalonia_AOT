using Avalonia.Data.Converters;
using System;
using System.Globalization;

namespace NakuruTool_Avalonia_AOT.Features.Shared.Converters
{
    /// <summary>
    /// Nullable DateTimeを文字列に変換するコンバーター
    /// nullの場合は空文字列を返す
    /// </summary>
    public class NullableDateTimeConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is DateTime dateTime && dateTime != DateTime.MinValue)
            {
                var format = parameter as string ?? "yyyy/MM/dd HH:mm";
                return dateTime.ToString(format, culture);
            }

            return string.Empty;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is string str && !string.IsNullOrEmpty(str))
            {
                if (DateTime.TryParse(str, culture, DateTimeStyles.None, out var dateTime))
                {
                    return dateTime;
                }
            }

            return null;
        }
    }
}
