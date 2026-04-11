using System;
using System.Threading;
using System.Threading.Tasks;

namespace NakuruTool_Avalonia_AOT.Features.BeatmapGenerator;

/// <summary>
/// オーディオファイルのレート（速度）変更を行うサービス。
/// </summary>
public interface IAudioRateChanger
{
    /// <summary>
    /// オーディオファイルのレートを変更して出力する。
    /// </summary>
    /// <param name="inputPath">入力ファイルのフルパス（.mp3, .ogg, .wav）</param>
    /// <param name="outputPath">出力ファイルのフルパス</param>
    /// <param name="rate">レート倍率（例: 1.1 = 1.1倍速）</param>
    /// <param name="changePitch">true = NC方式（ピッチ+速度を同時変更）、false = DT方式（速度のみ変更、ピッチ維持）</param>
    /// <param name="progress">進捗コールバック（0.0〜1.0）</param>
    /// <param name="cancellationToken">キャンセルトークン</param>
    /// <returns>成功時 true、失敗時 false</returns>
    Task<bool> ChangeRateAsync(
        string inputPath,
        string outputPath,
        double rate,
        bool changePitch,
        IProgress<float>? progress = null,
        CancellationToken cancellationToken = default);
}
