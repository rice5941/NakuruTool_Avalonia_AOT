using System.Collections.Generic;

namespace NakuruTool_Avalonia_AOT.Features.BeatmapGenerator;

/// <summary>
/// .osuファイルが参照する外部アセットの一覧。
/// ファイル名はすべて beatmap フォルダからの相対パス。
/// </summary>
public sealed record OsuReferencedAssets
{
    /// <summary>[General] AudioFilename — メインオーディオ</summary>
    public required string MainAudioFilename { get; init; }

    /// <summary>
    /// レート変換が必要な音声ファイル（メインオーディオを除く）。
    /// [Events] Sample行 + [HitObjects] カスタムヒットサウンド + .osb内のSample行 の和集合。
    /// 拡張子が .wav/.ogg/.mp3 のもののみ。重複なし。
    /// </summary>
    public required IReadOnlySet<string> SampleAudioFiles { get; init; }

    /// <summary>
    /// そのままコピーする非音声ファイル。
    /// 背景画像、動画、スプライト、Animationフレーム画像、.osbファイル自体。重複なし。
    /// </summary>
    public required IReadOnlySet<string> NonAudioFiles { get; init; }
}
