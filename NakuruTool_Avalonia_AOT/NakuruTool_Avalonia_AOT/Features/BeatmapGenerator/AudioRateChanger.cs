using System.Threading;
using System.Threading.Tasks;

namespace NakuruTool_Avalonia_AOT.Features.BeatmapGenerator;

/// <summary>
/// オーディオレート変更の結果。
/// </summary>
/// <param name="Success">変換成功/失敗</param>
/// <param name="ActualOutputPath">実際に出力されたファイルパス。3chフォールバック時は要求と異なるパスになる。失敗時は null</param>
public readonly record struct AudioRateChangeResult(
    bool Success,
    string? ActualOutputPath = null);

/// <summary>
/// オーディオファイルのレート（速度）変更を行うサービス。
/// </summary>
public interface IAudioRateChanger
{
    /// <summary>
    /// オーディオファイルのレートを変更して出力する。
    /// 入力フォーマットと同じフォーマットで出力する（MP3→MP3, OGG→OGG, WAV→WAV）。
    /// 3ch以上のMP3出力要求時はOGGにフォールバックし、ActualOutputPathで通知する。
    /// </summary>
    /// <param name="inputPath">入力ファイルのフルパス（.mp3, .ogg, .wav）</param>
    /// <param name="outputPath">出力ファイルのフルパス</param>
    /// <param name="rate">レート倍率（例: 1.1 = 1.1倍速）</param>
    /// <param name="changePitch">true = NC方式（ピッチ+速度を同時変更）、false = DT方式（速度のみ変更、ピッチ維持）</param>
    /// <param name="mp3VbrQuality">MP3出力時のVBR品質（0=最高, 9=最低）。nullの場合はデフォルト（4）</param>
    /// <param name="cancellationToken">キャンセルトークン</param>
    /// <returns>変換結果</returns>
    Task<AudioRateChangeResult> ChangeRateAsync(
        string inputPath,
        string outputPath,
        double rate,
        bool changePitch,
        int? mp3VbrQuality = null,
        CancellationToken cancellationToken = default);
}
