using System;
using System.Collections.Generic;
using System.Globalization;

namespace NakuruTool_Avalonia_AOT.Features.BeatmapGenerator;

/// <summary>
/// Storyboard 行を rate に応じて時間軸変換する共有 static helper。
/// <c>.osu</c> の <c>[Events]</c> セクションから利用される。
/// 入力 raw token を保持し、canonical 化はしない（<c>5</c> なら <c>5</c>、<c>Sample</c> なら <c>Sample</c>）。
/// </summary>
internal static class StoryboardLineRateTransformer
{
    /// <summary>
    /// top-level event 宣言行（先頭がインデントでない行）を変換する。
    /// Animation の <c>frameDelay</c> は変換しない（<c>.osu</c> 仕様）。
    /// </summary>
    /// <param name="line">変換対象の行。</param>
    /// <param name="rate">レート倍率。</param>
    /// <param name="sampleFilenameMap">Sample event の filename リネームマップ。null / 空なら無変換。</param>
    public static string TransformEventLine(
        string line,
        decimal rate,
        IReadOnlyDictionary<string, string>? sampleFilenameMap)
    {
        if (string.IsNullOrEmpty(line))
            return line;

        var parts = line.Split(',');
        if (parts.Length < 2)
            return line;

        var kind = StoryboardSyntaxHelper.ClassifyEvent(parts[0].Trim());
        var changed = false;

        switch (kind)
        {
            case StoryboardEventKind.Background:
            case StoryboardEventKind.Sprite:
                // 時刻フィールドなし
                return line;

            case StoryboardEventKind.Video:
                // Video,startTime,"filename",x,y
                changed |= TryScaleField(parts, 1, rate);
                break;

            case StoryboardEventKind.Break:
                // 2,startTime,endTime
                if (parts.Length < 3)
                    return line;
                changed |= TryScaleField(parts, 1, rate);
                changed |= TryScaleField(parts, 2, rate);
                break;

            case StoryboardEventKind.Colour:
                // 3,startTime,r,g,b
                changed |= TryScaleField(parts, 1, rate);
                break;

            case StoryboardEventKind.Sample:
                // Sample,time,layer,"filename",volume
                if (parts.Length < 4)
                {
                    if (parts.Length >= 2 && TryScaleField(parts, 1, rate))
                        return string.Join(',', parts);
                    return line;
                }
                changed |= TryScaleField(parts, 1, rate);
                if (sampleFilenameMap is { Count: > 0 } map)
                {
                    if (TryRenameSampleFilename(parts[3], map, out var renamed))
                    {
                        parts[3] = renamed;
                        changed = true;
                    }
                }
                break;

            case StoryboardEventKind.Animation:
                // Animation,layer,origin,"filename",x,y,frameCount,frameDelay,loopType
                // .osu 仕様: frameDelay はスケールしない
                break;

            case StoryboardEventKind.Unknown:
            default:
                return line;
        }

        return changed ? string.Join(',', parts) : line;
    }

    /// <summary>
    /// 時刻値を <c>/rate</c> でスケールし、四捨五入する（<c>away from zero</c>）。
    /// </summary>
    private static int ScaleTime(int value, decimal rate)
        => (int)Math.Round(value / rate, MidpointRounding.AwayFromZero);

    /// <summary>
    /// <paramref name="parts"/>[<paramref name="index"/>] を整数として scale する。
    /// 失敗時は元の文字列を維持し <c>false</c> を返す。
    /// </summary>
    private static bool TryScaleField(string[] parts, int index, decimal rate)
    {
        if ((uint)index >= (uint)parts.Length)
            return false;

        var field = parts[index];
        if (!int.TryParse(field, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
            return false;

        var scaled = ScaleTime(value, rate);
        var newField = scaled.ToString(CultureInfo.InvariantCulture);
        if (string.Equals(newField, field, StringComparison.Ordinal))
            return false;

        parts[index] = newField;
        return true;
    }

    /// <summary>
    /// Sample event の filename フィールドを map で rename する。
    /// quote 状態と元の前後空白は保持する。
    /// </summary>
    private static bool TryRenameSampleFilename(
        string field,
        IReadOnlyDictionary<string, string> map,
        out string renamed)
    {
        renamed = field;

        var trimmed = field.Trim();
        if (trimmed.Length == 0)
            return false;

        var hadQuotes = trimmed.Length >= 2 && trimmed[0] == '"' && trimmed[^1] == '"';
        var unquoted = hadQuotes ? trimmed[1..^1] : trimmed;
        var normalized = unquoted.Replace('\\', '/').Trim();

        if (!map.TryGetValue(normalized, out var newName))
            return false;

        var newField = hadQuotes ? "\"" + newName + "\"" : newName;
        if (string.Equals(newField, field, StringComparison.Ordinal))
            return false;

        renamed = newField;
        return true;
    }
}
