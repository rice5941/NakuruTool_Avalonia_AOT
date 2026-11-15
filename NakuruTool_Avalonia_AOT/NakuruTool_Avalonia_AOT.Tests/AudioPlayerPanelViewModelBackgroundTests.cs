using System.Collections.Concurrent;
using Avalonia.Headless.XUnit;
using NakuruTool_Avalonia_AOT.Features.AudioPlayer;
using NakuruTool_Avalonia_AOT.Features.OsuDatabase;
using NakuruTool_Avalonia_AOT.Features.Settings;
using SkiaSharp;
using Xunit;

namespace NakuruTool_Avalonia_AOT.Tests;

/// <summary>
/// オーディオパネルの背景画像の非同期読み込み（縮小・最新要求のみ反映）を固定する。
/// UI スレッドへの Post はテスト内のキューに溜め、テストスレッドで実行する。
/// </summary>
public partial class AudioPlayerPanelViewModelTests
{
    [AvaloniaFact]
    public void Background_IsLoadedAsynchronously_AndDownscaled()
    {
        using var env = new BackgroundTestEnvironment();
        var beatmap = env.CreateBeatmap("set1", "a.osu", "bg.jpg", 1920, 1080);
        var vm = env.CreateViewModel();

        vm.PlayBeatmap(beatmap, 0, autoPlay: false);
        Assert.Null(vm.BackgroundImage); // UI スレッドでは読み込まない

        env.Pump(() => vm.BackgroundImage is not null);
        Assert.Equal(AudioPlayerPanelViewModel.BackgroundDecodeWidth, vm.BackgroundImage!.PixelSize.Width);
        Assert.Equal(450, vm.BackgroundImage.PixelSize.Height);

        vm.Dispose();
        Assert.Null(vm.BackgroundImage);
    }

    [AvaloniaFact]
    public void Background_RapidSwitch_AppliesOnlyLatestRequest()
    {
        using var env = new BackgroundTestEnvironment();
        var first = env.CreateBeatmap("set1", "a.osu", "a.jpg", 640, 360);
        var second = env.CreateBeatmap("set2", "b.osu", "b.png", 320, 180);
        var vm = env.CreateViewModel();

        vm.PlayBeatmap(first, 0, autoPlay: false);
        vm.PlayBeatmap(second, 1, autoPlay: false);
        env.WaitForPosts(total: 2);

        Assert.NotNull(vm.BackgroundImage);
        Assert.Equal(320, vm.BackgroundImage!.PixelSize.Width); // 2 曲目（元画像より大きくしない）
        vm.Dispose();
    }

    [AvaloniaFact]
    public void Background_ReturningToDisplayedSong_IgnoresPendingResult()
    {
        using var env = new BackgroundTestEnvironment();
        var first = env.CreateBeatmap("set1", "a.osu", "a.jpg", 640, 360);
        var second = env.CreateBeatmap("set2", "b.osu", "b.png", 320, 180);
        var vm = env.CreateViewModel();

        vm.PlayBeatmap(first, 0, autoPlay: false);
        env.Pump(() => vm.BackgroundImage is not null);
        var shown = vm.BackgroundImage;

        vm.PlayBeatmap(second, 1, autoPlay: false); // 読み込み中に
        vm.PlayBeatmap(first, 0, autoPlay: false);  // 表示中の曲へ戻る
        env.WaitForPosts(total: 2);

        Assert.Same(shown, vm.BackgroundImage);
        vm.Dispose();
    }

    [AvaloniaFact]
    public void Background_WithoutImage_ClearsPreviousImage()
    {
        using var env = new BackgroundTestEnvironment();
        var withImage = env.CreateBeatmap("set1", "a.osu", "a.jpg", 640, 360);
        var withoutImage = env.CreateBeatmap("set2", "b.osu", background: null, 0, 0);
        var vm = env.CreateViewModel();

        vm.PlayBeatmap(withImage, 0, autoPlay: false);
        env.Pump(() => vm.BackgroundImage is not null);

        vm.PlayBeatmap(withoutImage, 1, autoPlay: false);
        Assert.NotNull(vm.BackgroundImage); // 結果が届くまでは前の画像のまま（ちらつき防止）
        env.WaitForPosts(total: 2);

        Assert.Null(vm.BackgroundImage);
        vm.Dispose();
    }

    private sealed class BackgroundTestEnvironment : IDisposable
    {
        private readonly string _root = Path.Combine(Path.GetTempPath(), "NakuruPlayerBgTests_" + Guid.NewGuid().ToString("N"));
        private readonly ConcurrentQueue<Action> _uiQueue = new();
        private int _posted;

        public BackgroundTestEnvironment()
        {
            Directory.CreateDirectory(Path.Combine(_root, "Songs"));
        }

        public AudioPlayerPanelViewModel CreateViewModel()
        {
            var audioService = new StubAudioPlayerService();
            var settingsService = new StubSettingsService(new SettingsData
            {
                OsuFolderPath = _root,
                AudioVolume = 50,
            });
            var audioVm = new AudioPlayerViewModel(audioService, settingsService);
            return new AudioPlayerPanelViewModel(audioService, audioVm, settingsService)
            {
                PostToUi = action =>
                {
                    _uiQueue.Enqueue(action);
                    Interlocked.Increment(ref _posted);
                },
            };
        }

        public Beatmap CreateBeatmap(string folder, string osuFile, string? background, int width, int height)
        {
            var dir = Path.Combine(_root, "Songs", folder);
            Directory.CreateDirectory(dir);
            var events = background is null ? string.Empty : $"0,0,\"{background}\",0,0\n";
            File.WriteAllText(Path.Combine(dir, osuFile),
                $"osu file format v14\n\n[Events]\n{events}\n[TimingPoints]\n");

            if (background is not null)
            {
                using var bitmap = new SKBitmap(width, height);
                bitmap.Erase(SKColors.CornflowerBlue);
                using var image = SKImage.FromBitmap(bitmap);
                var format = background.EndsWith(".png", StringComparison.OrdinalIgnoreCase)
                    ? SKEncodedImageFormat.Png
                    : SKEncodedImageFormat.Jpeg;
                using var data = image.Encode(format, 90);
                File.WriteAllBytes(Path.Combine(dir, background), data.ToArray());
            }

            return new Beatmap
            {
                MD5Hash = folder + "/" + osuFile,
                KeyCount = 7,
                Title = "Title",
                Artist = "Artist",
                Version = "Insane",
                Creator = "Mapper",
                FolderName = folder,
                AudioFilename = "audio.mp3",
                OsuFileName = osuFile,
                Grade = string.Empty,
            };
        }

        /// <summary>UI スレッド宛ての処理を実行しながら、条件が満たされるまで待つ。</summary>
        public void Pump(Func<bool> condition, int timeoutMs = 5000)
        {
            var deadline = Environment.TickCount64 + timeoutMs;
            while (true)
            {
                while (_uiQueue.TryDequeue(out var action))
                    action();
                if (condition()) return;
                Assert.True(Environment.TickCount64 < deadline, "Timed out waiting for background image.");
                Thread.Sleep(5);
            }
        }

        /// <summary>テスト開始からのバックグラウンド読み込み結果が <paramref name="total"/> 件届き、すべて反映されるまで待つ。</summary>
        public void WaitForPosts(int total)
            => Pump(() => Volatile.Read(ref _posted) >= total && _uiQueue.IsEmpty);

        public void Dispose()
        {
            try { Directory.Delete(_root, recursive: true); } catch { }
        }
    }
}
