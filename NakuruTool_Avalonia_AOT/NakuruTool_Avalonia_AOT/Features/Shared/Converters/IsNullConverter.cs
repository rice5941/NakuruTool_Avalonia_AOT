using Avalonia.Data.Converters;
using System;
using System.Globalization;

namespace NakuruTool_Avalonia_AOT.Features.Shared.Converters
{
    /// <summary>
    /// 値が null かどうかを bool で返すコンバーター
    /// </summary>
    public class IsNullConverter : IValueConverter
    {
        public static readonly IsNullConverter Instance = new();

        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
            => value is null;

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    /// <summary>
    /// 値が null でないかどうかを bool で返すコンバーター
    /// </summary>
    public class IsNotNullConverter : IValueConverter
    {
        public static readonly IsNotNullConverter Instance = new();

        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
            => value is not null;

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}
