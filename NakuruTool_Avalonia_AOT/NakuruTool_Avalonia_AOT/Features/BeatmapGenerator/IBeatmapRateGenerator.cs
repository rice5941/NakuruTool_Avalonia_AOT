using System;
using System.Threading;
using System.Threading.Tasks;
using NakuruTool_Avalonia_AOT.Features.OsuDatabase;

namespace NakuruTool_Avalonia_AOT.Features.BeatmapGenerator;

/// <summary>
/// beatmap レート変更生成のオーケストレーションサービス。
/// オーディオ変換と .osu 変換を統合し、生成フロー全体を管理する。
/// </summary>
public interface IBeatmapRateGenerator
{
    /// <summary>単一 beatmap のレート変更生成</summary>
    Task<RateGenerationResult> GenerateAsync(
        Beatmap beatmap,
        RateGenerationOptions options,
        IProgress<RateGenerationProgress>? progress = null,
        CancellationToken cancellationToken = default);

    /// <summary>複数 beatmap の一括レート変更生成</summary>
    Task<BatchGenerationResult> GenerateBatchAsync(
        ReadOnlyMemory<Beatmap> beatmaps,
        RateGenerationOptions options,
        IProgress<RateGenerationProgress>? progress = null,
        CancellationToken cancellationToken = default);
}
