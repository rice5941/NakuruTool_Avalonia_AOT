using NakuruTool_Avalonia_AOT.Features.OsuDatabase;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace NakuruTool_Avalonia_AOT.Features.ImportExport.Models;

/// <summary>JSONルート: 1コレクション = 1ファイル</summary>
public class CollectionExchangeData
{
    public string Name { get; set; } = string.Empty;
    public List<CollectionExchangeBeatmap> Beatmaps { get; set; } = new();
}

/// <summary>JSON内の1 beatmap</summary>

public class CollectionExchangeBeatmap
{
    public string Title { get; set; } = string.Empty;
    public string Artist { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public string Creator { get; set; } = string.Empty;
    public double Cs { get; set; }

    [JsonPropertyName("beatmapset_id")]
    public int BeatmapsetId { get; set; }

    public string Md5 { get; set; } = string.Empty;

    /// <summary>Beatmap → DTO変換（エクスポート時）</summary>
    public static CollectionExchangeBeatmap FromBeatmap(Beatmap beatmap) => new()
    {
        Title = beatmap.Title,
        Artist = beatmap.Artist,
        Version = beatmap.Version,
        Creator = beatmap.Creator,
        Cs = (double)beatmap.KeyCount,
        BeatmapsetId = beatmap.BeatmapSetId,
        Md5 = beatmap.MD5Hash,
    };
}

/// <summary>NativeAOT対応 JSON Source Generator</summary>
[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower)]
[JsonSerializable(typeof(CollectionExchangeData))]
[JsonSerializable(typeof(List<CollectionExchangeData>))]
[JsonSerializable(typeof(CollectionExchangeBeatmap))]
public partial class ImportExportJsonContext : JsonSerializerContext
{
}
