namespace NakuruTool_Avalonia_AOT.Features.BeatmapGenerator;

/// <summary>一括レート変更生成の結果</summary>
public sealed record BatchGenerationResult
{
    public required RateGenerationResult[] Results { get; init; }
    public int SuccessCount { get; init; }
    public int FailureCount { get; init; }
    public bool WasCancelled { get; init; }
}
