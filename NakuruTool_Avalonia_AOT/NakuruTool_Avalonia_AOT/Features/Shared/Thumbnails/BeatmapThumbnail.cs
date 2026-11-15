using System.Collections.Generic;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;

namespace NakuruTool_Avalonia_AOT.Features.Shared.Thumbnails;

/// <summary>サムネイルの読み込み状態</summary>
internal enum BeatmapThumbnailState
{
    /// <summary>未読込（キューにも積まれていない）</summary>
    NotLoaded,
    /// <summary>読み込み待ちキューに積まれている</summary>
    Queued,
    /// <summary>ワーカーが読み込み中（または同じ背景画像のデコード完了待ち）</summary>
    Loading,
    /// <summary>読み込み済み（<see cref="BeatmapThumbnail.Image"/> が非 null）</summary>
    Loaded,
    /// <summary>背景画像なし・読み込み失敗。再表示のたびに再試行しないよう結果として保持する</summary>
    Failed,
}

/// <summary>
/// 譜面の背景画像サムネイル（.osu ファイル単位）。
/// <see cref="BeatmapThumbnailService"/> が生成・キャッシュし、表示ページの行コピー（<c>Beatmap.Thumbnail</c>）に載せる。
/// 読み込みが終わると <see cref="Image"/> が UI スレッドで設定され、バインディング経由で表示が差し替わる。
/// </summary>
public sealed class BeatmapThumbnail : ObservableObject
{
    private Bitmap? _image;

    internal BeatmapThumbnail(string key, string songDirectory, string osuFilePath)
    {
        Key = key;
        SongDirectory = songDirectory;
        OsuFilePath = osuFilePath;
    }

    /// <summary>縮小済みの背景画像。未読込・画像なしの間は null（UI スレッドでのみ更新）。</summary>
    public Bitmap? Image
    {
        get => _image;
        internal set => SetProperty(ref _image, value);
    }

    /// <summary>キャッシュのキー（FolderName と OsuFileName の組）</summary>
    internal string Key { get; }

    /// <summary>譜面フォルダの絶対パス</summary>
    internal string SongDirectory { get; }

    /// <summary>.osu ファイルの絶対パス</summary>
    internal string OsuFilePath { get; }

    // ---- 以下はサービスが管理する内部状態 ----

    /// <summary>読み込み状態（サービスのロック内で更新）</summary>
    internal BeatmapThumbnailState State { get; set; }

    /// <summary>このサムネイルを表示中のページ（セッション）の数（サービスのロック内で更新）</summary>
    internal int PinCount { get; set; }

    /// <summary>
    /// このサムネイルを表示中で、かつ読み込みを一時停止していないページの数（サービスのロック内で更新）。
    /// 0 のものはワーカーが読まずに捨てる。追い出しの判定は <see cref="PinCount"/> で行う。
    /// </summary>
    internal int ActivePinCount { get; set; }

    /// <summary>読み込み待ちキュー上のノード（キューに無いときは null）</summary>
    internal LinkedListNode<BeatmapThumbnail>? QueueNode { get; set; }

    /// <summary>LRU リスト上のノード（UI スレッドでのみ操作）</summary>
    internal LinkedListNode<BeatmapThumbnail>? LruNode { get; set; }

    /// <summary><see cref="Image"/> の元になった背景画像のパス（共有 Bitmap の参照カウント解放用）</summary>
    internal string? BackgroundPath { get; set; }

    /// <summary>キャッシュから追い出し済み。以後に届いた読み込み結果は破棄する（UI スレッドでのみ操作）</summary>
    internal bool Evicted { get; set; }
}
