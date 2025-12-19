using Avalonia.Data.Converters;
using System;
using System.Globalization;

namespace NakuruTool_Avalonia_AOT.Features.Shared.Converters;

/// <summary>
/// string と DateTimeOffset? の間で変換を行うコンバーター
/// CalendarDatePicker用
/// </summary>
public class DateTimeOffsetConverter : IValueConverter
{
    public static readonly DateTimeOffsetConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        // string -> DateTimeOffset?
        if (value is string str && !string.IsNullOrWhiteSpace(str))
        {
            if (DateTime.TryParse(str, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
            {
                return new DateTimeOffset(DateTime.SpecifyKind(dt.Date, DateTimeKind.Local));
            }
        }

        return null;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        // DateTimeOffset? -> string
        if (value is DateTimeOffset dto)
        {
            // ローカル時間として日付のみを文字列化
            return dto.LocalDateTime.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        }

        return string.Empty;
    }
}
