using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace NakuruTool_Avalonia_AOT.Translate;

/// <summary>
/// NativeAOT対応のためのJSON Source Generatorコンテキスト
/// </summary>
[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(Dictionary<string, JsonElement>))]
public partial class LanguageJsonContext : JsonSerializerContext
{
}
