using Avalonia.Controls;

namespace NakuruTool_Avalonia_AOT.Features.Licenses;

/// <summary>
/// ライセンス全文表示ウィンドウの ViewModel
/// </summary>
public sealed class LicenseTextWindowViewModel
{
    public string Title { get; }
    public string LicenseText { get; }

    public LicenseTextWindowViewModel(string title, string licenseText)
    {
        Title = title;
        LicenseText = licenseText;
    }
}
