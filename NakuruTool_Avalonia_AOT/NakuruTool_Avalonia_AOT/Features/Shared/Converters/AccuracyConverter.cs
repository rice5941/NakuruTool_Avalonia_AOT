using Avalonia.Data.Converters;
using System;
using System.Globalization;

namespace NakuruTool_Avalonia_AOT.Features.Shared.Converters
{
    /// <summary>
    /// 精度値を文字列に変換するコンバーター（0の場合は"-"を返す）
    /// </summary>
    public class AccuracyConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is double doubleValue)
            {
                if (doubleValue == 0.0)
                {
                    return "-";
                }
                return $"{doubleValue:F2}%";
            }

            return "-";
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
