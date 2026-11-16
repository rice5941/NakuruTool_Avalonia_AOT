using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using NakuruTool_Avalonia_AOT.Features.OsuDatabase;
using NakuruTool_Avalonia_AOT.Features.Settings;
using NakuruTool_Avalonia_AOT.Features.Shared.Extensions;
using R3;

namespace NakuruTool_Avalonia_AOT.Features.Analyze7k;

public enum Analyze7kState
{
    /// <summary>設定で無効</summary>
    Disabled,
    /// <summary>analyze_7k.dll が読み込めない</summary>
    EngineUnavailable,
    /// <summary>DB 読み込み前</summary>
    Idle,
    Running,
    Completed,
}

/// <summary>
/// 解析の進捗。<paramref name="Processed"/> / <paramref name="Total"/> は全 7K 譜面（MD5 の重複を除く）に対する件数で、
/// 前回までに解析した譜面（キャッシュ済み・再試行しない失敗）も解析済みに数える。<paramref name="Failed"/> も同様。
/// </summary>
/// <param name="Remaining">残り時間の目安（解析中のみ。測定が足りない間は null）</param>
public sealed record Analyze7kProgress(Analyze7kState State, int Processed, int Total, int Failed, TimeSpan? Remaining = null)
{
    public static readonly Analyze7kProgress Initial = new(Analyze7kState.Idle, 0, 0, 0);
}

public interface IAnalyze7kService : IDisposable
{
    Analyze7kProgress Progress { get; }

    /// <summary>進捗の変化（UI スレッドで通知）</summary>
    Observable<Analyze7kProgress> ProgressChanged { get; }

    /// <summary>解析結果（Beatmap.ChartAnalysis）が変わった譜面の MD5（UI スレッドで通知）</summary>
    Observable<IReadOnlySet<string>> ChartAnalysesChanged { get; }

    /// <summary>DB 読み込み完了時に呼ぶ（UI スレッド）。キャッシュを反映して解析を始める。</summary>
    void OnDatabaseLoaded();

}

public static class Analyze7kServiceExtensions
{
    /// <summary>解析がもう進まない状態か（完了・無効・エンジンなし）</summary>
    public static bool IsFinished(this Analyze7kState state)
        => state is Analyze7kState.Completed or Analyze7kState.Disabled or Analyze7kState.EngineUnavailable;

    /// <summary>
    /// 解析が終わる（完了・無効・エンジンなし）まで待つ。UI スレッドで呼ぶ。
    /// 完了の通知は最後の結果を Beatmap に反映した後に届くので、戻った時点で全譜面の結果が使える。
    /// 起動時のプリセット一括生成が 7K 解析の条件を使う場合に、解析前の譜面を取りこぼさないようにする。
    /// </summary>
    public static async Task WhenAnalysisFinishedAsync(this IAnalyze7kService service, CancellationToken cancellationToken)
    {
        // Progress と ProgressChanged はどちらも UI スレッドで更新されるので、確認と購読の間に通知を取りこぼさない
        if (service.Progress.State.IsFinished())
            return;
        await service.ProgressChanged.FirstAsync(static p => p.State.IsFinished(), cancellationToken);
    }
}

/// <summary>
/// 全 7K 譜面（等速）のバックグラウンド解析。仕様: docs/ANALYZE_7K_SPEC.md
/// </summary>
public sealed class Analyze7kService : IAnalyze7kService
{
    /// <summary>C# 側の変換ロジックの版。段位ジャンルの判定などを変えたら上げる（キャッシュのキーに含める）</summary>
    public const string LogicVersion = "chart-1";

    /// <summary>段位エンジンの再帰に余裕を持たせるスタック</summary>
    private const int WorkerStackSize = 16 * 1024 * 1024;

    private static readonly TimeSpan PublishInterval = TimeSpan.FromSeconds(3);

    private readonly IDatabaseService _database;
    private readonly ISettingsService _settingsService;
    private readonly Subject<Analyze7kProgress> _progressChanged = new();
    private readonly Subject<IReadOnlySet<string>> _chartAnalysesChanged = new();
    private readonly CompositeDisposable _disposables = new();
    private readonly object _jobGate = new();

    private Analyze7kCache? _cache;
    private CancellationTokenSource? _jobCts;
    private bool _databaseLoaded;
    private int _processed;
    private int _failed;

    public Analyze7kService(IDatabaseService database, ISettingsService settingsService)
    {
        _database = database;
        _settingsService = settingsService;

        _settingsService.SettingsData
            .ObserveProperty(nameof(ISettingsData.Analyze7kEnabled))
            .Subscribe(_ => RestartIfLoaded())
            .AddTo(_disposables);
    }

    public Analyze7kProgress Progress { get; private set; } = Analyze7kProgress.Initial;

    public Observable<Analyze7kProgress> ProgressChanged => _progressChanged;

    public Observable<IReadOnlySet<string>> ChartAnalysesChanged => _chartAnalysesChanged;

    public void OnDatabaseLoaded()
    {
        _databaseLoaded = true;
        Restart();
    }

    private void RestartIfLoaded()
    {
        if (_databaseLoaded)
            Restart();
    }

    private void Restart()
    {
        CancellationTokenSource cts;
        lock (_jobGate)
        {
            _jobCts?.Cancel();
            _jobCts = cts = new CancellationTokenSource();
        }

        if (!_settingsService.SettingsData.Analyze7kEnabled)
        {
            var cleared = _database.ApplyChartAnalyses(static _ => null);
            if (cleared.Count > 0)
                _chartAnalysesChanged.OnNext(cleared);
            SetProgress(new Analyze7kProgress(Analyze7kState.Disabled, 0, 0, 0));
            return;
        }

        if (!Analyze7kNative.IsAvailable)
        {
            SetProgress(new Analyze7kProgress(Analyze7kState.EngineUnavailable, 0, 0, 0));
            return;
        }

        var snapshot = new JobSnapshot(_database.Beatmaps, _settingsService.SettingsData.OsuFolderPath, ResolveParallelism());
        _ = Task.Run(() => RunJobAsync(snapshot, cts.Token));
    }

    private int ResolveParallelism() => ResolveParallelism(_settingsService.SettingsData.Analyze7kParallelism, Environment.ProcessorCount);

    /// <summary>
    /// 並列数。設定が 0 なら論理コア数の半分（ワーカーは優先度 BelowNormal で動くので、残りのコアで画面操作が滞らない）。
    /// </summary>
    public static int ResolveParallelism(int configured, int processorCount)
        => configured > 0 ? configured : Math.Max(1, processorCount / 2);

    private sealed record JobSnapshot(Beatmap[] Beatmaps, string OsuFolder, int Parallelism);

    /// <param name="AlternatePath">フォルダ名の末尾の空白・ピリオドを除いたパス（<see cref="Analyze7kRules.AlternateChartPath"/>）</param>
    private sealed record WorkItem(string Md5, string Path, string? AlternatePath, double Difficulty);

    private async Task RunJobAsync(JobSnapshot snapshot, CancellationToken ct)
    {
        // 前回までに解析した件数（進捗の解析済み・失敗に足す）
        var previous = new PlanCounts(0, 0);
        var total = 0;
        try
        {
            var cache = EnsureCache();
            cache.ForgetRetryableFailures();
            if (ct.IsCancellationRequested)
                return;

            SetProgressFromWorker(Analyze7kState.Running, 0, 0, 0);
            await PublishAsync(snapshot, ct);

            var plan = BuildPlan(snapshot, cache, out previous);
            Interlocked.Exchange(ref _processed, 0);
            Interlocked.Exchange(ref _failed, 0);
            total = previous.Done + plan.Count;

            if (plan.Count > 0)
            {
                var estimator = new Analyze7kRemainingTimeEstimator();
                var stopwatch = Stopwatch.StartNew();
                ReportRunning(previous, total, estimator, stopwatch, plan.Count);

                var queue = new ConcurrentQueue<WorkItem>(plan);
                var workers = new List<Task>();
                for (int i = 0; i < Math.Min(snapshot.Parallelism, plan.Count); i++)
                    workers.Add(RunOnLargeStack(() => WorkerLoop(queue, cache, ct)));

                var all = Task.WhenAll(workers);
                var lastPublish = DateTime.UtcNow;
                while (!all.IsCompleted)
                {
                    await Task.WhenAny(all, Task.Delay(500, CancellationToken.None));
                    if (ct.IsCancellationRequested)
                        break;
                    ReportRunning(previous, total, estimator, stopwatch, plan.Count);
                    if (DateTime.UtcNow - lastPublish >= PublishInterval)
                    {
                        lastPublish = DateTime.UtcNow;
                        await PublishAsync(snapshot, ct);
                    }
                }
                await all;
            }

            if (ct.IsCancellationRequested)
                return;
            await PublishAsync(snapshot, ct);
            ReportFinished(previous, total);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Analyze7k job failed: {ex}");
            if (!ct.IsCancellationRequested)
                ReportFinished(previous, total);
        }
    }

    private Analyze7kCache EnsureCache()
    {
        lock (_jobGate)
        {
            if (_cache is not null)
                return _cache;

            var directory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "cache", "analyze-7k");
            var cache = new Analyze7kCache(directory, Analyze7kCache.MakeVersionKey(Analyze7kNative.EngineVersion!, LogicVersion));
            cache.Load();
            _cache = cache;
            return cache;
        }
    }

    /// <summary>前回までに解析した 7K 譜面の件数（Done は結果あり＋再試行しない失敗、Failed はそのうちの失敗）</summary>
    private readonly record struct PlanCounts(int Done, int Failed);

    /// <summary>まだ解析していない 7K 譜面を、★の高い順に並べる（上位の結果を早く表示するため）</summary>
    private static List<WorkItem> BuildPlan(JobSnapshot snapshot, Analyze7kCache cache, out PlanCounts previous)
    {
        var plan = new List<WorkItem>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        int done = 0, failed = 0;
        foreach (var beatmap in snapshot.Beatmaps)
        {
            if (beatmap.KeyCount != 7 || string.IsNullOrEmpty(beatmap.MD5Hash) || !seen.Add(beatmap.MD5Hash))
                continue;
            if (cache.TryGet(beatmap.MD5Hash, out _))
            {
                done++;
                continue;
            }
            if (cache.GetFailure(beatmap.MD5Hash) is not null)
            {
                done++;
                failed++;
                continue;
            }
            var songs = Path.Combine(snapshot.OsuFolder, "Songs");
            var path = Path.Combine(songs, beatmap.FolderName, beatmap.OsuFileName);
            var alternate = Analyze7kRules.AlternateChartPath(songs, beatmap.FolderName, beatmap.OsuFileName);
            plan.Add(new WorkItem(beatmap.MD5Hash, path, alternate, beatmap.Difficulty));
        }
        plan.Sort(static (a, b) => b.Difficulty.CompareTo(a.Difficulty));
        previous = new PlanCounts(done, failed);
        return plan;
    }

    private void WorkerLoop(ConcurrentQueue<WorkItem> queue, Analyze7kCache cache, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested && queue.TryDequeue(out var item))
        {
            if (!ProcessItem(item, cache))
                Interlocked.Increment(ref _failed);
            Interlocked.Increment(ref _processed);
        }
    }

    private static bool TryReadAllBytes(string path, out byte[] bytes)
    {
        try
        {
            bytes = File.ReadAllBytes(path);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            bytes = [];
            return false;
        }
    }

    /// <summary>1譜面を解析してキャッシュに保存する。解析できなければ false。</summary>
    private static bool ProcessItem(WorkItem item, Analyze7kCache cache)
    {
        if (!TryReadAllBytes(item.Path, out var bytes) &&
            (item.AlternatePath is null || !TryReadAllBytes(item.AlternatePath, out bytes)))
        {
            cache.AddFailure(item.Md5, Analyze7kFailure.FileMissing);
            return false;
        }

        // 中身が osu!.db の MD5 と違う .osu（譜面の更新後に osu! が読み直していない）も、osu! が遊ぶときに読むのは
        // このファイルなので、その中身を解析して osu!.db の譜面の結果にする。解析できなければ、osu! が読み直すかファイルが
        // 直れば解析できるかもしれないので、再試行する理由（md5_mismatch）で記録する。
        var stale = !string.Equals(Convert.ToHexStringLower(MD5.HashData(bytes)), item.Md5, StringComparison.OrdinalIgnoreCase);

        var reason = TryAnalyze(bytes, out var analysis);
        if (reason is null)
        {
            cache.Add(item.Md5, analysis!);
            return true;
        }
        cache.AddFailure(item.Md5, stale ? Analyze7kFailure.Md5Mismatch : reason);
        return false;
    }

    /// <summary>.osu の中身を解析する。解析できなければその理由を返す。</summary>
    private static string? TryAnalyze(byte[] bytes, out ManiaChartAnalysis? analysis)
    {
        analysis = null;
        if (!Analyze7kRules.TryReadChartHeader(bytes, out var od, out var headerReason))
            return headerReason!;

        try
        {
            analysis = Analyze7kNative.ToAnalysis(Analyze7kNative.Analyze(bytes, od));
            return null;
        }
        catch (Analyze7kEngineException ex)
        {
            return Analyze7kFailure.FromEngineMessage(ex.Message);
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException)
        {
            return Analyze7kFailure.EngineError;
        }
    }

    private static Task RunOnLargeStack(Action action)
    {
        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            try
            {
                action();
                tcs.SetResult();
            }
            catch (Exception ex)
            {
                tcs.SetException(ex);
            }
        }, WorkerStackSize)
        {
            IsBackground = true,
            Priority = ThreadPriority.BelowNormal,
            Name = "Analyze7kWorker",
        };
        thread.Start();
        return tcs.Task;
    }

    /// <summary>キャッシュの結果を Beatmap に反映する</summary>
    private async Task PublishAsync(JobSnapshot snapshot, CancellationToken ct)
    {
        var cache = _cache;
        if (cache is null || ct.IsCancellationRequested)
            return;

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            // DB が読み直された後の古いジョブの結果は反映しない
            if (ct.IsCancellationRequested || !ReferenceEquals(_database.Beatmaps, snapshot.Beatmaps))
                return;

            var changed = _database.ApplyChartAnalyses(beatmap =>
                beatmap.KeyCount == 7 && cache.TryGet(beatmap.MD5Hash, out var analysis) ? analysis : null);
            if (changed.Count > 0)
                _chartAnalysesChanged.OnNext(changed);
        });
    }

    /// <summary>解析中の進捗と残り時間を通知する</summary>
    private void ReportRunning(PlanCounts previous, int total, Analyze7kRemainingTimeEstimator estimator, Stopwatch stopwatch, int planCount)
    {
        var processed = Volatile.Read(ref _processed);
        var failed = Volatile.Read(ref _failed);
        var remaining = estimator.Update(stopwatch.Elapsed, processed, planCount - processed);
        SetProgressFromWorker(Analyze7kState.Running, previous.Done + processed, total, previous.Failed + failed, remaining);
    }

    private void ReportFinished(PlanCounts previous, int total) =>
        SetProgressFromWorker(Analyze7kState.Completed,
            previous.Done + Volatile.Read(ref _processed), total, previous.Failed + Volatile.Read(ref _failed));

    private void SetProgressFromWorker(Analyze7kState state, int processed, int total, int failed, TimeSpan? remaining = null)
    {
        var progress = new Analyze7kProgress(state, processed, total, failed, remaining);
        Dispatcher.UIThread.Post(() => SetProgress(progress));
    }

    private void SetProgress(Analyze7kProgress progress)
    {
        if (progress == Progress)
            return;
        Progress = progress;
        _progressChanged.OnNext(progress);
    }

    public void Dispose()
    {
        lock (_jobGate)
        {
            _jobCts?.Cancel();
            _cache?.Dispose();
        }
        _disposables.Dispose();
        _progressChanged.Dispose();
        _chartAnalysesChanged.Dispose();
    }
}
