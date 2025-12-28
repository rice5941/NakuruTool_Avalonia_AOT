using Avalonia.Data.Converters;
using NakuruTool_Avalonia_AOT.Features.MapList.Models;
using NakuruTool_Avalonia_AOT.Features.Translate;
using System;
using System.Globalization;

namespace NakuruTool_Avalonia_AOT.Features.Shared.Converters
{
    /// <summary>
    /// プリセット名をnullの場合は翻訳された「(なし)」に変換するコンバーター
    /// </summary>
    public class PresetNameConverter : IValueConverter
    {
        public static readonly PresetNameConverter Instance = new();

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            // valueはFilterPreset?オブジェクト
            if (value is FilterPreset preset && !string.IsNullOrEmpty(preset.Name))
            {
                return preset.Name;
            }

            // nullまたは空文字の場合は「(なし)」を返す
            return LanguageService.Instance.GetString("MapFilter.PresetNone");
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
