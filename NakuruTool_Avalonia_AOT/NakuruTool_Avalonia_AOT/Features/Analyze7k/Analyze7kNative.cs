using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;

namespace NakuruTool_Avalonia_AOT.Features.Analyze7k;

/// <summary>
/// analyze_7k.dll（Rust / Mania Tracker）の呼び出し。複数スレッドから同時に呼んでよい。
/// 段位エンジンは再帰の深い処理を含むため、呼び出し側は大きめのスタックのスレッドで使うこと。
/// </summary>
public static unsafe class Analyze7kNative
{
    private const int StatusOk = 0;

    /// <summary>Prism の値名と <see cref="PrismGenre"/> の対応（表示順）</summary>
    private static readonly string[] PrismGenreKeys = ["Jumpstream", "Handstream", "Speed", "Technical", "JackSpeed", "ChordjackOnly"];

    private static readonly Lazy<string?> s_engineVersion = new(LoadEngineVersion);

    /// <summary>エンジンの版（DLL が読み込めなければ null）</summary>
    public static string? EngineVersion => s_engineVersion.Value;

    public static bool IsAvailable => EngineVersion is not null;

    private static string? LoadEngineVersion()
    {
        try
        {
            A7kBuffer buffer;
            if (NativeMethods.a7k_engine_version(&buffer) != StatusOk)
                return null;
            return TakeString(buffer);
        }
        catch (DllNotFoundException)
        {
            return null;
        }
        catch (EntryPointNotFoundException)
        {
            return null;
        }
        catch (BadImageFormatException)
        {
            return null;
        }
    }

    /// <summary>1譜面を解析する。.osu はファイルの内容そのまま（BOM 付きでもよい）を渡す。</summary>
    /// <exception cref="Analyze7kEngineException">エンジンが失敗した場合</exception>
    public static Analyze7kResponse Analyze(ReadOnlySpan<byte> osu, double od)
    {
        var requestJson = JsonSerializer.SerializeToUtf8Bytes(new Analyze7kRequest { Od = od }, Analyze7kJsonContext.Default.Analyze7kRequest);
        A7kBuffer buffer;
        int status;
        fixed (byte* osuPtr = osu)
        fixed (byte* requestPtr = requestJson)
        {
            status = NativeMethods.a7k_analyze(osuPtr, (nuint)osu.Length, requestPtr, (nuint)requestJson.Length, &buffer);
        }

        var json = TakeBytes(buffer);
        var response = JsonSerializer.Deserialize(json, Analyze7kJsonContext.Default.Analyze7kResponse)
                       ?? throw new Analyze7kEngineException("empty response");
        if (status != StatusOk)
            throw new Analyze7kEngineException(response.Error ?? $"status {status}");
        return response;
    }

    /// <summary>エンジンの出力を解析結果に変換する（段位ジャンルはここで判定する）</summary>
    public static ManiaChartAnalysis ToAnalysis(Analyze7kResponse response)
    {
        var prism = response.Prism ?? throw new Analyze7kEngineException("Prism result missing");
        var dan = response.Dan ?? throw new Analyze7kEngineException("dan result missing");
        return new ManiaChartAnalysis
        {
            PrismOverall = prism.Overall,
            PrismLnOverall = prism.Ln > 0 ? prism.Ln : null,
            GenreSkills = Genres(prism.Genres),
            GenreTendencies = Genres(prism.Tendency),
            RegularDan = dan.Regular?.RawDan,
            LnDan = dan.Ln?.RawDan,
            PrimaryFamily = dan.PrimaryFamily switch
            {
                "regular" => DanSide.Regular,
                "ln" => DanSide.Ln,
                _ => null,
            },
            RegularGenres = DanBuckets.RegularGenres(dan),
            LnGenres = DanBuckets.LnGenres(dan),
            Eligible = dan.Eligible,
            Vibro = dan.Vibro,
        };
    }

    private static GenreValues Genres(Dictionary<string, double>? values)
    {
        if (values is null)
            return default;
        Span<float> buffer = stackalloc float[PrismGenreKeys.Length];
        for (int i = 0; i < PrismGenreKeys.Length; i++)
            buffer[i] = values.TryGetValue(PrismGenreKeys[i], out var v) ? (float)v : 0;
        return GenreValues.FromArray(buffer);
    }

    private static string TakeString(A7kBuffer buffer) => Encoding.UTF8.GetString(TakeBytes(buffer));

    private static byte[] TakeBytes(A7kBuffer buffer)
    {
        try
        {
            return buffer.ptr == null
                ? []
                : new ReadOnlySpan<byte>(buffer.ptr, checked((int)buffer.len)).ToArray();
        }
        finally
        {
            NativeMethods.a7k_free(buffer);
        }
    }
}

public sealed class Analyze7kEngineException(string message) : Exception(message);
