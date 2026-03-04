using Avalonia.Media.Imaging;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NakuruTool_Avalonia_AOT.Features.OsuDatabase;
using NakuruTool_Avalonia_AOT.Features.Settings;
using NakuruTool_Avalonia_AOT.Features.Shared.ViewModels;
using R3;
using System;
using System.Collections.Generic;
using System.IO;

namespace NakuruTool_Avalonia_AOT.Features.AudioPlayer;

/// <summary>
/// リピートモード
/// </summary>
public enum RepeatMode
{
    None,
    All,
    One
}

/// <summary>
/// オーディオ再生パネルのViewModel。
/// シーク・次曲/前曲・シャッフル・リピート・背景画像・曲情報表示を管理する。
/// </summary>
public partial class AudioPlayerPanelViewModel : ViewModelBase
{
    // ---- [ObservableProperty] プロパティ ----

    [ObservableProperty]
    public partial double CurrentPosition { get; set; }

    [ObservableProperty]
    public partial double Duration { get; set; }

    [ObservableProperty]
    public partial string CurrentPositionText { get; set; } = "0:00";

    [ObservableProperty]
    public partial string DurationText { get; set; } = "0:00";

    /// <summary>シークバードラッグ中フラグ。true の間はポーリング更新を抑制する。</summary>
    [ObservableProperty]
    public partial bool IsSeeking { get; set; }

    [ObservableProperty]
    public partial RepeatMode RepeatMode { get; set; } = RepeatMode.None;

    [ObservableProperty]
    public partial bool IsShuffleEnabled { get; set; }

    /// <summary>背景画像。null 時は非表示。</summary>
    [ObservableProperty]
    public partial Bitmap? BackgroundImage { get; set; }

    [ObservableProperty]
    public partial string CurrentTitle { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string CurrentArtist { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string CurrentVersion { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string CurrentCreator { get; set; } = string.Empty;

    // ---- 手動プロパティ ----

    /// <summary>
    /// 既存の再生/音量/状態VMへの参照。
    /// 再生制御 (Play, Stop, Pause, TogglePlayPause) はすべてこちら経由で呼び出す。
    /// </summary>
    public AudioPlayerViewModel AudioPlayer { get; }

    // ---- 内部フィールド ----

    /// <summary>
    /// 位置/シーク系 API (GetPosition, GetDuration, Seek) の直接呼び出し用。
    /// 再生制御は AudioPlayer (AudioPlayerViewModel) 経由で行う。
    /// </summary>
    private readonly IAudioPlayerService _audioPlayerService;

    private readonly ISettingsService _settingsService;

    /// <summary>再生位置ポーリング購読ハンドル。</summary>
    private IDisposable? _positionTimer;

    private Beatmap? _currentBeatmap;
    private Beatmap[] _filteredBeatmaps = Array.Empty<Beatmap>();
    private int _currentTrackIndex;

    private readonly List<int> _shuffleHistory = new();
    private int _shuffleIndex = -1;

    /// <summary>ランダム再生で既に再生された曲のインデックス。重複防止用。</summary>
    private readonly HashSet<int> _playedIndices = new();

    /// <summary>GetNextShuffleIndex 内で候補を収集する再利用バッファ。毎回のアロケーションを回避する。</summary>
    private readonly List<int> _shuffleCandidates = new();

    private Action<int>? _navigateCallback;

    /// <summary>前回ロード成功した背景画像の OsuFileName。同ファイルの重複ロードを回避する（成功時のみ更新）。</summary>
    private string? _lastBgOsuFileName;

    // ---- コンストラクタ ----

    public AudioPlayerPanelViewModel(
        IAudioPlayerService audioPlayerService,
        AudioPlayerViewModel audioPlayerViewModel,
        ISettingsService settingsService)
    {
        _audioPlayerService = audioPlayerService;
        AudioPlayer = audioPlayerViewModel;
        _settingsService = settingsService;

        // 再生状態変化 → ポーリング制御
        _audioPlayerService.StateChanged
            .Subscribe(state =>
            {
                if (state == AudioPlayerState.Playing)
                    StartPositionPolling();
                else
                    StopPositionPolling();
            })
            .AddTo(Disposables);

        // 再生完了通知 (自然終了のみ)
        _audioPlayerService.PlaybackCompleted
            .Subscribe(_ => HandlePlaybackCompleted())
            .AddTo(Disposables);
    }

    // ---- 再生開始 ----

    /// <summary>
    /// 指定した Beatmap を再生し、パネルの表示内容をすべて更新する。
    /// </summary>
    /// <param name="autoPlay">true のとき実際に再生を開始する。false のときは曲情報・背景画像の更新のみ行う。</param>
    public void PlayBeatmap(Beatmap beatmap, int trackIndex, bool autoPlay = true)
    {
        _currentBeatmap = beatmap;
        _currentTrackIndex = trackIndex;

        // 曲情報更新 (PreferUnicode 判定)
        var preferUnicode = _settingsService.SettingsData.PreferUnicode;
        CurrentTitle = (preferUnicode && !string.IsNullOrEmpty(beatmap.TitleUnicode))
            ? beatmap.TitleUnicode
            : beatmap.Title;
        CurrentArtist = (preferUnicode && !string.IsNullOrEmpty(beatmap.ArtistUnicode))
            ? beatmap.ArtistUnicode
            : beatmap.Artist;
        CurrentVersion = beatmap.Version;
        CurrentCreator = beatmap.Creator;

        // 背景画像読み込み
        LoadBackgroundImage(beatmap);

        // 再生開始 (再生制御は AudioPlayerViewModel 経由)
        // Duration取得のため常に再生を開始し、autoPlay=false の場合は即停止する
        AudioPlayer.PlayBeatmapAudio(beatmap);

        // 位置・時間リセット
        // symphonia probe により GetDuration() は MP3 を含む全形式で確実に値を返す
        CurrentPosition = 0;
        CurrentPositionText = "0:00";
        var dur = _audioPlayerService.GetDuration();
        Duration = dur;
        DurationText = FormatTime(dur);

        if (!autoPlay)
        {
            // 自動再生が無効の場合は即停止する
            _audioPlayerService.Stop();
        }
    }

    // ---- ナビゲーションコンテキスト更新 ----

    /// <summary>
    /// フィルタ変更時に MapListViewModel から呼び出される。
    /// </summary>
    public void SetNavigationContext(Beatmap[] filteredBeatmaps, int currentIndex)
    {
        _filteredBeatmaps = filteredBeatmaps;
        _currentTrackIndex = currentIndex;
        _shuffleHistory.Clear();
        _shuffleIndex = -1;
        _playedIndices.Clear();
        _lastBgOsuFileName = null; // フィルタ変更時は背景キャッシュもリセット
    }

    // ---- コールバック設定 ----

    /// <summary>MapListViewModel がコンストラクタで設定するページ遷移コールバック。</summary>
    public void SetNavigateCallback(Action<int> callback)
    {
        _navigateCallback = callback;
    }

    // ---- コマンド ----

    /// <summary>
    /// 再生／一時停止を切り替える。停止中かつ現在曲が設定されている場合は再生を開始する。
    /// </summary>
    [RelayCommand]
    private void TogglePlayPause()
    {
        if (_audioPlayerService.CurrentState == AudioPlayerState.Stopped && _currentBeatmap != null)
        {
            // 停止中の場合は現在の曲を先頭から再生する
            AudioPlayer.PlayBeatmapAudio(_currentBeatmap);
            var dur = _audioPlayerService.GetDuration();
            Duration = dur;
            DurationText = FormatTime(dur);
        }
        else
        {
            _audioPlayerService.TogglePlayPause();
        }
    }

    /// <summary>次の曲へ移動する。シャッフル時はランダム選択、通常時はインデックス+1（非再生可能曲をスキップ）。</summary>
    [RelayCommand]
    private void NextTrack()
    {
        if (_filteredBeatmaps.Length == 0) return;

        int nextIndex;
        if (IsShuffleEnabled)
        {
            nextIndex = GetNextShuffleIndex();
        }
        else
        {
            // 再生可能な次の曲が見つかるまでスキップ
            nextIndex = _currentTrackIndex + 1;
            bool found = false;
            for (int i = 0; i < _filteredBeatmaps.Length; i++, nextIndex++)
            {
                if (nextIndex >= _filteredBeatmaps.Length)
                {
                    if (RepeatMode == RepeatMode.All)
                        nextIndex = 0;
                    else
                        return;
                }
                if (IsPlayableBeatmap(_filteredBeatmaps[nextIndex])) { found = true; break; }
            }
            if (!found) return;
        }

        NavigateToTrack(nextIndex);
    }

    /// <summary>前の曲へ移動する。3秒以上再生済みなら現在曲の先頭に戻る。通常時は非再生可能曲をスキップ。</summary>
    [RelayCommand]
    private void PreviousTrack()
    {
        if (_filteredBeatmaps.Length == 0) return;

        // 3秒以上再生済みなら現在曲の先頭に戻る
        if (_audioPlayerService.GetPosition() > 3.0)
        {
            _audioPlayerService.Seek(0);
            CurrentPosition = 0;
            CurrentPositionText = "0:00";
            return;
        }

        int prevIndex;
        if (IsShuffleEnabled)
        {
            prevIndex = GetPreviousShuffleIndex();
        }
        else
        {
            // 再生可能な前の曲が見つかるまでスキップ
            prevIndex = _currentTrackIndex - 1;
            bool found = false;
            for (int i = 0; i < _filteredBeatmaps.Length; i++, prevIndex--)
            {
                if (prevIndex < 0)
                {
                    if (RepeatMode == RepeatMode.All)
                        prevIndex = _filteredBeatmaps.Length - 1;
                    else
                        return;
                }
                if (IsPlayableBeatmap(_filteredBeatmaps[prevIndex])) { found = true; break; }
            }
            if (!found) return;
        }

        NavigateToTrack(prevIndex);
    }

    /// <summary>リピートモードを循環切替する (None → All → One → None)。</summary>
    [RelayCommand]
    private void ToggleRepeat()
    {
        RepeatMode = RepeatMode switch
        {
            RepeatMode.None => RepeatMode.All,
            RepeatMode.All  => RepeatMode.One,
            RepeatMode.One  => RepeatMode.None,
            _               => RepeatMode.None
        };
    }

    /// <summary>シャッフルを ON/OFF 切替し、履歴をリセットする。</summary>
    [RelayCommand]
    private void ToggleShuffle()
    {
        IsShuffleEnabled = !IsShuffleEnabled;
        _shuffleHistory.Clear();
        _shuffleIndex = -1;
        _playedIndices.Clear();
    }

    /// <summary>シークバードラッグ完了時に現在位置をサービスに反映する。</summary>
    [RelayCommand]
    private void Seek()
    {
        var target = CurrentPosition;
        _audioPlayerService.Seek(target);
        CurrentPosition = target;
        CurrentPositionText = FormatTime(target);
    }

    // ---- 再生位置ポーリング ----

    /// <summary>
    /// 200ms 間隔で再生位置をポーリングする。
    /// Observable.Interval はバックグラウンドスレッドで実行されるため、
    /// UIプロパティの更新は Dispatcher.UIThread.Post() でマーシャリングする。
    /// </summary>
    private void StartPositionPolling()
    {
        StopPositionPolling();
        _positionTimer = Observable.Interval(TimeSpan.FromMilliseconds(200))
            .Subscribe(_ =>
            {
                if (IsSeeking) return;
                var pos = _audioPlayerService.GetPosition();
                Dispatcher.UIThread.Post(() =>
                {
                    CurrentPosition = pos;
                    CurrentPositionText = FormatTime(pos);
                });
            });
    }

    private void StopPositionPolling()
    {
        _positionTimer?.Dispose();
        _positionTimer = null;
    }

    // ---- 背景画像 ----

    /// <summary>
    /// Beatmap の OsuFileName から背景画像を読み込む。
    /// 同じ.osuファイルは再読み込みをスキップし、失敗時はキャッシュを更新せず再試行を許容する。
    /// </summary>
    private void LoadBackgroundImage(Beatmap beatmap)
    {
        // 同じ.osuファイルは再読み込みをスキップ
        if (beatmap.OsuFileName == _lastBgOsuFileName) return;

        // 旧画像を破棄
        BackgroundImage?.Dispose();
        BackgroundImage = null;

        var osuFolderPath = _settingsService.SettingsData.OsuFolderPath;
        if (string.IsNullOrEmpty(osuFolderPath)) return;

        var songDir = Path.Combine(osuFolderPath, "Songs", beatmap.FolderName);

        // osu!DB から取得した .osu ファイル名を使用
        if (string.IsNullOrEmpty(beatmap.OsuFileName)) return;
        var osuFilePath = Path.Combine(songDir, beatmap.OsuFileName);
        if (!File.Exists(osuFilePath)) return;

        var bgFilename = OsuFileParser.GetBackgroundFilename(osuFilePath);
        if (string.IsNullOrEmpty(bgFilename)) return;

        var bgPath = Path.Combine(songDir, bgFilename);
        if (!File.Exists(bgPath)) return;

        try
        {
            BackgroundImage = new Bitmap(bgPath);
            // 成功時のみキャッシュを更新（失敗後に同じファイルを選択した場合に再試行できるよう）
            _lastBgOsuFileName = beatmap.OsuFileName;
        }
        catch
        {
            BackgroundImage = null;
        }
    }

    // ---- 再生終了ハンドリング ----

    /// <summary>
    /// 再生の自然終了時に呼ばれる。リピートモードに応じて次の動作を決定する。
    /// </summary>
    private void HandlePlaybackCompleted()
    {
        switch (RepeatMode)
        {
            case RepeatMode.One:
                // 同じ曲を最初から再生（Rust側でSinkが再構築されるためSeek(0)は不要）
                if (_currentBeatmap != null)
                    AudioPlayer.PlayBeatmapAudio(_currentBeatmap);
                break;

            case RepeatMode.All:
            case RepeatMode.None:
            default:
                // RepeatMode.All: NextTrack 内で末尾到達時に index=0 に循環
                // RepeatMode.None: NextTrack 内で末尾到達時に return して停止維持
                NextTrack();
                break;
        }
    }

    // ---- シャッフルアルゴリズム ----

    private int GetNextShuffleIndex()
    {
        // 履歴内で戻った後に再度進む場合: 履歴を再利用
        if (_shuffleIndex < _shuffleHistory.Count - 1)
        {
            _shuffleIndex++;
            return _shuffleHistory[_shuffleIndex];
        }

        // 現在曲を再生済みとしてマーク
        _playedIndices.Add(_currentTrackIndex);

        // AudioFilenameが空でなく、まだ再生されていないインデックスを候補として収集（再利用バッファ）
        _shuffleCandidates.Clear();
        CollectCandidates(_shuffleCandidates);

        // 候補が無い場合
        if (_shuffleCandidates.Count == 0)
        {
            if (RepeatMode == RepeatMode.All)
            {
                // 全リピート時: 再生済みフラグをリセットして再収集
                _playedIndices.Clear();
                _playedIndices.Add(_currentTrackIndex);
                CollectCandidates(_shuffleCandidates);
            }

            // それでも候補がなければ現在のインデックスを返す
            if (_shuffleCandidates.Count == 0)
                return _currentTrackIndex;
        }

        // 候補からランダムに選択
        int next = _shuffleCandidates[Random.Shared.Next(_shuffleCandidates.Count)];

        _shuffleHistory.Add(next);
        _shuffleIndex = _shuffleHistory.Count - 1;
        return next;
    }

    /// <summary>
    /// AudioFilename が空でなく、まだ再生されていないインデックスを <paramref name="result"/> に追加する。
    /// 呼び出し前に result.Clear() 済みであること。
    /// </summary>
    private void CollectCandidates(List<int> result)
    {
        for (int i = 0; i < _filteredBeatmaps.Length; i++)
        {
            if (_playedIndices.Contains(i)) continue;
            if (!IsPlayableBeatmap(_filteredBeatmaps[i])) continue;
            result.Add(i);
        }
    }

    /// <summary>
    /// AudioFilename が .ogg または .mp3 であれば再生可能と判定する。
    /// </summary>
    private static bool IsPlayableBeatmap(Beatmap beatmap)
    {
        if (string.IsNullOrEmpty(beatmap.AudioFilename)) return false;
        var ext = Path.GetExtension(beatmap.AudioFilename.AsSpan());
        return ext.Equals(".ogg", StringComparison.OrdinalIgnoreCase)
            || ext.Equals(".mp3", StringComparison.OrdinalIgnoreCase);
    }

    private int GetPreviousShuffleIndex()
    {
        if (_shuffleIndex > 0)
        {
            _shuffleIndex--;
            return _shuffleHistory[_shuffleIndex];
        }
        // 履歴先頭では現在曲を維持
        return _currentTrackIndex;
    }

    // ---- ナビゲーション ----

    private void NavigateToTrack(int filteredIndex)
    {
        if (filteredIndex < 0 || filteredIndex >= _filteredBeatmaps.Length) return;
        _navigateCallback?.Invoke(filteredIndex);
    }

    // ---- ユーティリティ ----

    /// <summary>
    /// 秒数を "m:ss" 形式の文字列に変換する。
    /// NaN / Infinity / 0以下はすべて "0:00" を返す。
    /// </summary>
    public static string FormatTime(double seconds)
    {
        if (double.IsNaN(seconds) || double.IsInfinity(seconds) || seconds <= 0)
            return "0:00";
        var ts = TimeSpan.FromSeconds(seconds);
        return $"{(int)ts.TotalMinutes}:{ts.Seconds:D2}";
    }

    // ---- Dispose ----

    public override void Dispose()
    {
        StopPositionPolling();
        BackgroundImage?.Dispose();
        BackgroundImage = null;
        base.Dispose();
    }
}
