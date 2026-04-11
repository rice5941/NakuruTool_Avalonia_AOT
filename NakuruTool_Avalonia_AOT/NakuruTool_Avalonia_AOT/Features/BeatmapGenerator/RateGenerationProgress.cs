namespace NakuruTool_Avalonia_AOT.Features.BeatmapGenerator;

/// <summary>単曲/一括生成で共通利用する進捗情報</summary>
public sealed record RateGenerationProgress(
    string Message,
    int CurrentIndex,
    int TotalCount,
    int ProgressPercent);
