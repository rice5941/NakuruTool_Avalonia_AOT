using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace NakuruTool_Avalonia_AOT.Features.BeatmapGenerator;

/// <summary>
/// libmp3lame.dll を P/Invoke でラップした MP3 エンコーダ。
/// SoundTouch.cs と同一のパターン（IDisposable + ファイナライザ + ネスト NativeMethods）。
/// </summary>
public sealed partial class LameMp3Encoder : IDisposable
{
    internal const string LameLibrary = "libmp3lame.dll";

    private IntPtr _handle;
    private bool _disposed;

    #region Native Methods

    private static partial class NativeMethods
    {
        [LibraryImport(LameLibrary, EntryPoint = "lame_init")]
        [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
        internal static partial IntPtr LameInit();

        [LibraryImport(LameLibrary, EntryPoint = "lame_set_num_channels")]
        [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
        internal static partial int LameSetNumChannels(IntPtr handle, int channels);

        [LibraryImport(LameLibrary, EntryPoint = "lame_set_in_samplerate")]
        [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
        internal static partial int LameSetInSampleRate(IntPtr handle, int sampleRate);

        [LibraryImport(LameLibrary, EntryPoint = "lame_set_VBR")]
        [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
        internal static partial int LameSetVBR(IntPtr handle, int vbrMode);

        [LibraryImport(LameLibrary, EntryPoint = "lame_set_VBR_quality")]
        [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
        internal static partial int LameSetVBRQuality(IntPtr handle, float quality);

        [LibraryImport(LameLibrary, EntryPoint = "lame_init_params")]
        [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
        internal static partial int LameInitParams(IntPtr handle);

        [LibraryImport(LameLibrary, EntryPoint = "lame_encode_buffer_interleaved_ieee_float")]
        [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
        internal static partial int LameEncodeBufferInterleavedIeeeFloat(
            IntPtr handle, float[] pcmBuffer, int numSamples,
            byte[] mp3Buffer, int mp3BufferSize);

        [LibraryImport(LameLibrary, EntryPoint = "lame_encode_flush")]
        [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
        internal static partial int LameEncodeFlush(
            IntPtr handle, byte[] mp3Buffer, int mp3BufferSize);

        [LibraryImport(LameLibrary, EntryPoint = "lame_close")]
        [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
        internal static partial int LameClose(IntPtr handle);

        [LibraryImport(LameLibrary, EntryPoint = "lame_get_lametag_frame")]
        [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
        internal static partial nint LameGetLameTagFrame(IntPtr handle, byte[] buffer, nint size);
    }

    #endregion

    #region Constructor

    /// <summary>LAME エンコーダの初期化。</summary>
    /// <param name="channels">チャンネル数（1 or 2）</param>
    /// <param name="sampleRate">サンプルレート（Hz）</param>
    /// <param name="vbrQuality">VBR品質（0=最高, 9=最低）。デフォルト 4</param>
    public LameMp3Encoder(int channels, int sampleRate, int vbrQuality = 4)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(sampleRate);
        ArgumentOutOfRangeException.ThrowIfLessThan(vbrQuality, 0);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(vbrQuality, 9);
        if (channels is not 1 and not 2)
            throw new NotSupportedException($"MP3 エンコードは 1ch / 2ch のみ対応です: {channels}ch");

        _handle = NativeMethods.LameInit();
        if (_handle == IntPtr.Zero)
            throw new InvalidOperationException("LAME エンコーダの初期化に失敗しました");

        try
        {
            ThrowIfLameError(NativeMethods.LameSetNumChannels(_handle, channels), "チャンネル数設定");
            ThrowIfLameError(NativeMethods.LameSetInSampleRate(_handle, sampleRate), "サンプルレート設定");
            ThrowIfLameError(NativeMethods.LameSetVBR(_handle, 4), "VBRモード設定"); // vbr_mtrh = 4
            ThrowIfLameError(NativeMethods.LameSetVBRQuality(_handle, vbrQuality), "VBR品質設定");

            ThrowIfLameError(NativeMethods.LameInitParams(_handle), "パラメータ初期化");
        }
        catch
        {
            NativeMethods.LameClose(_handle);
            _handle = IntPtr.Zero;
            throw;
        }
    }

    /// <summary>
    /// Finalizes an instance of the <see cref="LameMp3Encoder"/> class.
    /// </summary>
    ~LameMp3Encoder() => Dispose(disposing: false);

    #endregion

    #region Public API

    /// <summary>
    /// インターリーブ IEEE float PCM データを MP3 にエンコード。
    /// </summary>
    /// <param name="pcmBuffer">入力PCM（float, インターリーブ）</param>
    /// <param name="numFrames">フレーム数（= サンプル数 / チャンネル数）</param>
    /// <param name="mp3Buffer">出力MP3バッファ</param>
    /// <returns>書き込まれたバイト数。エラー時は例外</returns>
    public int Encode(float[] pcmBuffer, int numFrames, byte[] mp3Buffer)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var result = NativeMethods.LameEncodeBufferInterleavedIeeeFloat(
            _handle, pcmBuffer, numFrames, mp3Buffer, mp3Buffer.Length);
        ThrowIfLameError(result, "MP3 エンコード");
        return result;
    }

    /// <summary>エンコーダ内の残りデータをフラッシュ。</summary>
    /// <param name="mp3Buffer">出力MP3バッファ</param>
    /// <returns>書き込まれたバイト数。エラー時は例外</returns>
    public int Flush(byte[] mp3Buffer)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var result = NativeMethods.LameEncodeFlush(_handle, mp3Buffer, mp3Buffer.Length);
        ThrowIfLameError(result, "MP3 フラッシュ");
        return result;
    }

    /// <summary>
    /// VBR Xing タグフレームを取得する。Flush() 後に呼び出すこと。
    /// 戻り値のバイト数分を MP3 ファイルの先頭（オフセット 0）に書き込むことで、
    /// プレーヤーが正確な再生時間を計算できるようになる。
    /// </summary>
    /// <param name="tagBuffer">タグフレームを受け取るバッファ（数百バイト程度）</param>
    /// <returns>書き込まれたバイト数。0 の場合はタグなし</returns>
    public int GetLameTagFrame(byte[] tagBuffer)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return (int)NativeMethods.LameGetLameTagFrame(_handle, tagBuffer, tagBuffer.Length);
    }

    #endregion

    #region IDisposable Support

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    private void Dispose(bool disposing)
    {
        if (_disposed)
            return;

        if (_handle != IntPtr.Zero)
        {
            NativeMethods.LameClose(_handle);
            _handle = IntPtr.Zero;
        }

        _disposed = true;
    }

    #endregion

    private static void ThrowIfLameError(int result, string operation)
    {
        if (result >= 0)
            return;

        throw new InvalidOperationException($"LAME の {operation} に失敗しました (code: {result})");
    }
}
