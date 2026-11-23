using NakuruTool_Avalonia_AOT.Features.MapList.Models;
using NakuruTool_Avalonia_AOT.Features.Translate;

namespace NakuruTool_Avalonia_AOT.Features.Shared.Converters;

/// <summary>
/// 範囲の境界タイプを「以上 / より大きい」（最小値側）、「以下 / 未満」（最大値側）の文字列に変換するコンバータ
/// </summary>
public class RangeBoundaryToStringConverter : TranslatingConverterBase
{
    /// <summary>最小値側（以上 / より大きい）</summary>
    public static readonly RangeBoundaryToStringConverter Min = new(isMin: true);

    /// <summary>最大値側（以下 / 未満）</summary>
    public static readonly RangeBoundaryToStringConverter Max = new(isMin: false);

    private readonly bool _isMin;

    private RangeBoundaryToStringConverter(bool isMin) => _isMin = isMin;

    protected override string Translate(object? value)
    {
        if (value is not RangeBoundaryType boundary)
            return value?.ToString() ?? string.Empty;

        var key = (_isMin, boundary) switch
        {
            (true, RangeBoundaryType.Inclusive) => "MapFilter.Boundary.MinInclusive",
            (true, _) => "MapFilter.Boundary.MinExclusive",
            (false, RangeBoundaryType.Inclusive) => "MapFilter.Boundary.MaxInclusive",
            (false, _) => "MapFilter.Boundary.MaxExclusive",
        };
        return LanguageService.Instance.GetString(key);
    }
}
