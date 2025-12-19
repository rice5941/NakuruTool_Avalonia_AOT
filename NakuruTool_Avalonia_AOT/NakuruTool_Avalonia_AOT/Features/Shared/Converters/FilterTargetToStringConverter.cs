using System;
using System.Globalization;
using Avalonia.Data.Converters;
using NakuruTool_Avalonia_AOT.Features.MapList.Models;
using NakuruTool_Avalonia_AOT.Features.Translate;

namespace NakuruTool_Avalonia_AOT.Features.Shared.Converters;

/// <summary>
/// FilterTargetを多言語対応した文字列に変換するコンバータ
/// </summary>
public class FilterTargetToStringConverter : IValueConverter
{
    public static readonly FilterTargetToStringConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is FilterTarget target)
        {
            var key = $"MapFilter.Target.{target}";
            return LanguageService.Instance.GetString(key);
        }
        return value?.ToString() ?? string.Empty;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
