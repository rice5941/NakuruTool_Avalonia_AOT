using Avalonia.Data.Converters;
using NakuruTool_Avalonia_AOT.Features.Translate;
using System;
using System.Globalization;

namespace NakuruTool_Avalonia_AOT.Features.Shared.Converters;

public class LanguageCodeToNativeNameConverter : IValueConverter
{
    public static readonly LanguageCodeToNativeNameConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is string languageCode
            ? LanguageService.Instance.GetLanguageDisplayName(languageCode)
            : LanguageService.Instance.GetLanguageDisplayName(null);
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}