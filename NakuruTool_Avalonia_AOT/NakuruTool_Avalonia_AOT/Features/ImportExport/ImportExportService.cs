using Avalonia.Threading;
using NakuruTool_Avalonia_AOT.Features.ImportExport.Models;
using NakuruTool_Avalonia_AOT.Features.OsuDatabase;
using NakuruTool_Avalonia_AOT.Features.Settings;
using R3;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using ZLinq;

namespace NakuruTool_Avalonia_AOT.Features.ImportExport;

public record ImportExportProgress(string Message, int ProgressValue);

public interface IImportExportService : IDisposable
{
    Observable<ImportExportProgress> ProgressObservable { get; }
    List<ImportFileItem> GetImportFiles();
    Task<int> ExportAsync(IReadOnlyList<string> collectionNames);
    Task<bool> ImportAsync(IReadOnlyList<string> filePaths);
}

/// <summary>
/// コレクションのエクスポート / インポートを行うサービス
/// </summary>
public class ImportExportService : IImportExportService
{
    private readonly IDatabaseService _databaseService;
    private readonly ISettingsService _settingsService;

    private readonly Subject<ImportExportProgress> _progress = new();
    public Observable<ImportExportProgress> ProgressObservable => _progress;

    private static readonly string ExportsFolder =
        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "exports");

    private static readonly string ImportsFolder =
        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "imports");

    public ImportExportService(IDatabaseService databaseService, ISettingsService settingsService)
    {
        _databaseService = databaseService;
        _settingsService = settingsService;
    }

    /// <summary>
    /// 指定したコレクション名のコレクションを JSON ファイルとして exports/ フォルダに書き出す。
    /// 戻り値: 成功したファイル数
    /// </summary>
    public async Task<int> ExportAsync(IReadOnlyList<string> collectionNames)
    {
        EnsureDirectory(ExportsFolder);

        var collections = _databaseService.OsuCollections;
        var total = collectionNames.Count;
        var succeeded = 0;

        for (var i = 0; i < total; i++)
        {
            var name = collectionNames[i];
            var progressPct = (int)((double)i / total * 90);
            NotifyProgress($"エクスポート中: {name} ({i + 1}/{total})", progressPct);

            try
            {
                var collection = collections.AsValueEnumerable()
                    .FirstOrDefault(c => c.Name == name);

                if (collection is null)
                    continue;

                var data = BuildExchangeData(collection);
                var json = JsonSerializer.Serialize(data, ImportExportJsonContext.Default.CollectionExchangeData);

                var fileName = SanitizeFileName(name) + ".json";
                var filePath = Path.Combine(ExportsFolder, fileName);
                await File.WriteAllTextAsync(filePath, json);
                succeeded++;
            }
            catch (Exception ex)
            {
                NotifyProgress($"エラー ({name}): {ex.Message}", progressPct);
            }
        }

        NotifyProgress($"エクスポート完了: {succeeded}/{total} 件", 100);
        return succeeded;
    }

    private CollectionExchangeData BuildExchangeData(OsuCollection collection)
    {
        var data = new CollectionExchangeData { Name = collection.Name };

        foreach (var md5 in collection.BeatmapMd5s)
        {
            if (_databaseService.TryGetBeatmapByMd5(md5, out var beatmap) && beatmap is not null)
            {
                data.Beatmaps.Add(CollectionExchangeBeatmap.FromBeatmap(beatmap));
            }
            else
            {
                // DB に存在しない場合は MD5 のみ保持
                data.Beatmaps.Add(new CollectionExchangeBeatmap { Md5 = md5 });
            }
        }

        return data;
    }

    // ─────────────────────────── インポート ───────────────────────────

    /// <summary>
    /// imports/ フォルダの JSON ファイル一覧を取得（パース済み）
    /// </summary>
    public List<ImportFileItem> GetImportFiles()
    {
        EnsureDirectory(ImportsFolder);

        var result = new List<ImportFileItem>();
        var files = Directory.GetFiles(ImportsFolder, "*.json");

        foreach (var filePath in files)
        {
            try
            {
                var json = File.ReadAllText(filePath);
                var data = JsonSerializer.Deserialize(json, ImportExportJsonContext.Default.CollectionExchangeData);
                if (data is null) continue;

                result.Add(new ImportFileItem
                {
                    FilePath = filePath,
                    DisplayName = Path.GetFileNameWithoutExtension(filePath),
                    CollectionName = data.Name,
                    BeatmapCount = data.Beatmaps.Count,
                    ParsedData = data
                });
            }
            catch
            {
                // パース失敗のファイルはスキップ
            }
        }

        return result;
    }

    /// <summary>
    /// 指定パスの JSON ファイルをインポートし、collection.db に反映する。
    /// 戻り値: すべて成功した場合 true
    /// </summary>
    public async Task<bool> ImportAsync(IReadOnlyList<string> filePaths)
    {
        var settings = _settingsService.SettingsData;
        if (string.IsNullOrWhiteSpace(settings.OsuFolderPath))
        {
            NotifyProgress("エラー: osu! フォルダが設定されていません", 0);
            return false;
        }

        var collectionDbPath = Path.Combine(settings.OsuFolderPath, "collection.db");
        if (!File.Exists(collectionDbPath))
        {
            NotifyProgress("エラー: collection.db が見つかりません", 0);
            return false;
        }

        var total = filePaths.Count;
        var allSuccess = true;

        for (var i = 0; i < total; i++)
        {
            var filePath = filePaths[i];
            var progressPct = (int)((double)i / total * 80);
            NotifyProgress($"インポート中: {Path.GetFileNameWithoutExtension(filePath)} ({i + 1}/{total})", progressPct);

            try
            {
                var json = await File.ReadAllTextAsync(filePath);
                var data = JsonSerializer.Deserialize(json, ImportExportJsonContext.Default.CollectionExchangeData);
                if (data is null)
                {
                    allSuccess = false;
                    continue;
                }

                // MD5照合でコレクション内容を構築
                var md5s = ResolveMd5s(data);

                var newCollection = new OsuCollection
                {
                    Name = data.Name,
                    BeatmapMd5s = md5s
                };

                // 同名コレクションはサイレント上書き
                _databaseService.OsuCollections.RemoveAll(c => c.Name == data.Name);
                _databaseService.OsuCollections.Add(newCollection);
            }
            catch (Exception ex)
            {
                NotifyProgress($"エラー ({Path.GetFileName(filePath)}): {ex.Message}", progressPct);
                allSuccess = false;
            }
        }

        NotifyProgress("collection.db を書き込み中...", 85);

        try
        {
            await CollectionDbWriter.WriteAsync(_databaseService.OsuCollections, collectionDbPath);
        }
        catch (Exception ex)
        {
            NotifyProgress($"書き込みエラー: {ex.Message}", 85);
            return false;
        }

        return allSuccess;
    }

    private string[] ResolveMd5s(CollectionExchangeData data)
    {
        var md5s = new List<string>(data.Beatmaps.Count);

        foreach (var bm in data.Beatmaps)
        {
            if (!string.IsNullOrEmpty(bm.Md5) && _databaseService.TryGetBeatmapByMd5(bm.Md5, out _))
            {
                md5s.Add(bm.Md5);
            }
        }

        return md5s.ToArray();
    }

    // ─────────────────────────── ユーティリティ ───────────────────────────

    private void NotifyProgress(string message, int value)
    {
        var data = new ImportExportProgress(message, Math.Max(0, Math.Min(100, value)));
        Dispatcher.UIThread.Post(() => _progress.OnNext(data));
    }

    private static void EnsureDirectory(string path)
    {
        if (!Directory.Exists(path))
            Directory.CreateDirectory(path);
    }

    /// <summary>ファイル名として使用できない文字を _ に置換</summary>
    private static string SanitizeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var chars = name.ToCharArray();
        for (var i = 0; i < chars.Length; i++)
        {
            if (Array.IndexOf(invalid, chars[i]) >= 0)
                chars[i] = '_';
        }
        return new string(chars);
    }

    public void Dispose()
    {
        _progress.Dispose();
    }
}
