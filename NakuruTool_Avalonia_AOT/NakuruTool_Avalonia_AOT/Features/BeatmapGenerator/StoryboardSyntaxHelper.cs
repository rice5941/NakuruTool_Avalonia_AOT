using System;
using System.Collections.Generic;

namespace NakuruTool_Avalonia_AOT.Features.BeatmapGenerator;

/// <summary>
/// Storyboard イベントの種別。
/// .osu / .osb の <c>[Events]</c> セクションで使用される alias を canonical 化したもの。
/// </summary>
internal enum StoryboardEventKind
{
    Unknown,
    Background,
    Video,
    Break,
    Colour,
    Sprite,
    Sample,
    Animation,
}

/// <summary>
/// Storyboard インデント付きコマンド行（<c>_F</c> / <c>_M</c> 等）の種別。
/// </summary>
internal enum StoryboardCommandKind
{
    Unknown,
    Fade,
    Move,
    MoveX,
    MoveY,
    Scale,
    VectorScale,
    Rotate,
    Colour,
    Parameter,
    Loop,
    Trigger,
}

/// <summary>
/// Storyboard 行の構文解析共通ヘルパー。
/// alias 正規化・コマンド行判定・<c>[Variables]</c> 展開を、
/// <c>OsuFileAssetParser</c> / <c>OsuFileRateConverter</c> / <c>OsbFileRateConverter</c>
/// から共有して呼び出す。
/// NativeAOT 制約に従い、リフレクション・動的コード生成・LINQ 動的式は使用しない。
/// </summary>
internal static class StoryboardSyntaxHelper
{
    /// <summary>
    /// <c>[Events]</c> 行先頭トークンを <see cref="StoryboardEventKind"/> に正規化する。
    /// 数値 alias と文字列 alias を OrdinalIgnoreCase で同一視する。
    /// </summary>
    /// <param name="token">Trim 済み前提のトークン。</param>
    public static StoryboardEventKind ClassifyEvent(string token)
    {
        if (string.IsNullOrEmpty(token))
            return StoryboardEventKind.Unknown;

        if (token.Equals("0", StringComparison.Ordinal)
            || token.Equals("Background", StringComparison.OrdinalIgnoreCase))
            return StoryboardEventKind.Background;

        if (token.Equals("1", StringComparison.Ordinal)
            || token.Equals("Video", StringComparison.OrdinalIgnoreCase))
            return StoryboardEventKind.Video;

        if (token.Equals("2", StringComparison.Ordinal)
            || token.Equals("Break", StringComparison.OrdinalIgnoreCase))
            return StoryboardEventKind.Break;

        if (token.Equals("3", StringComparison.Ordinal)
            || token.Equals("Colour", StringComparison.OrdinalIgnoreCase)
            || token.Equals("Color", StringComparison.OrdinalIgnoreCase))
            return StoryboardEventKind.Colour;

        if (token.Equals("4", StringComparison.Ordinal)
            || token.Equals("Sprite", StringComparison.OrdinalIgnoreCase))
            return StoryboardEventKind.Sprite;

        if (token.Equals("5", StringComparison.Ordinal)
            || token.Equals("Sample", StringComparison.OrdinalIgnoreCase))
            return StoryboardEventKind.Sample;

        if (token.Equals("6", StringComparison.Ordinal)
            || token.Equals("Animation", StringComparison.OrdinalIgnoreCase))
            return StoryboardEventKind.Animation;

        return StoryboardEventKind.Unknown;
    }

    /// <summary>
    /// 行が indented storyboard command 行（先頭が <c>' '</c> または <c>'_'</c>）か判定する。
    /// </summary>
    public static bool IsCommandLine(string line)
        => line.Length > 0 && (line[0] == ' ' || line[0] == '_');

    /// <summary>
    /// indented command 行を、行頭の <c>' '</c> / <c>'_'</c> 連続部分（prefix）と
    /// それ以降の本体（body）に分離する。混在も許容する（例: <c>"__F..."</c>, <c>"  F..."</c>, <c>"_ F..."</c>）。
    /// </summary>
    public static (string Prefix, string Body) SplitCommandPrefix(string line)
    {
        var i = 0;
        while (i < line.Length && (line[i] == ' ' || line[i] == '_'))
            i++;

        if (i == 0)
            return (string.Empty, line);

        return (line[..i], line[i..]);
    }

    /// <summary>
    /// indented command 行 body の先頭トークン（例 <c>F</c>, <c>MX</c>, <c>L</c>）を
    /// <see cref="StoryboardCommandKind"/> に正規化する。OrdinalIgnoreCase。
    /// </summary>
    public static StoryboardCommandKind ClassifyCommand(string token)
    {
        if (string.IsNullOrEmpty(token))
            return StoryboardCommandKind.Unknown;

        if (token.Equals("F", StringComparison.OrdinalIgnoreCase))
            return StoryboardCommandKind.Fade;
        if (token.Equals("M", StringComparison.OrdinalIgnoreCase))
            return StoryboardCommandKind.Move;
        if (token.Equals("MX", StringComparison.OrdinalIgnoreCase))
            return StoryboardCommandKind.MoveX;
        if (token.Equals("MY", StringComparison.OrdinalIgnoreCase))
            return StoryboardCommandKind.MoveY;
        if (token.Equals("S", StringComparison.OrdinalIgnoreCase))
            return StoryboardCommandKind.Scale;
        if (token.Equals("V", StringComparison.OrdinalIgnoreCase))
            return StoryboardCommandKind.VectorScale;
        if (token.Equals("R", StringComparison.OrdinalIgnoreCase))
            return StoryboardCommandKind.Rotate;
        if (token.Equals("C", StringComparison.OrdinalIgnoreCase))
            return StoryboardCommandKind.Colour;
        if (token.Equals("P", StringComparison.OrdinalIgnoreCase))
            return StoryboardCommandKind.Parameter;
        if (token.Equals("L", StringComparison.OrdinalIgnoreCase))
            return StoryboardCommandKind.Loop;
        if (token.Equals("T", StringComparison.OrdinalIgnoreCase))
            return StoryboardCommandKind.Trigger;

        return StoryboardCommandKind.Unknown;
    }

    /// <summary>
    /// <c>[Variables]</c> の longest-match 展開を 1 行に対して適用する。
    /// </summary>
    /// <param name="line">展開対象の行。</param>
    /// <param name="variables">変数名（先頭 <c>$</c> を含む）→ 値 のマップ。</param>
    /// <returns>
    /// 展開後の文字列。<c>$</c> を含むがどのキーにもマッチしない場合は <c>null</c>
    /// （呼び出し側で行スキップを判断する）。<c>$</c> を含まない場合は引数 <paramref name="line"/> をそのまま返す。
    /// </returns>
    public static string? ExpandVariables(string line, IReadOnlyDictionary<string, string> variables)
    {
        ArgumentNullException.ThrowIfNull(line);
        ArgumentNullException.ThrowIfNull(variables);

        if (line.Length == 0 || !line.Contains('$'))
            return line;

        if (variables.Count == 0)
            return null;

        // 変数名の長さ降順で並べる（最長一致のため）。LINQ は使わず Sort で実現する。
        var sorted = new List<KeyValuePair<string, string>>(variables.Count);
        foreach (var kvp in variables)
            sorted.Add(kvp);
        sorted.Sort(static (a, b) => b.Key.Length.CompareTo(a.Key.Length));

        var result = line;
        var index = 0;
        while (index < result.Length)
        {
            var dollarIndex = result.IndexOf('$', index);
            if (dollarIndex < 0)
                break;

            var matched = false;
            for (var i = 0; i < sorted.Count; i++)
            {
                var key = sorted[i].Key;
                if (result.AsSpan(dollarIndex).StartsWith(key.AsSpan(), StringComparison.Ordinal))
                {
                    var value = sorted[i].Value;
                    result = string.Concat(
                        result.AsSpan(0, dollarIndex),
                        value,
                        result.AsSpan(dollarIndex + key.Length));
                    index = dollarIndex + value.Length;
                    matched = true;
                    break;
                }
            }

            if (!matched)
                return null;
        }

        return result;
    }
}
