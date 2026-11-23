using Avalonia.Threading;
using NakuruTool_Avalonia_AOT.Features.Analyze7k;
using NakuruTool_Avalonia_AOT.Features.Settings;
using NakuruTool_Avalonia_AOT.Features.Translate;
using R3;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using ZLinq;

[assembly: InternalsVisibleTo("NakuruTool_Avalonia_AOT.Tests")]

namespace NakuruTool_Avalonia_AOT.Features.OsuDatabase;
public interface IDatabaseService : IDisposable
{
    Observable<DatabaseLoadProgress> CollectionDbProgress { get; }
    Observable<DatabaseLoadProgress> OsuDbProgress { get; }
    Observable<DatabaseLoadProgress> ScoresDbProgress { get; }
    List<OsuCollection> OsuCollections { get; }
    Beatmap[] Beatmaps { get; }
    Task LoadDatabasesAsync();
    Task ReloadDatabasesAsync();
    Task ReloadCollectionDbAsync();
    bool TryGetBeatmapByMd5(string md5Hash, out Beatmap? beatmap);

    /// <summary>
    /// 指定譜面のローカルスコア（mania のみ、ReplayScore 降順）を返す。無ければ空。
    /// </summary>
    ReadOnlySpan<LocalScore> GetLocalScores(string md5Hash);

    /// <summary>
    /// 7K 譜面の解析結果を <see cref="Beatmaps"/> に反映する（UI スレッドから呼ぶこと）。
    /// 値が変わった譜面の MD5 を返す。
    /// </summary>
    IReadOnlySet<string> ApplyChartAnalyses(Func<Beatmap, ManiaChartAnalysis?> resolver);
}

/// <summary>
/// データベース読み込み進捗情報
/// </summary>
public record DatabaseLoadProgress(string Message, int Progress);

public class DatabaseService: IDatabaseService
{
    private readonly ISettingsService _settingsService;
    
    private List<OsuCollection> _osuCollections = new();
    private Beatmap[] _beatmaps = Array.Empty<Beatmap>();
    private Dictionary<string, int>? _beatmapIndex;
    private LocalScoreStore _localScores = LocalScoreStore.Empty;
    private readonly object _lockObject = new object();

    // R3 Subjectによる進捗通知
    private readonly Subject<DatabaseLoadProgress> _collectionDbProgress = new();
    private readonly Subject<DatabaseLoadProgress> _osuDbProgress = new();
    private readonly Subject<DatabaseLoadProgress> _scoresDbProgress = new();

    public Observable<DatabaseLoadProgress> CollectionDbProgress => _collectionDbProgress;
    public Observable<DatabaseLoadProgress> OsuDbProgress => _osuDbProgress;
    public Observable<DatabaseLoadProgress> ScoresDbProgress => _scoresDbProgress;

    public List<OsuCollection> OsuCollections => _osuCollections;
    public Beatmap[] Beatmaps => _beatmaps;

    public DatabaseService(ISettingsService settingsService)
    {
        _settingsService = settingsService;
    }

    /// <summary>
    /// collection.db / osu!.db / scores.dbを並列で読み込む
    /// </summary>
    public async Task LoadDatabasesAsync()
    {
        var settings = _settingsService.SettingsData;
        var osuFolderPath = settings.OsuFolderPath;
        
        if (string.IsNullOrWhiteSpace(osuFolderPath) || Directory.Exists(osuFolderPath) == false)
        {
            var message = string.Format(LanguageService.Instance.GetString("Loading.FolderNotFound"), osuFolderPath);
            throw new DirectoryNotFoundException(message);
        }

        var collectionDbPath = Path.Combine(osuFolderPath, "collection.db");
        var osuDbPath = Path.Combine(osuFolderPath, "osu!.db");
        var scoresDbPath = Path.Combine(osuFolderPath, "scores.db");

        // osu! で Songs フォルダの場所（BeatmapDirectory）を変えた後でも合うよう、読み込みのたびに設定を読み直す
        OsuSongsFolder.Refresh(osuFolderPath);

        // 旧ストアは新しい譜面インデックスと対応しないため先に破棄
        _localScores = LocalScoreStore.Empty;

        // collection.dbのバックアップを作成（起動時のみ）
        await CreateBackupAsync(collectionDbPath);

        // 3つのDBファイルを並列で読み込み開始
        var collectionTask = Task.Run(() => ReadCollectionDbAsync(collectionDbPath));
        var osuDbTask = Task.Run(() => ReadOsuDbAsync(osuDbPath));
        var scoresTask = Task.Run(() => ReadScoresDbFileAsync(scoresDbPath));

        // すべての読み込みを待機
        await Task.WhenAll(collectionTask, osuDbTask, scoresTask);

        // 結果を取得
        _osuCollections = await collectionTask;
        _beatmaps = await osuDbTask;
        _beatmapIndex = BuildBeatmapIndex(_beatmaps);
        var scoresDb = await scoresTask;

        // データベース読み込み完了後、スコアデータを適用
        if (scoresDb != null && _beatmaps != null && _beatmaps.Length > 0)
        {
            // UIスレッドで進捗表示を更新するためにawaitを使用
            await Task.Run(() =>
            {
                var store = LocalScoreStore.Build(scoresDb, _beatmapIndex!, _beatmaps.Length);
                ApplyScoresToBeatmaps(store);
                _localScores = store;
            });
        }
        else if (scoresDb == null)
        {
            // scores.dbが無い場合は、その旨を通知
            OnScoresDbProgressChanged(LanguageService.Instance.GetString("Loading.ScoresNotFound"), 100);
        }
    }

    /// <summary>
    /// collection.dbのバックアップを作成
    /// </summary>
    private async Task CreateBackupAsync(string collectionDbPath)
    {
        if (File.Exists(collectionDbPath) == false)
        {
            return;
        }

        await Task.Run(() =>
        {
            try
            {
                var backupFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "backups");
                if (Directory.Exists(backupFolder) == false)
                {
                    Directory.CreateDirectory(backupFolder);
                }

                var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                var backupPath = Path.Combine(backupFolder, $"collection_startup_{timestamp}.db");

                File.Copy(collectionDbPath, backupPath, true);
            }
            catch (Exception)
            {
                // バックアップ失敗は無視
            }
        });
    }

    /// <summary>
    /// collection.dbファイルを読み込む
    /// </summary>
    private async Task<List<OsuCollection>> ReadCollectionDbAsync(string filePath)
    {
        return await Task.Run(() =>
        {
            if (File.Exists(filePath) == false)
            {
                OnCollectionDbProgressChanged(LanguageService.Instance.GetString("Loading.CollectionNotFound"), 0);
                return new List<OsuCollection>();
            }

            try
            {
                using var parser = new CollectionDbParser();
                var collections = parser.ReadCollectionDb(filePath, (message, progress) =>
                {
                    OnCollectionDbProgressChanged(message, progress);
                });

                return collections;
            }
            catch (Exception ex)
            {
                var errorMessage = string.Format(LanguageService.Instance.GetString("Loading.CollectionError") ?? "Error loading collection.db: {0}", ex.Message);
                OnCollectionDbProgressChanged(errorMessage, 0);
                return new List<OsuCollection>();
            }
        });
    }

    /// <summary>
    /// osu!.dbファイルを読み込む（チャンク処理版: LOH回避 + 並列変換）
    /// </summary>
    private async Task<Beatmap[]> ReadOsuDbAsync(string filePath)
    {
        return await Task.Run(() =>
        {
            if (File.Exists(filePath) == false)
            {
                OnOsuDbProgressChanged(LanguageService.Instance.GetString("Loading.OsuNotFound"), 0);
                return Array.Empty<Beatmap>();
            }

            try
            {
                using var parser = new OsuDbParser();

                var beatmapArray = parser.ReadAndProcessChunked(filePath, (message, progress) =>
                {
                    OnOsuDbProgressChanged(message, progress);
                });

                return beatmapArray;
            }
            catch (Exception ex)
            {
                var errorMessage = string.Format(LanguageService.Instance.GetString("Loading.OsuError"), ex.Message);
                OnOsuDbProgressChanged(errorMessage, 0);
                throw;
            }
        });
    }


    /// <summary>
    /// scores.dbファイルを読み込んでパース結果を返す
    /// </summary>
    private async Task<ScoresDbParseResult?> ReadScoresDbFileAsync(string filePath)
    {
        return await Task.Run(() =>
        {
            try
            {
                if (File.Exists(filePath) == false)
                {
                    return null;
                }

                using var parser = new ScoresDbParser();
                return parser.ReadScoresDb(filePath, (message, progress) =>
                {
                    OnScoresDbProgressChanged(message, progress);
                });
            }
            catch (Exception)
            {
                return null;
            }
        });
    }

    /// <summary>
    /// ローカルスコアストアから譜面ごとのベスト値・プレイ回数を集計して Beatmap に適用
    /// </summary>
    private void ApplyScoresToBeatmaps(LocalScoreStore store)
    {
        if (_beatmaps == null || _beatmaps.Length == 0)
            return;

        var total = _beatmaps.Length;
        var applied = 0;

        OnScoresDbProgressChanged(string.Format(LanguageService.Instance.GetString("Loading.ScoresApplying"), 0, total), 50);

        for (int index = 0; index < total; index++)
        {
            var scores = store.GetScores(index);
            if (!scores.IsEmpty)
            {
                applied++;
                var beatmap = _beatmaps[index];

                // 全体値
                int bestScore = 0;
                double bestAccuracy = 0;

                // v1 (Default) Mod別値
                int bestScoreNoMod = 0, bestScoreHT = 0, bestScoreDT = 0;
                double bestAccNoMod = 0, bestAccHT = 0, bestAccDT = 0;

                // ScoreV2 Mod別値
                int bestScoreV2NoMod = 0, bestScoreV2HT = 0, bestScoreV2DT = 0;
                double bestAccV2NoMod = 0, bestAccV2HT = 0, bestAccV2DT = 0;

                foreach (ref readonly var score in scores)
                {
                    var acc = ScoreCalculator.CalculateAccuracy(score);
                    var replayScore = score.ReplayScore;

                    // 全体のベスト更新
                    if (replayScore > bestScore) bestScore = replayScore;
                    if (acc > bestAccuracy) bestAccuracy = acc;

                    var modCategory = OsuMods.Categorize(score.Mods);
                    var scoreSystem = OsuMods.CategorizeScoreSystem(score.Mods);

                    if (scoreSystem == ScoreSystemCategory.ScoreV2)
                    {
                        // ScoreV2 バケット
                        switch (modCategory)
                        {
                            case ModCategory.HalfTime:
                                if (replayScore > bestScoreV2HT) bestScoreV2HT = replayScore;
                                if (acc > bestAccV2HT) bestAccV2HT = acc;
                                break;
                            case ModCategory.DoubleTime:
                                if (replayScore > bestScoreV2DT) bestScoreV2DT = replayScore;
                                if (acc > bestAccV2DT) bestAccV2DT = acc;
                                break;
                            default:
                                if (replayScore > bestScoreV2NoMod) bestScoreV2NoMod = replayScore;
                                if (acc > bestAccV2NoMod) bestAccV2NoMod = acc;
                                break;
                        }
                    }
                    else
                    {
                        // Default (v1) バケット
                        switch (modCategory)
                        {
                            case ModCategory.HalfTime:
                                if (replayScore > bestScoreHT) bestScoreHT = replayScore;
                                if (acc > bestAccHT) bestAccHT = acc;
                                break;
                            case ModCategory.DoubleTime:
                                if (replayScore > bestScoreDT) bestScoreDT = replayScore;
                                if (acc > bestAccDT) bestAccDT = acc;
                                break;
                            default:
                                if (replayScore > bestScoreNoMod) bestScoreNoMod = replayScore;
                                if (acc > bestAccNoMod) bestAccNoMod = acc;
                                break;
                        }
                    }
                }

                _beatmaps[index] = beatmap with
                {
                    BestScore = bestScore,
                    BestAccuracy = bestAccuracy,
                    PlayCount = scores.Length,
                    BestScoreNoMod = bestScoreNoMod,
                    BestAccuracyNoMod = bestAccNoMod,
                    GradeNoMod = ScoreCalculator.CalculateGrade(bestAccNoMod),
                    BestScoreHT = bestScoreHT,
                    BestAccuracyHT = bestAccHT,
                    GradeHT = ScoreCalculator.CalculateGrade(bestAccHT),
                    BestScoreDT = bestScoreDT,
                    BestAccuracyDT = bestAccDT,
                    GradeDT = ScoreCalculator.CalculateGrade(bestAccDT),
                    BestScoreV2NoMod = bestScoreV2NoMod,
                    BestAccuracyV2NoMod = bestAccV2NoMod,
                    GradeV2NoMod = ScoreCalculator.CalculateGrade(bestAccV2NoMod),
                    BestScoreV2HT = bestScoreV2HT,
                    BestAccuracyV2HT = bestAccV2HT,
                    GradeV2HT = ScoreCalculator.CalculateGrade(bestAccV2HT),
                    BestScoreV2DT = bestScoreV2DT,
                    BestAccuracyV2DT = bestAccV2DT,
                    GradeV2DT = ScoreCalculator.CalculateGrade(bestAccV2DT)
                };
            }

            if ((index + 1) % 5000 == 0 || index == total - 1)
            {
                int progress = 50 + (int)((double)(index + 1) / total * 50);
                OnScoresDbProgressChanged(string.Format(LanguageService.Instance.GetString("Loading.ScoresApplying"), index + 1, total), progress);
            }
        }

        OnScoresDbProgressChanged(string.Format(LanguageService.Instance.GetString("Loading.ScoresCompleted"), applied), 100);
    }

    /// <summary>
    /// Beatmap配列に対するMD5→インデックス辞書を構築
    /// </summary>
    private Dictionary<string, int> BuildBeatmapIndex(Beatmap[] beatmaps)
    {
        if (beatmaps == null || beatmaps.Length == 0)
        {
            return new Dictionary<string, int>(0, StringComparer.Ordinal);
        }

        var index = new Dictionary<string, int>(beatmaps.Length, StringComparer.Ordinal);
        for (int i = 0; i < beatmaps.Length; i++)
        {
            var md5 = beatmaps[i].MD5Hash;
            if (!string.IsNullOrEmpty(md5) && !index.ContainsKey(md5))
            {
                index[md5] = i;
            }
        }

        return index;
    }

    /// <summary>
    /// MD5ハッシュでインデックスを取得（辞書検索）
    /// </summary>
    private bool TryGetBeatmapIndex(string md5Hash, out int index)
    {
        index = -1;
        if (string.IsNullOrEmpty(md5Hash) || _beatmaps == null || _beatmaps.Length == 0)
        {
            return false;
        }

        if (_beatmapIndex == null)
        {
            return false;
        }

        // TryGetValue失敗時はout indexが0に上書きされるため、戻り値をそのまま返す
        return _beatmapIndex.TryGetValue(md5Hash, out index);
    }

    /// <summary>
    /// MD5ハッシュで Beatmap を O(1) 検索する（ImportExport等から利用）
    /// </summary>
    public bool TryGetBeatmapByMd5(string md5Hash, out Beatmap? beatmap)
    {
        beatmap = null;
        if (TryGetBeatmapIndex(md5Hash, out var index))
        {
            beatmap = _beatmaps[index];
            return true;
        }
        return false;
    }

    /// <summary>
    /// 指定譜面のローカルスコア（mania のみ、ReplayScore 降順）を返す
    /// </summary>
    public ReadOnlySpan<LocalScore> GetLocalScores(string md5Hash)
    {
        if (TryGetBeatmapIndex(md5Hash, out var index))
        {
            return _localScores.GetScores(index);
        }
        return ReadOnlySpan<LocalScore>.Empty;
    }

    /// <summary>
    /// 7K 譜面の解析結果を反映する。Beatmap は不変レコードなので、値が変わった譜面だけ差し替える。
    /// </summary>
    public IReadOnlySet<string> ApplyChartAnalyses(Func<Beatmap, ManiaChartAnalysis?> resolver)
    {
        var beatmaps = _beatmaps;
        var changed = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < beatmaps.Length; i++)
        {
            var beatmap = beatmaps[i];
            var analysis = resolver(beatmap);
            if (!Equals(beatmap.ChartAnalysis, analysis))
            {
                beatmaps[i] = beatmap with { ChartAnalysis = analysis };
                changed.Add(beatmap.MD5Hash);
            }
        }
        return changed;
    }

    /// <summary>
    /// collection.db読み込み進捗を通知
    /// </summary>
    private void OnCollectionDbProgressChanged(string message, int progress)
        => PublishOnUi(_collectionDbProgress, message, progress);

    /// <summary>
    /// osu!.db読み込み進捗を通知
    /// </summary>
    private void OnOsuDbProgressChanged(string message, int progress)
        => PublishOnUi(_osuDbProgress, message, progress);

    /// <summary>
    /// scores.db読み込み進捗を通知
    /// </summary>
    private void OnScoresDbProgressChanged(string message, int progress) 
        => PublishOnUi(_scoresDbProgress, message, progress);

    private void PublishOnUi(Subject<DatabaseLoadProgress> subject, string message, int progress)
    {
        var data = new DatabaseLoadProgress(message, Math.Max(0, Math.Min(100, progress)));
        Dispatcher.UIThread.Post(() => subject.OnNext(data));
    }

    /// <summary>
    /// データベースをリロード
    /// </summary>
    public async Task ReloadDatabasesAsync()
    {
        lock (_lockObject)
        {
            _osuCollections?.Clear();
            _beatmaps = Array.Empty<Beatmap>();
            _beatmapIndex = null;
            _localScores = LocalScoreStore.Empty;
        }

        await LoadDatabasesAsync();
    }

    /// <summary>
    /// collection.db のみ再読込
    /// </summary>
    public async Task ReloadCollectionDbAsync()
    {
        var settings = _settingsService.SettingsData;
        var osuFolderPath = settings.OsuFolderPath;

        if (string.IsNullOrWhiteSpace(osuFolderPath) || Directory.Exists(osuFolderPath) == false)
        {
            var message = string.Format(LanguageService.Instance.GetString("Loading.FolderNotFound"), osuFolderPath);
            throw new DirectoryNotFoundException(message);
        }

        var collectionDbPath = Path.Combine(osuFolderPath, "collection.db");
        var collections = await ReadCollectionDbAsync(collectionDbPath);

        lock (_lockObject)
        {
            _osuCollections = collections;
        }
    }

    public void Dispose()
    {
        _collectionDbProgress.Dispose();
        _osuDbProgress.Dispose();
        _scoresDbProgress.Dispose();
    }
}
