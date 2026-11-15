using NakuruTool_Avalonia_AOT.Features.MapList.Models;
using NakuruTool_Avalonia_AOT.Features.Translate;

namespace NakuruTool_Avalonia_AOT.Features.Shared.Converters;

/// <summary>
/// FilterTargetを多言語対応した文字列に変換するコンバータ
/// </summary>
public class FilterTargetToStringConverter : TranslatingConverterBase
{
    public static readonly FilterTargetToStringConverter Instance = new();

    protected override string Translate(object? value)
    {
        if (value is FilterTarget target)
        {
            var key = $"MapFilter.Target.{target}";
            return LanguageService.Instance.GetString(key);
        }
        return value?.ToString() ?? string.Empty;
    }
}
