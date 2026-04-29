namespace NakuruTool_Avalonia_AOT.Features.Shared;

/// <summary>
/// .osu ファイルの <c>[Metadata]</c> / <c>[Difficulty]</c> セクションから
/// レート一括生成 JSON 出力に必要となる最小限の項目を保持する値オブジェクト。
/// </summary>
/// <param name="Title">楽曲タイトル (必須)。</param>
/// <param name="Artist">アーティスト名 (必須)。</param>
/// <param name="Version">難易度名 (必須)。</param>
/// <param name="Creator">譜面作者 (必須)。</param>
/// <param name="CircleSize">キー数 (任意)。欠落時は <c>0.0</c>。</param>
/// <param name="BeatmapSetId">ビートマップセット ID (任意)。欠落時は <c>-1</c>。</param>
public readonly record struct OsuFileBasicMetadata(
    string Title,
    string Artist,
    string Version,
    string Creator,
    double CircleSize,
    int BeatmapSetId);
