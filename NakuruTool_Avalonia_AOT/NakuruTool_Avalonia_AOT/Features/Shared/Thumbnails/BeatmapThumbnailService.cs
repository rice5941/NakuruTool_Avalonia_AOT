using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using NakuruTool_Avalonia_AOT.Features.OsuDatabase;
using NakuruTool_Avalonia_AOT.Features.Settings;
using NakuruTool_Avalonia_AOT.Features.Shared.Extensions;
using NakuruTool_Avalonia_AOT.Features.Shared.Imaging;
using R3;

namespace NakuruTool_Avalonia_AOT.Features.Shared.Thumbnails;

/// <summary>
/// 譜面一覧の背景サムネイルを非同期に読み込み、キャッシュするサービス。
/// MapList と譜面生成ページでキャッシュを共有する。
/// </summary>
public interface IBeatmapThumbnailService : IDisposable
{
    /// <summary>一覧（ViewModel）ごとの表示ページを登録するセッションを作る。</summary>
    IBeatmapThumbnailSession CreateSession();

    /// <summary>キャッシュをすべて破棄する（UI スレッドから呼ぶ）。</summary>
    void Clear();
}

/// <summary>一覧 1 つ分の表示ページ。表示中のサムネイルは追い出し対象から外れ、読み込みが優先される。</summary>
public interface IBeatmapThumbnailSession : IDisposable
{
    /// <summary>
    /// 表示ページを差し替える（UI スレッドから呼ぶ）。
    /// <paramref name="destination"/> の各要素に対応するサムネイル（作れない譜面は null）を書き込み、
    /// 未読込のものを読み込み待ちキューの先頭に積む。前のページで読み込み待ちのまま外れたものは読み込まない。
    /// </summary>
    void SetPage(ReadOnlySpan<Beatmap> beatmaps, Span<BeatmapThumbnail?> destination);

    /// <summary>
    /// 読み込みを一時停止・再開する（UI スレッドから呼ぶ。一覧が画面に出ていない間の無駄な読み込みを止める）。
    /// 停止中も <see cref="SetPage"/> はサムネイルを返し（読み込み済みはそのまま表示でき、追い出されない）、
    /// 未読込のものは読み込まない。読み込み待ちに残っていた分も読まずに捨てる。
    /// 再開すると、その時点の表示ページの未読込分を優先して読み込む。
    /// </summary>
    void SetPaused(bool paused);
}

/// <summary>
/// <see cref="IBeatmapThumbnailService"/> の実装。
/// <list type="bullet">
/// <item>ファイル I/O・デコードはバックグラウンドのワーカー（少数）で行い、結果だけを UI スレッドへ Post する。</item>
/// <item>デコードは表示サイズに合わせて縮小する（原寸の背景を保持しない）。</item>
/// <item>同じ背景画像を使う難易度は 1 回のデコード結果を参照カウントで共有する。</item>
/// <item>最新の表示ページを優先し、表示から外れた読み込み待ちは捨てる。</item>
/// <item>表示中でないサムネイルは LRU で上限数まで保持し、超えたら破棄する。</item>
/// <item>一覧が画面に出ていない間（音楽プレイヤーモードなど）は、セッション単位で読み込みを一時停止できる。</item>
/// </list>
/// </summary>
public sealed class BeatmapThumbnailService : IBeatmapThumbnailService
{
    /// <summary>デコード後の幅（px）。一覧の表示幅 96DIP を 200% スケールでも粗くならない大きさ。</summary>
    public const int DecodeWidth = 192;

    private readonly ISettingsService _settingsService;
    private readonly CompositeDisposable _disposables = new();

    // ---- UI スレッドでのみ操作 ----
    private readonly Dictionary<string, BeatmapThumbnail> _cache = new(StringComparer.Ordinal);
    /// <summary>先頭が最近使ったもの</summary>
    private readonly LinkedList<BeatmapThumbnail> _lru = new();

    // ---- _gate で保護 ----
    private readonly object _gate = new();
    private readonly LinkedList<BeatmapThumbnail> _queue = new();
    /// <summary>背景画像パス → デコード結果（大文字小文字を区別しない: Windows のパス）</summary>
    private readonly Dictionary<string, SharedBitmap> _bitmaps = new(StringComparer.OrdinalIgnoreCase);
    private int _runningWorkers;
    private bool _disposed;

    public BeatmapThumbnailService(ISettingsService settingsService)
    {
        _settingsService = settingsService;

        // osu! フォルダが変わったらパスがすべて無効になるので捨てる
        _settingsService.SettingsData.ObservePropertyAndSubscribe(
            nameof(ISettingsData.OsuFolderPath),
            () =>
            {
                if (Dispatcher.UIThread.CheckAccess()) Clear();
                else Dispatcher.UIThread.Post(Clear);
            },
            _disposables);
    }

    /// <summary>表示中でないサムネイルを保持する上限数（表示中のものは上限を超えても保持する）。</summary>
    internal int Capacity { get; init; } = 300;

    /// <summary>同時に読み込むワーカー数。ディスクの取り合い（DB 読込・7K 解析）を避けるため少なめにする。</summary>
    internal int MaxWorkers { get; init; } = Math.Clamp(Environment.ProcessorCount / 4, 1, 3);

    /// <summary>背景画像のデコード（テストで差し替え可能）。</summary>
    internal Func<string, Bitmap?> Decoder { get; init; } = DecodeThumbnail;

    /// <summary>読み込み結果を UI スレッドへ渡す（テストで差し替え可能）。</summary>
    internal Action<Action> PostToUi { get; init; } =
        static action => Dispatcher.UIThread.Post(action, DispatcherPriority.Background);

    /// <summary>キャッシュ中のサムネイル数（テスト用）</summary>
    internal int CachedCount => _cache.Count;

    public IBeatmapThumbnailSession CreateSession() => new Session(this);

    // ---- 表示ページの登録 ----

    private void SetPage(Session session, ReadOnlySpan<Beatmap> beatmaps, Span<BeatmapThumbnail?> destination)
    {
        if (destination.Length < beatmaps.Length)
            throw new ArgumentException("destination is shorter than beatmaps.", nameof(destination));

        var osuFolderPath = _settingsService.SettingsData.OsuFolderPath;
        int workersToStart;

        lock (_gate)
        {
            // 前のページの表示を外す
            UnpinLocked(session);

            if (_disposed)
            {
                destination[..beatmaps.Length].Clear();
                return;
            }

            session.EnsureCapacity(beatmaps.Length);
            for (int i = 0; i < beatmaps.Length; i++)
            {
                var thumbnail = GetOrCreate(beatmaps[i], osuFolderPath);
                destination[i] = thumbnail;
                if (thumbnail is null) continue;

                thumbnail.PinCount++;
                if (!session.Paused) thumbnail.ActivePinCount++;
                session.Add(thumbnail);
                Touch(thumbnail);
            }

            // 一時停止中は積まない（再開時に積む）
            workersToStart = session.Paused ? 0 : EnqueueFrontLocked(session);
        }

        StartWorkers(workersToStart);
        EvictOverflow();
    }

    private void SetPaused(Session session, bool paused)
    {
        int workersToStart = 0;
        lock (_gate)
        {
            if (session.Paused == paused) return;
            session.Paused = paused;

            var pinned = session.Pinned;
            for (int i = 0; i < session.Count; i++)
                pinned[i].ActivePinCount += paused ? -1 : 1;

            // 停止: 読み込み待ちの分は、ワーカーが取り出した時点で（他の一覧が表示していなければ）捨てられる
            // 再開: 表示中ページの未読込分を先頭に積む
            if (!paused && !_disposed)
                workersToStart = EnqueueFrontLocked(session);
        }

        StartWorkers(workersToStart);
    }

    /// <summary>
    /// セッションの表示ページのうち未読込のものをキューの先頭へ積み、起動すべきワーカー数を返す（_gate 内で呼ぶ）。
    /// ページの上から順に読むよう、末尾から先頭へ積む（最新ページがキューの先頭に来る）。
    /// </summary>
    private int EnqueueFrontLocked(Session session)
    {
        var pinned = session.Pinned;
        for (int i = session.Count - 1; i >= 0; i--)
        {
            var thumbnail = pinned[i];
            switch (thumbnail.State)
            {
                case BeatmapThumbnailState.NotLoaded:
                    thumbnail.State = BeatmapThumbnailState.Queued;
                    thumbnail.QueueNode = _queue.AddFirst(thumbnail);
                    break;
                case BeatmapThumbnailState.Queued when thumbnail.QueueNode is { } node:
                    _queue.Remove(node);
                    _queue.AddFirst(node);
                    break;
            }
        }

        var workersToStart = Math.Min(MaxWorkers - _runningWorkers, _queue.Count);
        if (workersToStart < 0) workersToStart = 0;
        _runningWorkers += workersToStart;
        return workersToStart;
    }

    private void StartWorkers(int count)
    {
        for (int i = 0; i < count; i++)
            Task.Run(WorkerLoop);
    }

    private void UnpinLocked(Session session)
    {
        var pinned = session.Pinned;
        for (int i = 0; i < session.Count; i++)
        {
            pinned[i].PinCount--;
            if (!session.Paused) pinned[i].ActivePinCount--;
            pinned[i] = null!;
        }
        session.Count = 0;
    }

    private BeatmapThumbnail? GetOrCreate(Beatmap beatmap, string osuFolderPath)
    {
        if (string.IsNullOrEmpty(osuFolderPath) ||
            string.IsNullOrEmpty(beatmap.FolderName) ||
            string.IsNullOrEmpty(beatmap.OsuFileName))
        {
            return null;
        }

        var key = string.Concat(beatmap.FolderName, "\0", beatmap.OsuFileName);
        if (_cache.TryGetValue(key, out var cached))
            return cached;

        string songDirectory;
        string osuFilePath;
        try
        {
            songDirectory = Path.Combine(osuFolderPath, "Songs", beatmap.FolderName);
            osuFilePath = Path.Combine(songDirectory, beatmap.OsuFileName);
        }
        catch (ArgumentException)
        {
            return null;
        }

        var created = new BeatmapThumbnail(key, songDirectory, osuFilePath);
        created.LruNode = _lru.AddFirst(created);
        _cache.Add(key, created);
        return created;
    }

    private void Touch(BeatmapThumbnail thumbnail)
    {
        if (thumbnail.LruNode is { } node && node != _lru.First)
        {
            _lru.Remove(node);
            _lru.AddFirst(node);
        }
    }

    // ---- 追い出し ----

    /// <summary>上限を超えた分を、使われていない古いものから破棄する（表示中・読み込み中は残す）。</summary>
    private void EvictOverflow()
    {
        var overflow = _cache.Count - Capacity;
        if (overflow <= 0) return;

        // 候補選びだけロック内で行う。PinCount と NotLoaded→Queued の遷移は UI スレッドでしか起きず、
        // Loaded / Failed / NotLoaded はワーカーが触らないので、ロック外で Remove しても状態は変わらない。
        List<BeatmapThumbnail>? victims = null;
        lock (_gate)
        {
            for (var node = _lru.Last; node is not null && overflow > 0; node = node.Previous)
            {
                var thumbnail = node.Value;
                if (thumbnail.PinCount <= 0 &&
                    thumbnail.State is not (BeatmapThumbnailState.Queued or BeatmapThumbnailState.Loading))
                {
                    (victims ??= new()).Add(thumbnail);
                    overflow--;
                }
            }
        }

        if (victims is null) return;
        foreach (var thumbnail in victims)
            Remove(thumbnail);
    }

    /// <summary>キャッシュから外し、表示を消してから共有 Bitmap の参照を解放する。</summary>
    private void Remove(BeatmapThumbnail thumbnail)
    {
        if (thumbnail.LruNode is { } node)
        {
            _lru.Remove(node);
            thumbnail.LruNode = null;
        }
        _cache.Remove(thumbnail.Key);
        thumbnail.Evicted = true;

        var image = thumbnail.Image;
        thumbnail.Image = null; // 先にバインディングから外す（破棄済み Bitmap を描画させない）
        if (image is not null && thumbnail.BackgroundPath is { } path)
            ReleaseBitmap(path);
        thumbnail.BackgroundPath = null;
    }

    public void Clear()
    {
        lock (_gate)
        {
            for (var node = _queue.First; node is not null; node = node.Next)
            {
                node.Value.QueueNode = null;
                node.Value.State = BeatmapThumbnailState.NotLoaded;
            }
            _queue.Clear();
        }

        if (_cache.Count == 0) return;
        var all = new BeatmapThumbnail[_cache.Count];
        _cache.Values.CopyTo(all, 0);
        // 読み込み中のものも追い出す（Evicted になるので、後から届いた結果は Complete 側で解放される）
        foreach (var thumbnail in all)
            Remove(thumbnail);
    }

    // ---- 共有 Bitmap ----

    private sealed class SharedBitmap
    {
        /// <summary>デコード済みの画像（デコード中は null）</summary>
        public Bitmap? Bitmap;
        /// <summary>この画像を参照しているサムネイル数</summary>
        public int RefCount;
        /// <summary>デコード中に同じ画像を要求したサムネイル</summary>
        public List<BeatmapThumbnail>? Waiters;
    }

    private void ReleaseBitmap(string path)
    {
        Bitmap? toDispose = null;
        lock (_gate)
        {
            if (_bitmaps.TryGetValue(path, out var entry) && entry.Bitmap is not null && --entry.RefCount <= 0)
            {
                _bitmaps.Remove(path);
                toDispose = entry.Bitmap;
            }
        }
        toDispose?.Dispose();
    }

    // ---- ワーカー ----

    private void WorkerLoop()
    {
        while (true)
        {
            BeatmapThumbnail? target = null;
            lock (_gate)
            {
                while (_queue.First is { } node)
                {
                    _queue.RemoveFirst();
                    var thumbnail = node.Value;
                    thumbnail.QueueNode = null;

                    // 表示から外れたもの・一時停止中の一覧だけが表示しているものは読まない（次に表示・再開されたら積み直す）
                    if (thumbnail.ActivePinCount > 0)
                    {
                        thumbnail.State = BeatmapThumbnailState.Loading;
                        target = thumbnail;
                        break;
                    }
                    thumbnail.State = BeatmapThumbnailState.NotLoaded;
                }

                if (target is null)
                {
                    _runningWorkers--;
                    return;
                }
            }

            try
            {
                Load(target);
            }
            catch
            {
                Complete(target, null, null);
            }
        }
    }

    private void Load(BeatmapThumbnail thumbnail)
    {
        var backgroundPath = ResolveBackgroundPath(thumbnail);
        if (backgroundPath is null)
        {
            Complete(thumbnail, null, null);
            return;
        }

        SharedBitmap entry;
        lock (_gate)
        {
            if (_bitmaps.TryGetValue(backgroundPath, out var existing))
            {
                if (existing.Bitmap is { } ready)
                {
                    existing.RefCount++;
                    Complete(thumbnail, backgroundPath, ready);
                }
                else
                {
                    // 同じ画像を別のワーカーがデコード中。終わったらまとめて渡してもらう
                    (existing.Waiters ??= new()).Add(thumbnail);
                }
                return;
            }

            entry = new SharedBitmap();
            _bitmaps.Add(backgroundPath, entry);
        }

        Bitmap? bitmap = null;
        try
        {
            bitmap = Decoder(backgroundPath);
        }
        catch
        {
            bitmap = null;
        }

        List<BeatmapThumbnail>? waiters;
        lock (_gate)
        {
            waiters = entry.Waiters;
            entry.Waiters = null;
            if (bitmap is null)
            {
                _bitmaps.Remove(backgroundPath);
            }
            else
            {
                entry.Bitmap = bitmap;
                entry.RefCount = 1 + (waiters?.Count ?? 0);
            }
        }

        Complete(thumbnail, bitmap is null ? null : backgroundPath, bitmap);
        if (waiters is not null)
        {
            foreach (var waiter in waiters)
                Complete(waiter, bitmap is null ? null : backgroundPath, bitmap);
        }
    }

    private static string? ResolveBackgroundPath(BeatmapThumbnail thumbnail)
        => BeatmapBackgroundDecoder.ResolveBackgroundPath(thumbnail.SongDirectory, thumbnail.OsuFilePath);

    /// <summary>背景画像を幅 <see cref="DecodeWidth"/> に縮小してデコードする（LOH を通らない。<see cref="BeatmapBackgroundDecoder.DecodeToWidth"/> 参照）。</summary>
    internal static Bitmap? DecodeThumbnail(string path) => BeatmapBackgroundDecoder.DecodeToWidth(path, DecodeWidth);

    /// <summary>読み込み結果を UI スレッドで反映する。<paramref name="bitmap"/> が非 null なら参照を 1 つ持った状態で渡される。</summary>
    private void Complete(BeatmapThumbnail thumbnail, string? backgroundPath, Bitmap? bitmap)
    {
        PostToUi(() =>
        {
            if (thumbnail.Evicted)
            {
                if (bitmap is not null && backgroundPath is not null)
                    ReleaseBitmap(backgroundPath);
                return;
            }

            lock (_gate)
            {
                thumbnail.State = bitmap is null ? BeatmapThumbnailState.Failed : BeatmapThumbnailState.Loaded;
            }
            thumbnail.BackgroundPath = backgroundPath;
            thumbnail.Image = bitmap;
        });
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed) return;
            _disposed = true;
        }
        _disposables.Dispose();
        Clear();
    }

    // ---- セッション ----

    private sealed class Session(BeatmapThumbnailService owner) : IBeatmapThumbnailSession
    {
        public BeatmapThumbnail[] Pinned = Array.Empty<BeatmapThumbnail>();
        public int Count;
        private bool _disposed;

        public void EnsureCapacity(int capacity)
        {
            if (Pinned.Length < capacity)
                Pinned = new BeatmapThumbnail[capacity];
        }

        /// <summary>読み込みを一時停止中か（owner._gate 内で読み書き）</summary>
        public bool Paused;

        public void Add(BeatmapThumbnail thumbnail) => Pinned[Count++] = thumbnail;

        public void SetPaused(bool paused)
        {
            if (_disposed) return;
            owner.SetPaused(this, paused);
        }

        public void SetPage(ReadOnlySpan<Beatmap> beatmaps, Span<BeatmapThumbnail?> destination)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(IBeatmapThumbnailSession));
            owner.SetPage(this, beatmaps, destination);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            lock (owner._gate)
            {
                owner.UnpinLocked(this);
            }
        }
    }
}
