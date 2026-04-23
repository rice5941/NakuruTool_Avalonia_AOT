namespace NakuruTool_Avalonia_AOT.Features.BeatmapGenerator;

/// <summary>
/// 入力オーディオファイルから取得したメタデータ（純 C# パーサーで抽出）。
/// </summary>
internal readonly record struct AudioInputMetadata(int Channels, int SampleRate);
