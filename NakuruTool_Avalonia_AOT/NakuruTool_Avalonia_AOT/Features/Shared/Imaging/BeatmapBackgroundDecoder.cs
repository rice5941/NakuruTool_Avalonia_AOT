using System;
using System.IO;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using NakuruTool_Avalonia_AOT.Features.AudioPlayer;
using SkiaSharp;

namespace NakuruTool_Avalonia_AOT.Features.Shared.Imaging;

/// <summary>
/// 譜面の背景画像を、表示サイズに合わせて縮小しながら読み込む（一覧のサムネイル・オーディオパネル共通）。
/// ファイル I/O とデコードを行うので、UI スレッド以外から呼ぶこと。
/// </summary>
internal static class BeatmapBackgroundDecoder
{
    /// <summary>
    /// .osu の [Events] から背景画像のファイル名を読み、実在する絶対パスを返す。背景なし・ファイルなしは null。
    /// </summary>
    public static string? ResolveBackgroundPath(string songDirectory, string osuFilePath)
    {
        var filename = OsuFileParser.GetBackgroundFilename(osuFilePath);
        if (string.IsNullOrEmpty(filename)) return null;

        var path = Path.Combine(songDirectory, filename);
        return File.Exists(path) ? path : null;
    }

    /// <summary>
    /// 背景画像のパス解決と縮小デコードをまとめて行う。背景なし・読み込み失敗は null。
    /// </summary>
    public static Bitmap? Load(string songDirectory, string osuFilePath, int maxWidth)
    {
        try
        {
            var path = ResolveBackgroundPath(songDirectory, osuFilePath);
            return path is null ? null : DecodeToWidth(path, maxWidth);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// 画像を幅 <paramref name="maxWidth"/>（元画像より大きくはしない）に縮小してデコードする。
    /// <para>
    /// <c>new Bitmap(path)</c> / <c>Bitmap.DecodeToWidth(Stream)</c> はファイル全体をマネージド配列（ファイルサイズを
    /// 2^n に切り上げた ArrayPool の配列 = 85KB 超で LOH）へコピーしてから Skia に渡すため使わない。
    /// ここではファイルを <see cref="SKData.Create(string)"/> でネイティブ側にメモリマップし、デコード・縮小のバッファも
    /// すべてネイティブメモリに置くので、マネージドヒープ（LOH）を通らない。
    /// </para>
    /// JPEG はデコード時に 1/2・1/4・1/8 へ縮小できる（<see cref="SKCodec.GetScaledDimensions"/>）ので、原寸のデコードも避けられる。
    /// </summary>
    public static Bitmap? DecodeToWidth(string path, int maxWidth)
    {
        using var data = SKData.Create(path);
        if (data is null) return null;
        using var codec = SKCodec.Create(data);
        if (codec is null) return null;

        var source = codec.Info;
        if (source.Width <= 0 || source.Height <= 0) return null;

        var width = Math.Clamp(maxWidth, 1, source.Width);
        var height = Math.Max(1, (int)Math.Round(source.Height * (double)width / source.Width));

        var decodeSize = codec.GetScaledDimensions((float)width / source.Width);
        var decodeInfo = new SKImageInfo(decodeSize.Width, decodeSize.Height, SKColorType.Bgra8888, SKAlphaType.Premul);
        using var decoded = new SKBitmap(decodeInfo);
        var result = codec.GetPixels(decodeInfo, decoded.GetPixels());
        if (result is not (SKCodecResult.Success or SKCodecResult.IncompleteInput)) return null;

        // 縮小結果を WriteableBitmap（Skia のネイティブバッファ）へ直接書き込む（中間コピーなし）
        var bitmap = new WriteableBitmap(new PixelSize(width, height), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Premul);
        try
        {
            bool scaled;
            using (var frame = bitmap.Lock())
            {
                var targetInfo = new SKImageInfo(width, height, SKColorType.Bgra8888, SKAlphaType.Premul);
                using var target = new SKPixmap(targetInfo, frame.Address, frame.RowBytes);
                // 2 倍以上縮める（PNG など、デコード時に縮小できない形式）ときだけ mipmap でエイリアシングを抑える。
                // JPEG はデコード時点で 2 倍未満まで縮んでいるので、mipmap 生成のコストを省く。
                var mipmap = decodeSize.Width >= width * 2 ? SKMipmapMode.Linear : SKMipmapMode.None;
                scaled = decoded.ScalePixels(target, new SKSamplingOptions(SKFilterMode.Linear, mipmap));
            }

            if (scaled) return bitmap;
            bitmap.Dispose();
            return null;
        }
        catch
        {
            bitmap.Dispose();
            throw;
        }
    }
}
