using System.Diagnostics;

namespace NakuruTool_Avalonia_AOT.Features.Shared;

/// <summary>
/// explorer.exe 起動用 <see cref="ProcessStartInfo"/> を生成するヘルパー。
/// パスにスペース・カンマ・括弧などが含まれていても <see cref="ProcessStartInfo.ArgumentList"/>
/// で 1 つの引数として安全に渡せるようにする。
/// </summary>
internal static class ExplorerProcessStartInfoFactory
{
    /// <summary>
    /// 指定フォルダを explorer.exe で開くための <see cref="ProcessStartInfo"/> を生成する。
    /// </summary>
    public static ProcessStartInfo CreateOpenFolder(string folderPath)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "explorer.exe",
            UseShellExecute = false,
        };
        // 文字列結合で argument を組み立てると、スペース・カンマ・括弧入りパスが
        // explorer.exe 側で複数引数として解釈され、結果として既定のドキュメントフォルダが
        // 開かれてしまう。ArgumentList を使えば各要素は 1 引数として正しくクォートされる。
        psi.ArgumentList.Add(folderPath);
        return psi;
    }
}
