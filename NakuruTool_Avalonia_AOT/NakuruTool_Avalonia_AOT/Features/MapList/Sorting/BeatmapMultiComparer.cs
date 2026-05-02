using System;
using System.Collections.Generic;
using NakuruTool_Avalonia_AOT.Features.OsuDatabase;

namespace NakuruTool_Avalonia_AOT.Features.MapList.Sorting;

/// <summary>
/// 複数の <see cref="SortRule"/> を優先順に評価して <see cref="Beatmap"/> を比較する Comparer。
/// NativeAOT 安全 (リフレクション/動的コード/ボックス化を回避)。
/// null や空文字列は方向に関わらず末尾固定。
/// </summary>
internal sealed class BeatmapMultiComparer : IComparer<Beatmap>
{
    private readonly SortRule[] _rules;
    private readonly bool _preferUnicode;
    private readonly ScoreSystemCategory _scoreSystem;
    private readonly ModCategory _mod;

    public BeatmapMultiComparer(
        SortRule[] activeRules,
        bool preferUnicode,
        ScoreSystemCategory scoreSystem,
        ModCategory mod)
    {
        _rules = activeRules ?? Array.Empty<SortRule>();
        _preferUnicode = preferUnicode;
        _scoreSystem = scoreSystem;
        _mod = mod;
    }

    public int Compare(Beatmap? x, Beatmap? y)
    {
        if (ReferenceEquals(x, y))
        {
            return 0;
        }
        if (x is null)
        {
            return 1;
        }
        if (y is null)
        {
            return -1;
        }

        for (var i = 0; i < _rules.Length; i++)
        {
            var rule = _rules[i];
            var (order, nullHandled) = CompareByField(x, y, rule.Field);
            if (order == 0)
            {
                continue;
            }

            if (!nullHandled && rule.Direction == SortDirection.Descending)
            {
                order = -order;
            }
            return order;
        }
        return 0;
    }

    private (int Order, bool NullHandled) CompareByField(Beatmap a, Beatmap b, SortField field)
        => field switch
        {
            SortField.KeyCount         => (a.KeyCount.CompareTo(b.KeyCount), false),
            SortField.Status           => (a.Status.CompareTo(b.Status), false),
            SortField.Title            => CompareText(GetTitle(a), GetTitle(b)),
            SortField.Version          => CompareText(a.Version, b.Version),
            SortField.Artist           => CompareText(GetArtist(a), GetArtist(b)),
            SortField.Creator          => CompareText(a.Creator, b.Creator),
            SortField.BPM              => (a.BPM.CompareTo(b.BPM), false),
            SortField.Difficulty       => (a.Difficulty.CompareTo(b.Difficulty), false),
            SortField.LongNoteRate     => (a.LongNoteRate.CompareTo(b.LongNoteRate), false),
            SortField.BestAccuracy     => (a.GetBestAccuracy(_scoreSystem, _mod)
                                            .CompareTo(b.GetBestAccuracy(_scoreSystem, _mod)), false),
            SortField.BestScore        => (a.GetBestScore(_scoreSystem, _mod)
                                            .CompareTo(b.GetBestScore(_scoreSystem, _mod)), false),
            SortField.LastPlayed       => CompareNullable(a.LastPlayed, b.LastPlayed),
            SortField.LastModifiedTime => CompareNullable(a.LastModifiedTime, b.LastModifiedTime),
            SortField.PlayCount        => (a.PlayCount.CompareTo(b.PlayCount), false),
            SortField.OD               => (a.OD.CompareTo(b.OD), false),
            SortField.HP               => (a.HP.CompareTo(b.HP), false),
            SortField.DrainTime        => (a.DrainTimeSeconds.CompareTo(b.DrainTimeSeconds), false),
            _                          => (0, false),
        };

    private static (int Order, bool NullHandled) CompareText(string? a, string? b)
    {
        var ae = string.IsNullOrEmpty(a);
        var be = string.IsNullOrEmpty(b);
        if (ae && be)
        {
            return (0, true);
        }
        if (ae)
        {
            return (1, true);
        }
        if (be)
        {
            return (-1, true);
        }
        return (string.Compare(a, b, StringComparison.OrdinalIgnoreCase), false);
    }

    private static (int Order, bool NullHandled) CompareNullable<T>(T? a, T? b)
        where T : struct, IComparable<T>
    {
        if (!a.HasValue && !b.HasValue)
        {
            return (0, true);
        }
        if (!a.HasValue)
        {
            return (1, true);
        }
        if (!b.HasValue)
        {
            return (-1, true);
        }
        return (a.Value.CompareTo(b.Value), false);
    }

    private string GetTitle(Beatmap b) =>
        _preferUnicode && !string.IsNullOrEmpty(b.TitleUnicode) ? b.TitleUnicode : b.Title;

    private string GetArtist(Beatmap b) =>
        _preferUnicode && !string.IsNullOrEmpty(b.ArtistUnicode) ? b.ArtistUnicode : b.Artist;
}
