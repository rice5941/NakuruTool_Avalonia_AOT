using CommunityToolkit.Mvvm.ComponentModel;
using NakuruTool_Avalonia_AOT.Features.Translate;
using System.Globalization;

namespace NakuruTool_Avalonia_AOT.Features.ImportExport.Models;

/// <summary>beatmap の表示専用行モデル（Missing = DB未存在）</summary>
public sealed partial class ImportExportBeatmapItem : ObservableObject
{
    public int BeatmapSetId { get; init; }
    public int KeyCount { get; init; }
    public string Title { get; init; } = string.Empty;
    public string TitleUnicode { get; init; } = string.Empty;
    public string Artist { get; init; } = string.Empty;
    public string ArtistUnicode { get; init; } = string.Empty;
    public string Version { get; init; } = string.Empty;
    public string Creator { get; init; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Exists))]
    [NotifyPropertyChangedFor(nameof(CanDownload))]
    public partial BeatmapDownloadState DownloadState { get; set; } = BeatmapDownloadState.NotExists;

    /// <summary>ダウンロード失敗の理由 (Error 状態のとき)。</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ErrorMessage))]
    public partial DownloadError? Error { get; set; }

    /// <summary>代替経路でダウンロードした場合、その直接ダウンロード URL。</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DownloadedTip))]
    public partial string? FallbackUrl { get; set; }

    /// <summary>ダウンロード完了アイコンのツールチップ。代替経路を使った場合のみ URL を表示する。</summary>
    public string? DownloadedTip => FallbackUrl is null
        ? null
        : string.Format(CultureInfo.CurrentCulture,
            LanguageService.Instance.GetString("ImportExport.DownloadStatus.DownloadedFallbackTip"), FallbackUrl);

    /// <summary>失敗理由を現在の言語で表した文言 (ツールチップ用)。</summary>
    public string? ErrorMessage => Error?.ToMessage();

    /// <summary>言語切り替え時に、翻訳を含む表示 (<see cref="ErrorMessage"/> など) を再評価させる。</summary>
    public void RefreshLocalizedText()
    {
        if (Error is not null)
            OnPropertyChanged(nameof(ErrorMessage));
        if (FallbackUrl is not null)
            OnPropertyChanged(nameof(DownloadedTip));
    }

    /// <summary>Waiting 状態のときの残り時間の短い表示 (例: "3s")。</summary>
    [ObservableProperty]
    public partial string? WaitCountdown { get; set; }

    /// <summary>Waiting 状態のときに、何を待っているかの説明 (ツールチップ用)。</summary>
    [ObservableProperty]
    public partial string? WaitMessage { get; set; }

    /// <summary>DBに存在する場合 true</summary>
    public bool Exists => DownloadState == BeatmapDownloadState.Exists;

    /// <summary>ダウンロード可能な場合 true</summary>
    public bool CanDownload => BeatmapSetId > 0 && (DownloadState == BeatmapDownloadState.NotExists || DownloadState == BeatmapDownloadState.Error);
}
