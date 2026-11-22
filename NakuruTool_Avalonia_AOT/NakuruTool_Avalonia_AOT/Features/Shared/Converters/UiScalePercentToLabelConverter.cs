using NakuruTool_Avalonia_AOT.Features.Shared.Scaling;
using NakuruTool_Avalonia_AOT.Features.Translate;

namespace NakuruTool_Avalonia_AOT.Features.Shared.Converters;

/// <summary>
/// UI 表示倍率の設定値（%）を表示用の文字列に変換するコンバータ。
/// 0（自動）は翻訳文字列、それ以外は「n%」にする。
/// </summary>
public class UiScalePercentToLabelConverter : TranslatingConverterBase
{
    public static readonly UiScalePercentToLabelConverter Instance = new();

    protected override string Translate(object? value)
    {
        return value switch
        {
            UiScaleService.AutoPercent => LanguageService.Instance.GetString("Settings.UiScaleAuto"),
            int percent => $"{percent}%",
            _ => value?.ToString() ?? string.Empty,
        };
    }
}
