using System;
using System.Threading;
using System.Threading.Tasks;

namespace NakuruTool_Avalonia_AOT.Features.ImportExport;

/// <summary>
/// ダウンロード開始の間隔を制御する。
/// 前回の開始時刻から <c>interval</c> 以上経過するまで次の開始を待たせる
/// (ダウンロードにかかった時間も間隔に含まれる)。
/// </summary>
internal sealed class DownloadIntervalGate
{
    private readonly TimeSpan _interval;
    private readonly TimeProvider _timeProvider;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private long? _lastStartTimestamp;

    public DownloadIntervalGate(TimeSpan interval, TimeProvider? timeProvider = null)
    {
        _interval = interval;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <summary>開始してよいタイミングまで待機し、開始時刻を記録する。</summary>
    /// <param name="onWaiting">実際に待機が発生する場合に、待機時間を渡して呼ばれる。</param>
    public async Task WaitForTurnAsync(Action<TimeSpan>? onWaiting, CancellationToken ct)
    {
        await _lock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (_lastStartTimestamp is { } last)
            {
                var remaining = _interval - _timeProvider.GetElapsedTime(last);
                if (remaining > TimeSpan.Zero)
                {
                    onWaiting?.Invoke(remaining);
                    await Task.Delay(remaining, _timeProvider, ct).ConfigureAwait(false);
                }
            }
            _lastStartTimestamp = _timeProvider.GetTimestamp();
        }
        finally
        {
            _lock.Release();
        }
    }
}
