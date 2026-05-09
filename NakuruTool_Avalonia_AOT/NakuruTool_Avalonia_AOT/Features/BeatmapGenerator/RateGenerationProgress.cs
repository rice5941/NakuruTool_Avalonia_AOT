namespace NakuruTool_Avalonia_AOT.Features.BeatmapGenerator;

/// <summary>単曲/一括生成で共通利用する進捗情報</summary>
/// <param name="MessageKey">翻訳リソースキー（例: "BeatmapGen.Progress.Analyzing"）</param>
/// <param name="MessageArgs">翻訳文字列への format 引数（省略可）</param>
/// <param name="MessagePrefix">フォルダ名などの非翻訳プレフィックス（省略可）</param>
public sealed record RateGenerationProgress(
    string MessageKey,
    int CurrentIndex,
    int TotalCount,
    int ProgressPercent,
    string[]? MessageArgs = null,
    string? MessagePrefix = null);
