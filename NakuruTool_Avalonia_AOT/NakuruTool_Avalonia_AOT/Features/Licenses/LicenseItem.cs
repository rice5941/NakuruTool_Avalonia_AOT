namespace NakuruTool_Avalonia_AOT.Features.Licenses;

/// <summary>
/// ライセンス情報を保持するモデル
/// </summary>
public sealed class LicenseItem
{
    /// <summary>
    /// パッケージ名
    /// </summary>
    public required string PackageName { get; init; }

    /// <summary>
    /// バージョン
    /// </summary>
    public required string Version { get; init; }

    /// <summary>
    /// ライセンス種別（例: MIT, Apache-2.0など）
    /// </summary>
    public required string LicenseType { get; init; }

    /// <summary>
    /// プロジェクトURL
    /// </summary>
    public string? Url { get; init; }

    /// <summary>
    /// 著作権表示
    /// </summary>
    public string? Copyright { get; init; }
}
