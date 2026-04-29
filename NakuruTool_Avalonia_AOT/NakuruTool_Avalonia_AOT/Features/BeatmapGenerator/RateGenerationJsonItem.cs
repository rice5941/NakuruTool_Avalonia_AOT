namespace NakuruTool_Avalonia_AOT.Features.BeatmapGenerator;

/// <summary>
/// レート生成で <c>tempDir</c> に出力された <c>.osu</c> から構築する、
/// 後段の JSON writer に渡すための内部値オブジェクト。
/// ImportExport の DTO には依存させず BeatmapGenerator 内で完結させる。
/// </summary>
public sealed record RateGenerationJsonItem
{
    /// <summary>楽曲タイトル。</summary>
    public required string Title { get; init; }

    /// <summary>アーティスト名。</summary>
    public required string Artist { get; init; }

    /// <summary>難易度名。</summary>
    public required string Version { get; init; }

    /// <summary>譜面作者名。</summary>
    public required string Creator { get; init; }

    /// <summary>キー数 (CircleSize)。</summary>
    public double Cs { get; init; }

    /// <summary>ビートマップセット ID (欠落時は <c>-1</c>)。</summary>
    public int BeatmapsetId { get; init; }

    /// <summary>生成 <c>.osu</c> ファイルの MD5 ハッシュ (小文字 16 進)。</summary>
    public required string Md5 { get; init; }
}
