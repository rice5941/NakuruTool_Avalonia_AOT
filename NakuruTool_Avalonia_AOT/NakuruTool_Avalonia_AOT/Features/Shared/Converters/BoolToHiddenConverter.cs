using Avalonia.Data.Converters;
using System;
using System.Globalization;

namespace NakuruTool_Avalonia_AOT.Features.Shared.Converters
{
    /// <summary>
    /// BooleanをOpacity値に変換するコンバータ
    /// WPFのVisibility.Hiddenを再現（レイアウトスペースは確保しつつ非表示）
    /// </summary>
    public class BoolToOpacityConverter : IValueConverter
    {
        /// <summary>
        /// Trueの時のOpacity値（デフォルト: 1.0 = 表示）
        /// </summary>
        public double TrueOpacity { get; set; } = 1.0;

        /// <summary>
        /// Falseの時のOpacity値（デフォルト: 0.0 = 非表示）
        /// </summary>
        public double FalseOpacity { get; set; } = 0.0;

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
            {
                return boolValue ? TrueOpacity : FalseOpacity;
            }

            return FalseOpacity;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// BooleanをIsHitTestVisible値に変換するコンバータ
    /// </summary>
    public class BoolToHitTestVisibleConverter : IValueConverter
    {
        /// <summary>
        /// Trueの時のIsHitTestVisible値（デフォルト: true）
        /// </summary>
        public bool TrueValue { get; set; } = true;

        /// <summary>
        /// Falseの時のIsHitTestVisible値（デフォルト: false）
        /// </summary>
        public bool FalseValue { get; set; } = false;

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
            {
                return boolValue ? TrueValue : FalseValue;
            }

            return FalseValue;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Booleanを反転させるコンバータ
    /// </summary>
    public class InverseBooleanConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
            {
                return !boolValue;
            }

            return true;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
            {
                return !boolValue;
            }

            return true;
        }
    }
}
