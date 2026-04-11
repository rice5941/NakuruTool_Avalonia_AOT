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
    /// MP3/OGG 入力は OGG 出力、WAV 入力は WAV 出力。
    /// </summary>
    /// <param name="inputPath">入力ファイルのフルパス（.mp3, .ogg, .wav）</param>
    /// <param name="outputPath">出力ファイルのフルパス</param>
    /// <param name="rate">レート倍率（例: 1.1 = 1.1倍速）</param>
    /// <param name="changePitch">true = NC方式（ピッチ+速度を同時変更）、false = DT方式（速度のみ変更、ピッチ維持）</param>
    /// <param name="cancellationToken">キャンセルトークン</param>
    /// <returns>成功時 true、失敗時 false</returns>
    Task<bool> ChangeRateAsync(
        string inputPath,
        string outputPath,
        double rate,
        bool changePitch,
        CancellationToken cancellationToken = default);
}
