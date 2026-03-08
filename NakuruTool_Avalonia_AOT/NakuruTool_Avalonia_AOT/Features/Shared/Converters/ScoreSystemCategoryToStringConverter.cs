using System;
using System.Globalization;
using Avalonia.Data.Converters;
using NakuruTool_Avalonia_AOT.Features.OsuDatabase;
using NakuruTool_Avalonia_AOT.Features.Translate;

namespace NakuruTool_Avalonia_AOT.Features.Shared.Converters;

/// <summary>
/// ScoreSystemCategoryを多言語対応した文字列に変換するコンバータ
/// </summary>
public class ScoreSystemCategoryToStringConverter : IValueConverter
{
    public static readonly ScoreSystemCategoryToStringConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is ScoreSystemCategory category)
        {
            var key = $"MapList.ScoreSystem.{category}";
            return LanguageService.Instance.GetString(key);
        }
        return value?.ToString() ?? string.Empty;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
