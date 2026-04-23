using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NakuruTool_Avalonia_AOT.Features.BeatmapGenerator;

/// <summary>
/// ffmpeg プロセスの実行ユーティリティ。
/// </summary>
internal static class FfmpegProcessRunner
{
    private const int StderrTailLimit = 8192;

    /// <summary>
    /// ffmpeg プロセスを実行。exit code 0 で正常終了、非 0 なら <see cref="FfmpegExecutionException"/>。
    /// </summary>
    internal static async Task RunFfmpegAsync(
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken)
    {
        var psi = CreateStartInfo(FfmpegBinaryLocator.GetFfmpegPath(), arguments, redirectStdout: false);
        using var process = new Process { StartInfo = psi };

        var stderrTail = new StringBuilder();
        process.ErrorDataReceived += (_, e) => AppendWithLimit(stderrTail, e.Data);

        if (!process.Start())
        {
            throw new FfmpegExecutionException(-1, "Failed to start ffmpeg process.");
        }
        process.BeginErrorReadLine();

        using (cancellationToken.Register(() => TryKill(process)))
        {
            try
            {
                await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                TryKill(process);
                throw;
            }
        }

        if (process.ExitCode != 0)
        {
            throw new FfmpegExecutionException(process.ExitCode, stderrTail.ToString());
        }
    }

    private static ProcessStartInfo CreateStartInfo(string fileName, IReadOnlyList<string> arguments, bool redirectStdout)
    {
        var psi = new ProcessStartInfo
        {
            FileName = fileName,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = redirectStdout,
            RedirectStandardError = true,
            StandardErrorEncoding = Encoding.UTF8,
        };
        if (redirectStdout)
        {
            psi.StandardOutputEncoding = Encoding.UTF8;
        }
        for (int i = 0; i < arguments.Count; i++)
        {
            psi.ArgumentList.Add(arguments[i]);
        }
        return psi;
    }

    private static void AppendWithLimit(StringBuilder sb, string? data)
    {
        if (string.IsNullOrEmpty(data)) return;
        sb.Append(data);
        sb.Append('\n');
        int over = sb.Length - StderrTailLimit;
        if (over > 0)
        {
            sb.Remove(0, over);
        }
    }

    private static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch
        {
            // swallow
        }
    }
}
