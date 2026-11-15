using NakuruTool_Avalonia_AOT.Features.Translate;

namespace NakuruTool_Avalonia_AOT.Features.MapList.Sorting.Converters;

/// <summary>
/// SortFieldを多言語対応した文字列に変換するコンバータ
/// </summary>
internal sealed class SortFieldNameConverter : TranslatingConverterBase
{
    public static readonly SortFieldNameConverter Instance = new();

    protected override string Translate(object? value)
    {
        if (value is SortField field)
        {
            return LanguageService.Instance.GetString(KeyOf(field));
        }
        return string.Empty;
    }

    private static string KeyOf(SortField field) => field switch
    {
        SortField.None             => "MapList.Sort.Field.None",
        SortField.KeyCount         => "MapFilter.Target.KeyCount",
        SortField.Status           => "MapFilter.Target.Status",
        SortField.Title            => "MapFilter.Target.Title",
        SortField.Version          => "MapFilter.Target.Version",
        SortField.Artist           => "MapFilter.Target.Artist",
        SortField.Creator          => "MapFilter.Target.Creator",
        SortField.BPM              => "MapFilter.Target.BPM",
        SortField.Difficulty       => "MapFilter.Target.Difficulty",
        SortField.LongNoteRate     => "MapFilter.Target.LongNoteRate",
        SortField.BestAccuracy     => "MapFilter.Target.BestAccuracy",
        SortField.BestScore        => "MapFilter.Target.BestScore",
        SortField.LastPlayed       => "MapFilter.Target.LastPlayed",
        SortField.LastModifiedTime => "MapFilter.Target.LastModifiedTime",
        SortField.PlayCount        => "MapFilter.Target.PlayCount",
        SortField.OD               => "MapFilter.Target.OD",
        SortField.HP               => "MapFilter.Target.HP",
        SortField.DrainTime        => "MapFilter.Target.DrainTime",
        SortField.ManiaPrismOverall   => "MapFilter.Target.ManiaPrismOverall",
        SortField.ManiaPrismLnOverall => "MapFilter.Target.ManiaPrismLnOverall",
        SortField.ManiaDanRegular     => "MapFilter.Target.ManiaDanRegular",
        SortField.ManiaDanLn          => "MapFilter.Target.ManiaDanLn",
        _                          => "MapList.Sort.Field.None",
    };
}
