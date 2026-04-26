namespace NakuruTool_Avalonia_AOT.Features.BeatmapGenerator;

public interface IOsbFileRateConverter
{
    /// <summary>
    /// .osb ファイルをレート変換し、出力先パスに書き出す。
    /// </summary>
    void Convert(string sourceOsbPath, string destinationOsbPath, OsbFileConvertOptions options);
}
