namespace NakuruTool_Avalonia_AOT.Features.Licenses;

/// <summary>
/// ???C?Z???X???????????f??
/// </summary>
public sealed class LicenseItem
{
    /// <summary>
    /// ?p?b?P?[?W??
    /// </summary>
    public required string PackageName { get; init; }

    /// <summary>
    /// ?o?[?W????
    /// </summary>
    public required string Version { get; init; }

    /// <summary>
    /// ???C?Z???X???i??: MIT, Apache-2.0???j
    /// </summary>
    public required string LicenseType { get; init; }

    /// <summary>
    /// ?v???W?F?N?gURL
    /// </summary>
    public string? Url { get; init; }

    /// <summary>
    /// ?????\??
    /// </summary>
    public string? Copyright { get; init; }

    /// <summary>
    /// ライセンス全文テキスト
    /// </summary>
    public string? LicenseText { get; init; }
}
