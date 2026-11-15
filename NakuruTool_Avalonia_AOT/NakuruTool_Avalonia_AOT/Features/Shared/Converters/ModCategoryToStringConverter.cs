using NakuruTool_Avalonia_AOT.Features.OsuDatabase;
using NakuruTool_Avalonia_AOT.Features.Translate;

namespace NakuruTool_Avalonia_AOT.Features.Shared.Converters;

/// <summary>
/// ModCategoryを多言語対応した文字列に変換するコンバータ
/// </summary>
public class ModCategoryToStringConverter : TranslatingConverterBase
{
    public static readonly ModCategoryToStringConverter Instance = new();

    protected override string Translate(object? value)
    {
        if (value is ModCategory modCategory)
        {
            var key = $"MapList.ModCategory.{modCategory}";
            return LanguageService.Instance.GetString(key);
        }
        return value?.ToString() ?? string.Empty;
    }
}
