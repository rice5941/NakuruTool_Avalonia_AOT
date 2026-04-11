namespace NakuruTool_Avalonia_AOT.Features.BeatmapGenerator;

/// <summary>
/// .osu ファイルのレート変換サービス。
/// 入力 .osu ファイルを読み込み、指定レートで時間軸を変換した新しい .osu ファイルを出力する。
/// </summary>
public interface IOsuFileRateConverter
{
    /// <summary>
    /// .osu ファイルをレート変換し、出力先パスに書き出す。
    /// </summary>
    /// <param name="sourceOsuPath">変換元 .osu ファイルの絶対パス</param>
    /// <param name="destinationOsuPath">変換先 .osu ファイルの絶対パス</param>
    /// <param name="options">変換パラメータ</param>
    void Convert(string sourceOsuPath, string destinationOsuPath, OsuFileConvertOptions options);
}
