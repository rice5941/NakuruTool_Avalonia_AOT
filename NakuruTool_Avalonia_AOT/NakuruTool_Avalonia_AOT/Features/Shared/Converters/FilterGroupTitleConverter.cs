using NakuruTool_Avalonia_AOT.Features.Translate;

namespace NakuruTool_Avalonia_AOT.Features.Shared.Converters;

/// <summary>
/// 条件の組の番号を「条件の組 N」の文字列に変換するコンバータ
/// </summary>
public class FilterGroupTitleConverter : TranslatingConverterBase
{
    public static readonly FilterGroupTitleConverter Instance = new();

    protected override string Translate(object? value) =>
        string.Format(LanguageService.Instance.GetString("MapFilter.GroupTitle"), value);
}
