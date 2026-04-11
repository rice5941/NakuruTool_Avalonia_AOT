using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace NakuruTool_Avalonia_AOT.Features.BeatmapGenerator;

/// <summary>
/// nakuru_rate_audio ネイティブライブラリの P/Invoke 安全ラッパー。
/// </summary>
internal static unsafe class RateAudioInterop
{
    public enum ConvertMode
    {
        TempoOnly = 0,
        TempoPitch = 1,
    }

    public enum OutputFormat
    {
        Wav = 0,
        OggVorbis = 1,
        Mp3 = 2,
    }

    [ThreadStatic]
    private static Action<float>? s_currentProgressCallback;

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static void OnProgress(float progress)
    {
        s_currentProgressCallback?.Invoke(progress);
    }

    public static void Convert(
        string inputPath,
        string outputPath,
        double rate,
        ConvertMode mode,
        OutputFormat format,
        float quality = 0.4f,
        Action<float>? onProgress = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(inputPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);

        if (rate <= 0 || double.IsNaN(rate) || double.IsInfinity(rate))
            throw new ArgumentOutOfRangeException(nameof(rate), rate, "rate は正の有限値である必要があります。");

        var inBytes = Encoding.UTF8.GetBytes(inputPath);
        var outBytes = Encoding.UTF8.GetBytes(outputPath);

        s_currentProgressCallback = onProgress;
        try
        {
            int result;
            fixed (byte* inPtr = inBytes)
            fixed (byte* outPtr = outBytes)
            {
                result = NativeRateAudioMethods.nakuru_rate_audio_convert(
                    inPtr, inBytes.Length,
                    outPtr, outBytes.Length,
                    rate,
                    (int)mode,
                    (int)format,
                    quality,
                    onProgress is not null ? &OnProgress : null);
            }

            ThrowOnError(result);
        }
        finally
        {
            s_currentProgressCallback = null;
        }
    }

    public static string? GetLastError()
    {
        const int initialBufSize = 1024;
        var buf = stackalloc byte[initialBufSize];
        var written = NativeRateAudioMethods.nakuru_rate_audio_get_last_error(buf, initialBufSize);

        if (written == 0)
            return null;

        if (written < 0)
        {
            if (written == int.MinValue)
                return null;

            var required = -written;
            if (required == 0)
                return null;

            var managed = new byte[required];
            fixed (byte* ptr = managed)
            {
                written = NativeRateAudioMethods.nakuru_rate_audio_get_last_error(ptr, required);
            }

            return written > 0 ? Encoding.UTF8.GetString(managed, 0, written) : null;
        }

        return Encoding.UTF8.GetString(buf, written);
    }

    public static bool IsMp3Available()
    {
        return NativeRateAudioMethods.nakuru_rate_audio_is_mp3_available() == 1;
    }

    private static void ThrowOnError(int resultCode)
    {
        if (resultCode == 0)
            return;

        var message = GetLastError() ?? $"nakuru_rate_audio error code: {resultCode}";
        throw new InvalidOperationException(message);
    }

}
