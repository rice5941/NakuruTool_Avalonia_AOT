using System;

namespace NakuruTool_Avalonia_AOT.Features.BeatmapGenerator;

/// <summary>
/// ffmpeg プロセスが非 0 終了コードで終了した場合に throw される例外。
/// UI へは露出させず、ログ用途のみ。
/// </summary>
internal sealed class FfmpegExecutionException : Exception
{
    internal int ExitCode { get; }
    internal string StderrTail { get; }

    internal FfmpegExecutionException(int exitCode, string stderrTail)
        : base($"ffmpeg failed (exit={exitCode}): {stderrTail}")
    {
        ExitCode = exitCode;
        StderrTail = stderrTail;
    }
}
