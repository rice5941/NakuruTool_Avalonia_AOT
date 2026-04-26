using System.Collections.Generic;

namespace NakuruTool_Avalonia_AOT.Features.BeatmapGenerator;

public sealed record OsbFileConvertOptions
{
    /// <summary>レート倍率</summary>
    public required decimal Rate { get; init; }

    /// <summary>sample filename リネームマップ。null / 空でも可。</summary>
    public IReadOnlyDictionary<string, string>? SampleFilenameMap { get; init; }
}
