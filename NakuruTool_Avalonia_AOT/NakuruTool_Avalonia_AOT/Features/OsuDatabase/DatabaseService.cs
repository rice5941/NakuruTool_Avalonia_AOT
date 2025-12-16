using Avalonia.Threading;
using NakuruTool_Avalonia_AOT.Features.Settings;
using NakuruTool_Avalonia_AOT.Features.Translate;
using OsuParsers.Decoders;
using OsuParsers.Enums;
using OsuParsers.Enums.Database;
using R3;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ZLinq;

namespace NakuruTool_Avalonia_AOT.Features.OsuDatabase
{

    public interface IDatabaseService : IDisposable
    {
        Observable<DatabaseLoadProgress> CollectionDbProgress { get; }
        Observable<DatabaseLoadProgress> OsuDbProgress { get; }
        Observable<DatabaseLoadProgress> ScoresDbProgress { get; }
        IReadOnlyList<OsuCollection> OsuCollections { get; }
        IReadOnlyDictionary<string, Beatmap> Beatmaps { get; }
        Task LoadDatabasesAsync();
    }

    /// <summary>
    /// データベース読み込み進捗情報
    /// </summary>
    public record DatabaseLoadProgress(string Message, int Progress);

    public class DatabaseService: IDatabaseService
    {
        private readonly ISettingsService _settingsService;
        
        private List<OsuCollection> _osuCollections = new();
        private Dictionary<string, Beatmap> _beatmaps = new();
        private readonly object _lockObject = new object();

        // R3 Subjectによる進捗通知
        private readonly Subject<DatabaseLoadProgress> _collectionDbProgress = new();
        private readonly Subject<DatabaseLoadProgress> _osuDbProgress = new();
        private readonly Subject<DatabaseLoadProgress> _scoresDbProgress = new();

        public Observable<DatabaseLoadProgress> CollectionDbProgress => _collectionDbProgress;
        public Observable<DatabaseLoadProgress> OsuDbProgress => _osuDbProgress;
        public Observable<DatabaseLoadProgress> ScoresDbProgress => _scoresDbProgress;

        public IReadOnlyList<OsuCollection> OsuCollections => _osuCollections.AsReadOnly();

        public IReadOnlyDictionary<string, Beatmap> Beatmaps => _beatmaps;

        public DatabaseService(ISettingsService settingsService)
        {
            _settingsService = settingsService;
        }

        /// <summary>
        /// collection.db / osu!.db / scores.dbを並列で読み込む
        /// </summary>
        public async Task LoadDatabasesAsync()
        {
            var settings = _settingsService.SettingsData;
            var osuFolderPath = settings.OsuFolderPath;
            
            if (string.IsNullOrWhiteSpace(osuFolderPath) || Directory.Exists(osuFolderPath) == false)
            {
                var message = string.Format(LanguageService.Instance.GetString("Loading.FolderNotFound"), osuFolderPath);
                throw new DirectoryNotFoundException(message);
            }

            var collectionDbPath = Path.Combine(osuFolderPath, "collection.db");
            var osuDbPath = Path.Combine(osuFolderPath, "osu!.db");
            var scoresDbPath = Path.Combine(osuFolderPath, "scores.db");

            // collection.dbのバックアップを作成（起動時のみ）
            await CreateBackupAsync(collectionDbPath);

            // 3つのDBファイルを並列で読み込み開始
            var collectionTask = Task.Run(() => ReadCollectionDbAsync(collectionDbPath));
            var osuDbTask = Task.Run(() => ReadOsuDbAsync(osuDbPath));
            var scoresTask = Task.Run(() => ReadScoresDbFileAsync(scoresDbPath));

            // すべての読み込みを待機
            await Task.WhenAll(collectionTask, osuDbTask, scoresTask);

            // 結果を取得
            _osuCollections = await collectionTask;
            _beatmaps = await osuDbTask;
            var scoresDb = await scoresTask;

            // データベース読み込み完了後、スコアデータを適用
            if (scoresDb != null && _beatmaps != null && _beatmaps.Count > 0)
            {
                // UIスレッドで進捗表示を更新するためにawaitを使用
                await Task.Run(() => ApplyScoresToBeatmaps(scoresDb));
            }
            else if (scoresDb == null)
            {
                // scores.dbが無い場合は、その旨を通知
                OnScoresDbProgressChanged("スコアデータベースが見つかりませんでした（スキップ）", 100);
            }
        }

        /// <summary>
        /// collection.dbのバックアップを作成
        /// </summary>
        /// <param name="collectionDbPath">collection.dbのパス</param>
        /// <returns>非同期タスク</returns>
        private async Task CreateBackupAsync(string collectionDbPath)
        {
            if (File.Exists(collectionDbPath) == false)
            {
                // ファイルが存在しない場合はバックアップ不要
                return;
            }

            await Task.Run(() =>
            {
                try
                {
                    // バックアップフォルダの作成
                    var backupFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "backups");
                    if (Directory.Exists(backupFolder) == false)
                    {
                        Directory.CreateDirectory(backupFolder);
                    }

                    // タイムスタンプ付きのバックアップファイル名を生成
                    var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                    var backupPath = Path.Combine(backupFolder, $"collection_startup_{timestamp}.db");

                    // バックアップファイルをコピー
                    File.Copy(collectionDbPath, backupPath, true);

                    Console.WriteLine($"起動時バックアップを作成しました: {backupPath}");
                }
                catch (Exception ex)
                {
                    // バックアップに失敗してもDB読み込みは継続
                    Console.WriteLine($"バックアップ作成エラー: {ex.Message}");
                }
            });
        }

        /// <summary>
        /// collection.dbファイルを読み込む
        /// </summary>
        /// <param name="filePath">collection.dbのファイルパス</param>
        /// <returns>osu!コレクションのリスト</returns>
        private async Task<List<OsuCollection>> ReadCollectionDbAsync(string filePath)
        {
            return await Task.Run(() =>
            {
                if (File.Exists(filePath) == false)
                {
                    OnCollectionDbProgressChanged(LanguageService.Instance.GetString("Loading.CollectionNotFound"), 0);
                    return new List<OsuCollection>();
                }

                OnCollectionDbProgressChanged(LanguageService.Instance.GetString("Loading.CollectionLoading"), 0);
                var collections = new List<OsuCollection>();

                using (var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read))
                using (var reader = new BinaryReader(fileStream, Encoding.UTF8))
                {
                    // バージョン情報を読み込み（4バイト）
                    var version = reader.ReadInt32();
                    OnCollectionDbProgressChanged(string.Format(LanguageService.Instance.GetString("Loading.CollectionVersion"), version), 10);

                    // コレクション数を読み込み（4バイト）
                    var collectionCount = reader.ReadInt32();
                    OnCollectionDbProgressChanged(string.Format(LanguageService.Instance.GetString("Loading.CollectionCount"), collectionCount), 20);

                    // 各コレクションを読み込み
                    for (int i = 0; i < collectionCount; i++)
                    {
                        var collection = new OsuCollection();

                        // コレクション名を読み込み
                        collection.Name = ReadOsuString(reader);

                        // ビートマップ数を読み込み
                        var beatmapCount = reader.ReadInt32();

                        // MD5ハッシュを配列として読み込み
                        var md5Array = new string[beatmapCount];
                        int validCount = 0;
                        
                        for (int j = 0; j < beatmapCount; j++)
                        {
                            var md5Hash = ReadOsuString(reader);
                            if (string.IsNullOrEmpty(md5Hash) == false)
                            {
                                md5Array[validCount++] = md5Hash;
                            }
                        }

                        // 有効な要素のみを含む配列にリサイズ
                        if (validCount < beatmapCount)
                        {
                            Array.Resize(ref md5Array, validCount);
                        }

                        collection.BeatmapMd5s = md5Array;
                        collections.Add(collection);
                        
                        // 進捗を更新
                        var progress = 20 + (int)((double)i / collectionCount * 30);
                        var message = string.Format(LanguageService.Instance.GetString("Loading.CollectionItemCompleted"), collection.Name, validCount);
                        OnCollectionDbProgressChanged(message, progress);
                    }
                }

                OnCollectionDbProgressChanged(LanguageService.Instance.GetString("Loading.CollectionCompleted"), 100);
                return collections;
            });
        }

        /// <summary>
        /// osu!.dbファイルを読み込む（OsuParsersを使用）
        /// </summary>
        /// <param name="filePath">osu!.dbのファイルパス</param>
        /// <returns>Beatmap情報の辞書</returns>
        private async Task<Dictionary<string, Beatmap>> ReadOsuDbAsync(string filePath)
        {
            return await Task.Run(() =>
            {
                if (File.Exists(filePath) == false)
                {
                    OnOsuDbProgressChanged(LanguageService.Instance.GetString("Loading.OsuNotFound"), 0);
                    return new Dictionary<string, Beatmap>();
                }

                OnOsuDbProgressChanged(LanguageService.Instance.GetString("Loading.OsuLoading"), 0);

                try
                {
                    // OsuParsersを使用してosu!.dbを読み込み
                    var osuDatabase = DatabaseDecoder.DecodeOsu(filePath);
                    
                    OnOsuDbProgressChanged(string.Format(LanguageService.Instance.GetString("Loading.OsuVersion"), osuDatabase.OsuVersion), 10);
                    OnOsuDbProgressChanged(string.Format(LanguageService.Instance.GetString("Loading.BeatmapCount"), osuDatabase.Beatmaps.Count), 15);

                    var totalBeatmaps = osuDatabase.Beatmaps.Count;
                    var processedCount = 0;

                    // ZLinqを使用して並列処理でBeatmap情報を変換・フィルタリング
                    // AsValueEnumerableで値型列挙子を使用し、アロケーションを削減
                    var beatmapDict = osuDatabase.Beatmaps.AsValueEnumerable()
                        .Where(dbBeatmap => 
                        {
                            // 進捗を更新（100件ごと）
                            var current = System.Threading.Interlocked.Increment(ref processedCount);
                            if (current % 1000 == 0 || current == totalBeatmaps)
                            {
                                var progress = 15 + (int)((double)current / totalBeatmaps * 85);
                                var processingMessage = string.Format(LanguageService.Instance.GetString("Loading.BeatmapProcessing"), current, totalBeatmaps);
                                OnOsuDbProgressChanged(processingMessage, progress);
                            }
                            return string.IsNullOrEmpty(dbBeatmap.MD5Hash) == false;
                        })
                        .Select(dbBeatmap => (dbBeatmap.MD5Hash, Beatmap: ConvertToBeatmap(dbBeatmap)))
                        .Where(x => x.Beatmap.KeyCount != 0)
                        .DistinctBy(x => x.MD5Hash) // 重複するMD5ハッシュを除去（最初のエントリを保持）
                        .ToDictionary(x => x.MD5Hash, x => x.Beatmap);

                    OnOsuDbProgressChanged(LanguageService.Instance.GetString("Loading.OsuCompleted"), 100);
                    return beatmapDict;
                }
                catch (Exception ex)
                {
                    var errorMessage = string.Format(LanguageService.Instance.GetString("Loading.OsuError"), ex.Message);
                    OnOsuDbProgressChanged(errorMessage, 0);
                    throw;
                }
            });
        }

        /// <summary>
        /// osu!形式の文字列を読み込む
        /// </summary>
        /// <param name="reader">バイナリリーダー</param>
        /// <returns>読み込まれた文字列</returns>
        private string ReadOsuString(BinaryReader reader)
        {
            var prefix = reader.ReadByte();
            
            if (prefix == 0x00)
            {
                // 空文字列
                return string.Empty;
            }
            else if (prefix == 0x0b)
            {
                // 文字列あり
                var length = ReadULEB128(reader);
                var bytes = reader.ReadBytes((int)length);
                return Encoding.UTF8.GetString(bytes);
            }
            else
            {
                throw new InvalidDataException(string.Format(LanguageService.Instance.GetString("Loading.InvalidStringPrefix"), prefix));
            }
        }

        /// <summary>
        /// ULEB128形式の整数を読み込む
        /// </summary>
        /// <param name="reader">バイナリリーダー</param>
        /// <returns>読み込まれた整数値</returns>
        private uint ReadULEB128(BinaryReader reader)
        {
            uint result = 0;
            int shift = 0;

            while (true)
            {
                var b = reader.ReadByte();
                result |= (uint)(b & 0x7F) << shift;

                if ((b & 0x80) == 0)
                    break;

                shift += 7;
            }

            return result;
        }

        /// <summary>
        /// scores.dbファイルを読み込んでScoresDatabaseオブジェクトを返す（ファイルI/Oのみ）
        /// </summary>
        /// <param name="filePath">scores.dbファイルのパス</param>
        /// <returns>ScoresDatabaseオブジェクト</returns>
        private async Task<OsuParsers.Database.ScoresDatabase?> ReadScoresDbFileAsync(string filePath)
        {
            return await Task.Run(() =>
            {
                try
                {
                    if (File.Exists(filePath) == false)
                    {
                        Console.WriteLine("scores.dbファイルが見つかりません: " + filePath);
                        return null;
                    }

                    // scores.dbの読み込み開始を通知
                    OnScoresDbProgressChanged(LanguageService.Instance.GetString("Loading.ScoresLoading"), 0);

                    // OsuParsersを使用してscores.dbを読み込み
                    var scoresDb = DatabaseDecoder.DecodeScores(filePath);
                    
                    if (scoresDb == null || scoresDb.Scores == null)
                    {
                        Console.WriteLine("scores.dbの読み込みに失敗しました");
                        return null;
                    }

                    // 読み込み完了を通知
                    OnScoresDbProgressChanged(string.Format(LanguageService.Instance.GetString("Loading.ScoresLoaded"), scoresDb.Scores.Count), 50);

                    return scoresDb;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"scores.db読み込みエラー: {ex.Message}");
                    return null;
                }
            });
        }

        /// <summary>
        /// 読み込んだスコアデータをBeatmapに適用
        /// </summary>
        /// <param name="scoresDb">スコアデータベース</param>
        private void ApplyScoresToBeatmaps(OsuParsers.Database.ScoresDatabase scoresDb)
        {
            if (scoresDb == null || scoresDb.Scores == null || _beatmaps == null)
                return;

            var totalScores = scoresDb.Scores.Count;
            var processedScores = 0;

            // スコアデータの適用開始を通知
            OnScoresDbProgressChanged(string.Format(LanguageService.Instance.GetString("Loading.ScoresApplying"), 0, totalScores), 50);

            // 各beatmapのスコア情報を処理
            foreach (var scoreEntry in scoresDb.Scores)
            {
                var md5Hash = scoreEntry.Item1; // beatmapのMD5ハッシュ
                var scoreList = scoreEntry.Item2; // そのbeatmapのスコアリスト

                if (scoreList != null && scoreList.Count > 0)
                {
                    // 対応するBeatmapを検索（Dictionary検索で高速化）
                    if (_beatmaps.TryGetValue(md5Hash, out var beatmap))
                    {
                        // ZLinqを使用して最高スコアと最高精度を計算（アロケーション削減）
                        int bestScore = scoreList.AsValueEnumerable().Max(s => s.ReplayScore);
                        double bestAccuracy = scoreList.AsValueEnumerable().Max(s => CalculateAccuracy(s));
                        int playCount = scoreList.Count;

                        // record型なので、with式で新しいインスタンスを作成してDictionaryを更新
                        _beatmaps[md5Hash] = beatmap with
                        {
                            BestScore = bestScore,
                            BestAccuracy = bestAccuracy,
                            PlayCount = playCount
                        };
                    }
                }

                // 進捗カウンター更新
                processedScores++;
                
                // より頻繁に進捗を更新（1000件ごと、または最後）
                if (processedScores % 1000 == 0 || processedScores == totalScores)
                {
                    int progress = 1000 + (int)((double)processedScores / totalScores * 1000);
                    OnScoresDbProgressChanged(string.Format(LanguageService.Instance.GetString("Loading.ScoresApplying"), processedScores, totalScores), progress);
                }
            }

            // 適用完了を通知
            OnScoresDbProgressChanged(string.Format(LanguageService.Instance.GetString("Loading.ScoresCompleted"), totalScores), 100);
        }

        /// <summary>
        /// スコアから精度を計算
        /// </summary>
        /// <param name="score">スコア情報</param>
        /// <returns>精度（0-100%）</returns>
        private double CalculateAccuracy(OsuParsers.Database.Objects.Score score)
        {
            // 精度の計算
            // maniaモードの場合の精度計算
            if (score.Ruleset == Ruleset.Mania)
            {
                var totalHits = score.Count300 + score.Count100 + score.Count50 + 
                               score.CountGeki + score.CountKatu + score.CountMiss;
                
                if (totalHits == 0) return 0;

                // maniaの精度計算式
                var weightedScore = (score.Count300 + score.CountGeki) * 300.0 +
                                   score.CountKatu * 200.0 +
                                   score.Count100 * 100.0 +
                                   score.Count50 * 50.0;
                
                var maxScore = totalHits * 300.0;
                
                return maxScore > 0 ? weightedScore / maxScore * 100.0 : 0;
            }
            
            // 他のモードの場合（Scoreクラスには直接Accuracyプロパティがないため、計算で求める）
            var totalHitsOther = score.Count300 + score.Count100 + score.Count50 + score.CountMiss;
            if (totalHitsOther == 0) return 0;
            
            var weightedScoreOther = score.Count300 * 300.0 + score.Count100 * 100.0 + score.Count50 * 50.0;
            var maxScoreOther = totalHitsOther * 300.0;
            
            return maxScoreOther > 0 ? weightedScoreOther / maxScoreOther * 100.0 : 0;
        }

        /// <summary>
        /// OsuParsersのDbBeatmapをBeatmapモデルに変換
        /// </summary>
        /// <param name="dbBeatmap">OsuParsersのDbBeatmap</param>
        /// <returns>変換されたBeatmapモデル</returns>
        private Beatmap ConvertToBeatmap(OsuParsers.Database.Objects.DbBeatmap dbBeatmap)
        {
            // maniaモードの場合、CircleSizeからキー数を取得
            int keyCount = dbBeatmap.Ruleset == Ruleset.Mania ? (int)dbBeatmap.CircleSize : 0;

            return new Beatmap
            {
                MD5Hash = dbBeatmap.MD5Hash ?? string.Empty,
                Title = dbBeatmap.Title ?? string.Empty,
                Artist = dbBeatmap.Artist ?? string.Empty,
                Version = dbBeatmap.Difficulty ?? string.Empty,
                Creator = dbBeatmap.Creator ?? string.Empty,
                BPM = CalculateBPM(dbBeatmap),
                Difficulty = GetManiaStarRating(dbBeatmap),
                CircleSize = dbBeatmap.CircleSize,
                BeatmapSetId = dbBeatmap.BeatmapSetId,
                BeatmapId = dbBeatmap.BeatmapId,
                GameMode = (int)dbBeatmap.Ruleset,
                Status = ConvertRankedStatus(dbBeatmap.RankedStatus),
                IsPlayed = dbBeatmap.IsUnplayed == false,
                LastPlayed = dbBeatmap.LastPlayed == DateTime.MinValue ? null : dbBeatmap.LastPlayed,
                LastModifiedTime = dbBeatmap.LastModifiedTime == DateTime.MinValue ? null : dbBeatmap.LastModifiedTime,
                Exists = dbBeatmap.FolderName != null,
                Grade = GetGradeString(dbBeatmap),
                KeyCount = keyCount,
                LongNoteRate = CalculateLongNoteRate(dbBeatmap),
                BestScore = 0,
                BestAccuracy = 0,
                PlayCount = 0
            };
        }

        /// <summary>
        /// BPMを計算する
        /// </summary>
        /// <param name="dbBeatmap">DbBeatmap</param>
        /// <returns>BPM値</returns>
        private double CalculateBPM(OsuParsers.Database.Objects.DbBeatmap dbBeatmap)
        {
            try
            {
                // TimingPointsが存在しない場合はデフォルト値を返す
                if (dbBeatmap.TimingPoints == null || dbBeatmap.TimingPoints.Count == 0)
                {
                    return 120.0;
                }

                // 最初の非継承タイミングポイントからBPMを取得（ZLinq使用）
                var firstTimingPoint = dbBeatmap.TimingPoints.AsValueEnumerable()
                    .Where(tp => tp.Inherited == false)
                    .FirstOrDefault();
                
                if (firstTimingPoint != null)
                {
                    // DbTimingPointのBPMプロパティは実際にはBeatLengthを表している
                    // BPM = 60000 / BeatLength で計算
                    var beatLength = firstTimingPoint.BPM;
                    if (beatLength > 0)
                    {
                        return 60000.0 / beatLength;
                    }
                    else if (beatLength < 0)
                    {
                        // 負の値の場合は継承タイミングポイントなので、BPMを探す必要がある
                        // 通常はInherited = trueになっているはずなので、ここには来ないはず
                        return 120.0;
                    }
                }

                // 継承されていないタイミングポイントが見つからない場合
                // 最初のタイミングポイントからBPMを取得（ZLinq使用）
                var firstPoint = dbBeatmap.TimingPoints.AsValueEnumerable().First();
                if (firstPoint.BPM > 0)
                {
                    return 60000.0 / firstPoint.BPM;
                }
            }
            catch (Exception ex)
            {
                // エラーログを出力（実際の実装ではロガーを使用）
                System.Diagnostics.Debug.WriteLine($"BPM計算エラー: {ex.Message}");
            }

            // 取得できない場合はデフォルト値を返す
            return 120.0;
        }

        /// <summary>
        /// ロングノート率を計算する
        /// </summary>
        /// <param name="dbBeatmap">DbBeatmap</param>
        /// <returns>ロングノート率（0.0～1.0）</returns>
        private double CalculateLongNoteRate(OsuParsers.Database.Objects.DbBeatmap dbBeatmap)
        {
            // maniaモードでない場合は0.0を返す
            if (dbBeatmap.Ruleset != Ruleset.Mania)
            {
                return 0.0;
            }

            try
            {
                // 総ノート数を計算
                var totalNotes = dbBeatmap.CirclesCount + dbBeatmap.SlidersCount + dbBeatmap.SpinnersCount;
                
                // 総ノート数が0の場合は0.0を返す
                if (totalNotes == 0)
                {
                    return 0.0;
                }

                // ロングノート率 = SlidersCount / 総ノート数
                return (double)dbBeatmap.SlidersCount / totalNotes;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ロングノート率計算エラー: {ex.Message}");
                return 0.0;
            }
        }

        /// <summary>
        /// ManiaモードのStarRatingを取得する
        /// </summary>
        /// <param name="dbBeatmap">DbBeatmap</param>
        /// <returns>ManiaのStarRating値</returns>
        private double GetManiaStarRating(OsuParsers.Database.Objects.DbBeatmap dbBeatmap)
        {
            try
            {
                // ManiaStarRatingからMods.Noneの値を取得
                if (dbBeatmap.ManiaStarRating != null && 
                    dbBeatmap.ManiaStarRating.ContainsKey(Mods.None))
                {
                    return dbBeatmap.ManiaStarRating[Mods.None];
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ManiaStarRating取得エラー: {ex.Message}");
            }

            // 取得できない場合は0.0を返す
            return 0.0;
        }

        /// <summary>
        /// RankedStatusをBeatmapStatusに変換
        /// </summary>
        /// <param name="rankedStatus">OsuParsersのRankedStatus</param>
        /// <returns>BeatmapStatus</returns>
        private BeatmapStatus ConvertRankedStatus(object rankedStatus)
        {
            if (rankedStatus == null)
                return BeatmapStatus.None;

            var statusName = rankedStatus.ToString();
            
            return statusName switch
            {
                "Ranked" => BeatmapStatus.Ranked,
                "Loved" => BeatmapStatus.Loved,
                "Approved" => BeatmapStatus.Approved,
                "Qualified" => BeatmapStatus.Qualified,
                "Pending" => BeatmapStatus.Pending,
                "WIP" => BeatmapStatus.Pending,
                "Graveyard" => BeatmapStatus.Pending,
                _ => BeatmapStatus.None
            };
        }

        /// <summary>
        /// collection.db読み込み進捗を通知
        /// </summary>
        private void OnCollectionDbProgressChanged(string message, int progress)
            => PublishOnUi(_collectionDbProgress, message, progress);

        /// <summary>
        /// osu!.db読み込み進捗を通知
        /// </summary>
        private void OnOsuDbProgressChanged(string message, int progress)
            => PublishOnUi(_osuDbProgress, message, progress);

        /// <summary>
        /// scores.db読み込み進捗を通知
        /// </summary>
        private void OnScoresDbProgressChanged(string message, int progress) 
            => PublishOnUi(_scoresDbProgress, message, progress);

        private void PublishOnUi(Subject<DatabaseLoadProgress> subject, string message, int progress)
        {
            var data = new DatabaseLoadProgress(message, Math.Max(0, Math.Min(100, progress)));
            Dispatcher.UIThread.Post(() => subject.OnNext(data));
        }

        /// <summary>
        /// DbBeatmapからgradeを取得してstring形式で返す
        /// </summary>
        /// <param name="dbBeatmap">DbBeatmap</param>
        /// <returns>Grade文字列（maniaモード固定）</returns>
        private string GetGradeString(OsuParsers.Database.Objects.DbBeatmap dbBeatmap)
        {
            try
            {
                // maniaモードのgradeを取得
                if (dbBeatmap.Ruleset == Ruleset.Mania)
                {
                    var grade = dbBeatmap.ManiaGrade;
                    
                    // Gradeが未プレイ（N=None等）の場合は空文字列を返す
                    if (grade == Grade.N || grade == Grade.F)
                    {
                        return string.Empty;
                    }
                    
                    // Gradeを文字列に変換
                    switch (grade)
                    {
                        case Grade.XH:
                            return "SS+";
                        case Grade.X:
                            return "SS";
                        case Grade.SH:
                            return "S+";
                        case Grade.S:
                            return "S";
                        case Grade.A:
                            return "A";
                        case Grade.B:
                            return "B";
                        case Grade.C:
                            return "C";
                        case Grade.D:
                            return "D";
                        default:
                            return string.Empty;
                    }
                }
                
                return string.Empty;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Grade取得エラー: {ex.Message}");
                return string.Empty;
            }
        }

        /// <summary>
        /// データベースをリロード
        /// </summary>
        /// <returns>リロードタスク</returns>
        public async Task ReloadDatabasesAsync()
        {
            // 既存のデータをクリア
            lock (_lockObject)
            {
                _osuCollections?.Clear();
                _beatmaps?.Clear();
            }

            // 再読み込み
            await LoadDatabasesAsync();
        }

        public void Dispose()
        {
            _collectionDbProgress.Dispose();
            _osuDbProgress.Dispose();
            _scoresDbProgress.Dispose();
        }
    }
}