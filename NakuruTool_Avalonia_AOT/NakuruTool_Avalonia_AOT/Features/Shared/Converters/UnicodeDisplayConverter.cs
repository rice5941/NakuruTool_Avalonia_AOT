using Avalonia.Data.Converters;
using NakuruTool_Avalonia_AOT.Features.ImportExport.Models;
using NakuruTool_Avalonia_AOT.Features.OsuDatabase;
using NakuruTool_Avalonia_AOT.Features.Settings;
using System;
using System.Globalization;

namespace NakuruTool_Avalonia_AOT.Features.Shared.Converters;

/// <summary>
/// PreferUnicode設定に基づいてTitle/ArtistのUnicode版またはASCII版を返すコンバーター。
/// value: Beatmap または ImportExportBeatmapItem オブジェクト全体（{Binding .}で渡す）
/// parameter: "Title" または "Artist"（表示対象の指定）
/// 
/// SettingsService.Current (internal static) から PreferUnicode を参照する。
/// NativeAOT安全: リフレクション不使用。is パターンマッチと switch 式のみ。
/// </summary>
public class UnicodeDisplayConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var preferUnicode = SettingsService.Current?.PreferUnicode ?? false;
        var prop = parameter as string;

        if (value is Beatmap beatmap)
        {
            return prop switch
            {
                "Title" => preferUnicode && !string.IsNullOrEmpty(beatmap.TitleUnicode)
                    ? beatmap.TitleUnicode : beatmap.Title,
                "Artist" => preferUnicode && !string.IsNullOrEmpty(beatmap.ArtistUnicode)
                    ? beatmap.ArtistUnicode : beatmap.Artist,
                _ => string.Empty
            };
        }

        if (value is ImportExportBeatmapItem item)
        {
            return prop switch
            {
                "Title" => preferUnicode && !string.IsNullOrEmpty(item.TitleUnicode)
                    ? item.TitleUnicode : item.Title,
                "Artist" => preferUnicode && !string.IsNullOrEmpty(item.ArtistUnicode)
                    ? item.ArtistUnicode : item.Artist,
                _ => string.Empty
            };
        }

        return value;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
