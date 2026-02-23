using NakuruTool_Avalonia_AOT.Features.ImportExport.Models;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Xml.Linq;

namespace NakuruTool_Avalonia_AOT.Features.ImportExport;

/// <summary>
/// OsuCollection エクスポーター形式の XML を CollectionExchangeData にパースする。
/// System.Xml.Linq (XDocument) を使用し、リフレクション不要で NativeAOT 安全。
/// </summary>
public static class OsuCollectionXmlParser
{
    /// <summary>
    /// XML ファイルストリームから CollectionExchangeData を生成する。
    /// </summary>
    /// <returns>パース成功時は CollectionExchangeData、失敗時は null</returns>
    public static CollectionExchangeData? Parse(Stream stream)
    {
        XDocument doc;
        try
        {
            doc = XDocument.Load(stream);
        }
        catch
        {
            return null;
        }

        var root = doc.Root;
        if (root is null || root.Name.LocalName != "OsuCollection")
            return null;

        var name = root.Element("Name")?.Value?.Trim() ?? string.Empty;

        var beatmaps = new List<CollectionExchangeBeatmap>();
        var mapsElement = root.Element("Maps");
        if (mapsElement is not null)
        {
            foreach (var mapInfo in mapsElement.Elements("MapInfo"))
            {
                var beatmap = ParseMapInfo(mapInfo);
                if (beatmap is not null)
                    beatmaps.Add(beatmap);
            }
        }

        return new CollectionExchangeData
        {
            Name = name,
            Beatmaps = beatmaps
        };
    }

    /// <summary>
    /// 単一の MapInfo 要素を CollectionExchangeBeatmap に変換する。
    /// Md5Hash が空の場合は null を返す（DB照合不能のため）。
    /// </summary>
    private static CollectionExchangeBeatmap? ParseMapInfo(XElement mapInfo)
    {
        var md5 = mapInfo.Element("Md5Hash")?.Value ?? string.Empty;
        if (string.IsNullOrWhiteSpace(md5))
            return null;

        var title = mapInfo.Element("Title")?.Value ?? string.Empty;
        var difficulty = mapInfo.Element("Difficulty")?.Value ?? string.Empty;
        var creator = mapInfo.Element("Creator")?.Value ?? string.Empty;

        double cs = 0;
        var diffCsText = mapInfo.Element("DiffCS")?.Value;
        if (diffCsText is not null)
            double.TryParse(diffCsText, CultureInfo.InvariantCulture, out cs);

        return new CollectionExchangeBeatmap
        {
            Md5 = md5,
            Title = title,
            Artist = string.Empty,
            Version = difficulty,
            Creator = creator,
            Cs = cs,
            BeatmapsetId = 0
        };
    }
}
