using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
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
        Beatmap[] Beatmaps { get; }
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
        private Beatmap[] _beatmaps = Array.Empty<Beatmap>();
        private readonly object _lockObject = new object();

        // R3 Subjectによる進捗通知
        private readonly Subject<DatabaseLoadProgress> _collectionDbProgress = new();
        private readonly Subject<DatabaseLoadProgress> _osuDbProgress = new();
        private readonly Subject<DatabaseLoadProgress> _scoresDbProgress = new();

        public Observable<DatabaseLoadProgress> CollectionDbProgress => _collectionDbProgress;
        public Observable<DatabaseLoadProgress> OsuDbProgress => _osuDbProgress;
        public Observable<DatabaseLoadProgress> ScoresDbProgress => _scoresDbProgress;

        public IReadOnlyList<OsuCollection> OsuCollections => _osuCollections.AsReadOnly();
        public Beatmap[] Beatmaps => _beatmaps;

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
            if (scoresDb != null && _beatmaps != null && _beatmaps.Length > 0)
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
        private async Task CreateBackupAsync(string collectionDbPath)
        {
            if (File.Exists(collectionDbPath) == false)
            {
                return;
            }

            await Task.Run(() =>
            {
                try
                {
                    var backupFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "backups");
                    if (Directory.Exists(backupFolder) == false)
                    {
                        Directory.CreateDirectory(backupFolder);
                    }

                    var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                    var backupPath = Path.Combine(backupFolder, $"collection_startup_{timestamp}.db");

                    File.Copy(collectionDbPath, backupPath, true);

                    Console.WriteLine($"起動時バックアップを作成しました: {backupPath}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"バックアップ作成エラー: {ex.Message}");
                }
            });
        }

        /// <summary>
        /// collection.dbファイルを読み込む
        /// </summary>
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
                    var version = reader.ReadInt32();
                    OnCollectionDbProgressChanged(string.Format(LanguageService.Instance.GetString("Loading.CollectionVersion"), version), 10);

                    var collectionCount = reader.ReadInt32();
                    OnCollectionDbProgressChanged(string.Format(LanguageService.Instance.GetString("Loading.CollectionCount"), collectionCount), 20);

                    for (int i = 0; i < collectionCount; i++)
                    {
                        var collection = new OsuCollection();
                        collection.Name = ReadOsuString(reader);

                        var beatmapCount = reader.ReadInt32();
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

                        if (validCount < beatmapCount)
                        {
                            Array.Resize(ref md5Array, validCount);
                        }

                        collection.BeatmapMd5s = md5Array;
                        collections.Add(collection);
                        
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
        private async Task<Beatmap[]> ReadOsuDbAsync(string filePath)
        {
            return await Task.Run(() =>
            {
                if (File.Exists(filePath) == false)
                {
                    OnOsuDbProgressChanged(LanguageService.Instance.GetString("Loading.OsuNotFound"), 0);
                    return Array.Empty<Beatmap>();
                }

                OnOsuDbProgressChanged(LanguageService.Instance.GetString("Loading.OsuLoading"), 0);

                try
                {
                    var osuDatabase = DatabaseDecoder.DecodeOsu(filePath);
                    
                    OnOsuDbProgressChanged(string.Format(LanguageService.Instance.GetString("Loading.OsuVersion"), osuDatabase.OsuVersion), 10);
                    OnOsuDbProgressChanged(string.Format(LanguageService.Instance.GetString("Loading.BeatmapCount"), osuDatabase.Beatmaps.Count), 15);

                    var totalBeatmaps = osuDatabase.Beatmaps.Count;
                    var processedCount = 0;

                    var beatmapArray = osuDatabase.Beatmaps.AsValueEnumerable()
                        .Where(dbBeatmap => 
                        {
                            var current = System.Threading.Interlocked.Increment(ref processedCount);
                            if (current % 1000 == 0 || current == totalBeatmaps)
                            {
                                var progress = 15 + (int)((double)current / totalBeatmaps * 80);
                                var processingMessage = string.Format(LanguageService.Instance.GetString("Loading.BeatmapProcessing"), current, totalBeatmaps);
                                OnOsuDbProgressChanged(processingMessage, progress);
                            }
                            return string.IsNullOrEmpty(dbBeatmap.MD5Hash) == false;
                        })
                        .Select(dbBeatmap => ConvertToBeatmap(dbBeatmap))
                        .Where(beatmap => beatmap.KeyCount != 0)
                        .DistinctBy(beatmap => beatmap.MD5Hash)
                        .ToArray();

                    // MD5ハッシュでソート（二分探索用）
                    OnOsuDbProgressChanged(LanguageService.Instance.GetString("Loading.OsuSorting"), 95);
                    Array.Sort(beatmapArray, static (a, b) => string.CompareOrdinal(a.MD5Hash, b.MD5Hash));

                    OnOsuDbProgressChanged(LanguageService.Instance.GetString("Loading.OsuCompleted"), 100);
                    return beatmapArray;
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
        private string ReadOsuString(BinaryReader reader)
        {
            var prefix = reader.ReadByte();
            
            if (prefix == 0x00)
            {
                return string.Empty;
            }
            else if (prefix == 0x0b)
            {
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
        /// scores.dbファイルを読み込んでScoresDatabaseオブジェクトを返す
        /// </summary>
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

                    OnScoresDbProgressChanged(LanguageService.Instance.GetString("Loading.ScoresLoading"), 0);

                    var scoresDb = DatabaseDecoder.DecodeScores(filePath);
                    
                    if (scoresDb == null || scoresDb.Scores == null)
                    {
                        Console.WriteLine("scores.dbの読み込みに失敗しました");
                        return null;
                    }

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
        private void ApplyScoresToBeatmaps(OsuParsers.Database.ScoresDatabase scoresDb)
        {
            if (scoresDb == null || scoresDb.Scores == null || _beatmaps == null || _beatmaps.Length == 0)
                return;

            var totalScores = scoresDb.Scores.Count;
            var processedScores = 0;

            OnScoresDbProgressChanged(string.Format(LanguageService.Instance.GetString("Loading.ScoresApplying"), 0, totalScores), 50);

            foreach (var scoreEntry in scoresDb.Scores)
            {
                var md5Hash = scoreEntry.Item1;
                var scoreList = scoreEntry.Item2;

                if (scoreList != null && scoreList.Count > 0)
                {
                    // 二分探索で対応するBeatmapを検索
                    var index = BinarySearchByMd5(md5Hash);
                    if (index >= 0)
                    {
                        var beatmap = _beatmaps[index];
                        
                        int bestScore = scoreList.AsValueEnumerable().Max(s => s.ReplayScore);
                        double bestAccuracy = scoreList.AsValueEnumerable().Max(s => CalculateAccuracy(s));
                        int playCount = scoreList.Count;

                        _beatmaps[index] = beatmap with
                        {
                            BestScore = bestScore,
                            BestAccuracy = bestAccuracy,
                            PlayCount = playCount
                        };
                    }
                }

                processedScores++;
                
                if (processedScores % 1000 == 0 || processedScores == totalScores)
                {
                    int progress = 50 + (int)((double)processedScores / totalScores * 50);
                    OnScoresDbProgressChanged(string.Format(LanguageService.Instance.GetString("Loading.ScoresApplying"), processedScores, totalScores), progress);
                }
            }

            OnScoresDbProgressChanged(string.Format(LanguageService.Instance.GetString("Loading.ScoresCompleted"), totalScores), 100);
        }

        /// <summary>
        /// MD5ハッシュで二分探索を行い、該当するインデックスを返す
        /// </summary>
        private int BinarySearchByMd5(string md5Hash)
        {
            if (string.IsNullOrEmpty(md5Hash) || _beatmaps == null || _beatmaps.Length == 0)
                return -1;

            int left = 0;
            int right = _beatmaps.Length - 1;

            while (left <= right)
            {
                int mid = left + (right - left) / 2;
                int comparison = string.CompareOrdinal(_beatmaps[mid].MD5Hash, md5Hash);

                if (comparison == 0)
                    return mid;
                else if (comparison < 0)
                    left = mid + 1;
                else
                    right = mid - 1;
            }

            return -1;
        }

        /// <summary>
        /// スコアから精度を計算
        /// </summary>
        private double CalculateAccuracy(OsuParsers.Database.Objects.Score score)
        {
            if (score.Ruleset == Ruleset.Mania)
            {
                var totalHits = score.Count300 + score.Count100 + score.Count50 + 
                               score.CountGeki + score.CountKatu + score.CountMiss;
                
                if (totalHits == 0) return 0;

                var weightedScore = (score.Count300 + score.CountGeki) * 300.0 +
                                   score.CountKatu * 200.0 +
                                   score.Count100 * 100.0 +
                                   score.Count50 * 50.0;
                
                var maxScore = totalHits * 300.0;
                
                return maxScore > 0 ? weightedScore / maxScore * 100.0 : 0;
            }
            
            var totalHitsOther = score.Count300 + score.Count100 + score.Count50 + score.CountMiss;
            if (totalHitsOther == 0) return 0;
            
            var weightedScoreOther = score.Count300 * 300.0 + score.Count100 * 100.0 + score.Count50 * 50.0;
            var maxScoreOther = totalHitsOther * 300.0;
            
            return maxScoreOther > 0 ? weightedScoreOther / maxScoreOther * 100.0 : 0;
        }

        /// <summary>
        /// OsuParsersのDbBeatmapをBeatmapモデルに変換
        /// </summary>
        private Beatmap ConvertToBeatmap(OsuParsers.Database.Objects.DbBeatmap dbBeatmap)
        {
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
                BeatmapSetId = dbBeatmap.BeatmapSetId,
                BeatmapId = dbBeatmap.BeatmapId,
                Status = ConvertRankedStatus(dbBeatmap.RankedStatus),
                IsPlayed = dbBeatmap.IsUnplayed == false,
                LastPlayed = dbBeatmap.LastPlayed == DateTime.MinValue ? null : dbBeatmap.LastPlayed,
                LastModifiedTime = dbBeatmap.LastModifiedTime == DateTime.MinValue ? null : dbBeatmap.LastModifiedTime,
                FolderName = dbBeatmap.FolderName,
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
        private double CalculateBPM(OsuParsers.Database.Objects.DbBeatmap dbBeatmap)
        {
            try
            {
                if (dbBeatmap.TimingPoints == null || dbBeatmap.TimingPoints.Count == 0)
                {
                    return 120.0;
                }

                var firstTimingPoint = dbBeatmap.TimingPoints.AsValueEnumerable()
                    .Where(tp => tp.Inherited == false)
                    .FirstOrDefault();
                
                if (firstTimingPoint != null)
                {
                    var beatLength = firstTimingPoint.BPM;
                    if (beatLength > 0)
                    {
                        return 60000.0 / beatLength;
                    }
                    else if (beatLength < 0)
                    {
                        return 120.0;
                    }
                }

                var firstPoint = dbBeatmap.TimingPoints.AsValueEnumerable().First();
                if (firstPoint.BPM > 0)
                {
                    return 60000.0 / firstPoint.BPM;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"BPM計算エラー: {ex.Message}");
            }

            return 120.0;
        }

        /// <summary>
        /// ロングノート率を計算する
        /// </summary>
        private double CalculateLongNoteRate(OsuParsers.Database.Objects.DbBeatmap dbBeatmap)
        {
            if (dbBeatmap.Ruleset != Ruleset.Mania)
            {
                return 0.0;
            }

            try
            {
                var totalNotes = dbBeatmap.CirclesCount + dbBeatmap.SlidersCount + dbBeatmap.SpinnersCount;
                
                if (totalNotes == 0)
                {
                    return 0.0;
                }

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
        private double GetManiaStarRating(OsuParsers.Database.Objects.DbBeatmap dbBeatmap)
        {
            try
            {
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

            return 0.0;
        }

        /// <summary>
        /// RankedStatusをBeatmapStatusに変換
        /// </summary>
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
        private string GetGradeString(OsuParsers.Database.Objects.DbBeatmap dbBeatmap)
        {
            try
            {
                if (dbBeatmap.Ruleset == Ruleset.Mania)
                {
                    var grade = dbBeatmap.ManiaGrade;
                    
                    if (grade == Grade.N || grade == Grade.F)
                    {
                        return string.Empty;
                    }
                    
                    return grade switch
                    {
                        Grade.XH => "SS+",
                        Grade.X => "SS",
                        Grade.SH => "S+",
                        Grade.S => "S",
                        Grade.A => "A",
                        Grade.B => "B",
                        Grade.C => "C",
                        Grade.D => "D",
                        _ => string.Empty
                    };
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
        public async Task ReloadDatabasesAsync()
        {
            lock (_lockObject)
            {
                _osuCollections?.Clear();
                _beatmaps = Array.Empty<Beatmap>();
            }

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