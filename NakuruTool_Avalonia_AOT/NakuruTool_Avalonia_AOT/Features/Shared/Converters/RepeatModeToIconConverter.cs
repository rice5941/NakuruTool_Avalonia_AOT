using Avalonia.Data.Converters;
using Material.Icons;
using NakuruTool_Avalonia_AOT.Features.AudioPlayer;
using System;
using System.Globalization;

namespace NakuruTool_Avalonia_AOT.Features.Shared.Converters
{
    /// <summary>
    /// RepeatMode 値を MaterialIconKind に変換する IValueConverter 実装。
    /// NativeAOT 互換: リフレクション不使用、Source Generator 依存なし。
    /// </summary>
    public class RepeatModeToIconConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is RepeatMode mode)
            {
                return mode switch
                {
                    RepeatMode.None => MaterialIconKind.RepeatOff,
                    RepeatMode.All  => MaterialIconKind.Repeat,
                    RepeatMode.One  => MaterialIconKind.RepeatOnce,
                    _               => MaterialIconKind.RepeatOff,
                };
            }

            return MaterialIconKind.RepeatOff;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
