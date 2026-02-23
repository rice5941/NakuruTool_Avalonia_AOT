using Avalonia.Threading;
using NakuruTool_Avalonia_AOT.Features.Settings;
using NakuruTool_Avalonia_AOT.Features.Translate;
using R3;
using System;
using System.IO;
using System.Threading.Tasks;
using ZLinq;

namespace NakuruTool_Avalonia_AOT.Features.OsuDatabase
{
    public interface IGenerateCollectionService : IDisposable
    {
        Observable<GenerationProgress> GenerationProgressObservable { get; }
        Task<bool> GenerateCollection(string collectionName, Beatmap[] beatmaps);
    }

    /// <summary>
    /// コレクション生成進捗情報
    /// </summary>
    public record GenerationProgress(string Message, int ProgressValue);

    public class GenerateCollectionService: IGenerateCollectionService
    {
        private readonly IDatabaseService _databaseService;
        private readonly ISettingsService _settingsService;

        private readonly Subject<GenerationProgress> _generationProgress = new();
        public Observable<GenerationProgress> GenerationProgressObservable => _generationProgress;

        public GenerateCollectionService(IDatabaseService databaseService, ISettingsService settingsService)
        {
            _databaseService = databaseService;
            _settingsService = settingsService;
        }

        /// <summary>
        /// 新しいコレクションを生成してcollection.dbに保存
        /// </summary>
        /// <param name="collectionName">コレクション名</param>
        /// <param name="beatmaps">コレクションに含めるBeatmap</param>
        /// <returns>処理成功時はtrue</returns>
        public async Task<bool> GenerateCollection(string collectionName, Beatmap[] beatmaps)
        {
            try
            {
                var settings = _settingsService.SettingsData;
                if (string.IsNullOrWhiteSpace(settings?.OsuFolderPath))
                {
                    OnGenerationProgressChanged(LanguageService.Instance.GetString("Generation.Error.OsuFolderNotSet"), 0);
                    return false;
                }

                var collectionDbPath = Path.Combine(settings.OsuFolderPath, "collection.db");
                if (!File.Exists(collectionDbPath))
                {
                    OnGenerationProgressChanged(LanguageService.Instance.GetString("Generation.Error.CollectionDbNotFound"), 0);
                    return false;
                }

                OnGenerationProgressChanged(LanguageService.Instance.GetString("Generation.ReadingExistingCollections"), 10);

                // 重複するコレクションを削除（上書きのため）
                _databaseService.OsuCollections.RemoveAll(c => c.Name == collectionName);

                OnGenerationProgressChanged(LanguageService.Instance.GetString("Generation.CreatingCollection"), 30);

                // 新しいコレクションを作成
                var newCollection = new OsuCollection
                {
                    Name = collectionName,
                    BeatmapMd5s = beatmaps.AsValueEnumerable().Select(b => b.MD5Hash).ToArray()
                };

                // 既存のコレクションリストに追加
                _databaseService.OsuCollections.Add(newCollection);

                OnGenerationProgressChanged(LanguageService.Instance.GetString("Generation.WritingToDb"), 50);

                // collection.dbに書き込み
                await CollectionDbWriter.WriteAsync(_databaseService.OsuCollections, collectionDbPath);

                // 進捗を100%にするが、メッセージは表示しない（ViewModelで表示するため）
                OnGenerationProgressChanged(string.Empty, 100);

                return true;
            }
            catch (Exception ex)
            {
                OnGenerationProgressChanged($"{LanguageService.Instance.GetString("Generation.Error")}: {ex.Message}", 0);
                return false;
            }
        }

        /// <summary>
        /// 進捗通知
        /// </summary>
        private void OnGenerationProgressChanged(string message, int progressValue)
        {
            var data = new GenerationProgress(message, Math.Max(0, Math.Min(100, progressValue)));
            Dispatcher.UIThread.Post(() => _generationProgress.OnNext(data));
        }

        public void Dispose()
        {
            _generationProgress.Dispose();
        }
    }
}