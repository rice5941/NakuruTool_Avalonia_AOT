using Avalonia.Data.Converters;
using System;
using System.Globalization;

namespace NakuruTool_Avalonia_AOT.Features.Shared.Converters;
/// <summary>
/// スコア値を文字列に変換するコンバーター（0の場合は"-"を返す）
/// </summary>
public class ScoreConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is int intValue)
        {
            if (intValue == 0)
            {
                return "-";
            }
            return intValue.ToString("N0", culture);
        }

        return "-";
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
