using NAudio.Wave;

namespace NakuruTool_Avalonia_AOT.Features.BeatmapGenerator;

/// <summary>
/// SampleRate を偽装してリサンプラーにレート変更させるための ISampleProvider ラッパー。
/// </summary>
internal sealed class SampleRateOverrideSampleProvider : ISampleProvider
{
    private readonly ISampleProvider _source;

    public WaveFormat WaveFormat { get; }

    public SampleRateOverrideSampleProvider(ISampleProvider source, int overriddenSampleRate)
    {
        _source = source;
        WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(overriddenSampleRate, source.WaveFormat.Channels);
    }

    public int Read(float[] buffer, int offset, int count) => _source.Read(buffer, offset, count);
}
