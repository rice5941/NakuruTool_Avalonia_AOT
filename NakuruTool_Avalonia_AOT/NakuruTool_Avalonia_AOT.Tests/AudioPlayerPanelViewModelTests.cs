using NakuruTool_Avalonia_AOT.Features.AudioPlayer;
using NakuruTool_Avalonia_AOT.Features.OsuDatabase;
using NakuruTool_Avalonia_AOT.Features.Settings;
using R3;
using System;
using System.Collections.Generic;
using System.Reflection;
using Xunit;

namespace NakuruTool_Avalonia_AOT.Tests;

public class AudioPlayerPanelViewModelTests
{
    [Fact]
    public void RefreshNavigationContextPreservingCurrent_DoesNotClearPlayedMd5Hashes()
    {
        var vm = CreateSut();

        var a = MakeBeatmap("md5-a");
        var b = MakeBeatmap("md5-b");
        var c = MakeBeatmap("md5-c");

        vm.SetNavigationContext(new[] { a, b, c }, -1);

        vm.PlayBeatmap(a, 0, autoPlay: false);
        vm.PlayBeatmap(b, 1, autoPlay: false);

        var played = GetField<HashSet<string>>(vm, "_playedMd5Hashes");
        Assert.Contains("md5-a", played);
        Assert.Contains("md5-b", played);

        vm.RefreshNavigationContextPreservingCurrent(new[] { c, b, a });

        Assert.Contains("md5-a", played);
        Assert.Contains("md5-b", played);

        vm.Dispose();
    }

    [Fact]
    public void ShuffleHistory_UsesMd5_AfterSort()
    {
        var vm = CreateSut();

        var a = MakeBeatmap("md5-a");
        var b = MakeBeatmap("md5-b");
        var c = MakeBeatmap("md5-c");

        var original = new[] { a, b, c };
        var sorted = new[] { c, b, a };

        vm.SetNavigationContext(original, 0);
        vm.PlayBeatmap(a, 0, autoPlay: false);
        vm.IsShuffleEnabled = true;

        var history = GetField<List<string>>(vm, "_shuffleHistoryMd5Hashes");
        history.Clear();
        history.Add(b.MD5Hash);
        SetField(vm, "_shuffleIndex", -1);

        var navigatedIndex = -1;
        vm.SetNavigateCallback(i => navigatedIndex = i);

        vm.RefreshNavigationContextPreservingCurrent(sorted);
        vm.NextTrackCommand.Execute(null);

        Assert.Equal(1, navigatedIndex);

        vm.Dispose();
    }

    [Fact]
    public void TogglePlayPause_WhenStopped_RegistersCurrentMd5()
    {
        var (vm, _) = CreateSutWithAudioService();

        var beatmap = MakeBeatmap("md5-current");
        vm.SetNavigationContext(new[] { beatmap }, 0);
        vm.PlayBeatmap(beatmap, 0, autoPlay: false);

        var played = GetField<HashSet<string>>(vm, "_playedMd5Hashes");
        played.Clear();

        vm.TogglePlayPauseCommand.Execute(null);

        Assert.Contains("md5-current", played);

        vm.Dispose();
    }

    [Fact]
    public void PlaybackCompleted_WithRepeatOne_RegistersCurrentMd5()
    {
        var (vm, audioService) = CreateSutWithAudioService();

        var beatmap = MakeBeatmap("md5-repeat");
        vm.SetNavigationContext(new[] { beatmap }, 0);
        vm.PlayBeatmap(beatmap, 0, autoPlay: false);
        vm.RepeatMode = RepeatMode.One;

        var played = GetField<HashSet<string>>(vm, "_playedMd5Hashes");
        played.Clear();

        audioService.CompletePlayback();

        Assert.Contains("md5-repeat", played);

        vm.Dispose();
    }

    [Fact]
    public void ShuffleRepeatAll_WhenCandidatesDepleted_DoesNotPreRegisterPreviousLapCurrent()
    {
        var vm = CreateSut();

        var a = MakeBeatmap("md5-a");
        var b = MakeBeatmap("md5-b");
        var c = MakeBeatmap("md5-c");
        var d = MakeBeatmap("md5-d");
        var e = MakeBeatmap("md5-e");
        var beatmaps = new[] { a, b, c, d, e };

        vm.SetNavigationContext(beatmaps, 4);
        vm.PlayBeatmap(e, 4, autoPlay: false);
        vm.IsShuffleEnabled = true;
        vm.RepeatMode = RepeatMode.All;

        var played = GetField<HashSet<string>>(vm, "_playedMd5Hashes");
        played.Clear();
        played.Add(a.MD5Hash);
        played.Add(b.MD5Hash);
        played.Add(c.MD5Hash);
        played.Add(d.MD5Hash);
        played.Add(e.MD5Hash);

        var history = GetField<List<string>>(vm, "_shuffleHistoryMd5Hashes");
        history.Clear();
        history.Add("md5-obsolete");
        SetField(vm, "_shuffleIndex", 0);

        var selectedIndex = -1;
        vm.SetNavigateCallback(i =>
        {
            selectedIndex = i;
            vm.PlayBeatmap(beatmaps[i], i, autoPlay: false);
        });

        vm.NextTrackCommand.Execute(null);

        Assert.NotEqual(-1, selectedIndex);
        Assert.NotEqual(4, selectedIndex);
        Assert.Single(played);
        Assert.Contains(beatmaps[selectedIndex].MD5Hash, played);
        Assert.DoesNotContain("md5-obsolete", history);
        Assert.Equal(0, GetField<int>(vm, "_shuffleIndex"));

        vm.Dispose();
    }

    [Fact]
    public void ShuffleRepeatAll_NewLap_ConsumesFiveTracksWithoutDuplicates()
    {
        var vm = CreateSut();

        var a = MakeBeatmap("md5-a");
        var b = MakeBeatmap("md5-b");
        var c = MakeBeatmap("md5-c");
        var d = MakeBeatmap("md5-d");
        var e = MakeBeatmap("md5-e");
        var beatmaps = new[] { a, b, c, d, e };

        vm.SetNavigationContext(beatmaps, 4);
        vm.PlayBeatmap(e, 4, autoPlay: false);
        vm.IsShuffleEnabled = true;
        vm.RepeatMode = RepeatMode.All;

        var played = GetField<HashSet<string>>(vm, "_playedMd5Hashes");
        played.Clear();
        foreach (var beatmap in beatmaps)
        {
            played.Add(beatmap.MD5Hash);
        }

        vm.SetNavigateCallback(i => vm.PlayBeatmap(beatmaps[i], i, autoPlay: false));

        for (var i = 0; i < beatmaps.Length; i++)
        {
            vm.NextTrackCommand.Execute(null);
        }

        Assert.Equal(beatmaps.Length, played.Count);
        foreach (var beatmap in beatmaps)
        {
            Assert.Contains(beatmap.MD5Hash, played);
        }

        vm.Dispose();
    }

    [Fact]
    public void ShuffleRepeatAll_WhenOnlyOnePlayableTrack_AllowsCurrentTrackAsFallback()
    {
        var vm = CreateSut();

        var only = MakeBeatmap("md5-only");
        var beatmaps = new[] { only };

        vm.SetNavigationContext(beatmaps, 0);
        vm.PlayBeatmap(only, 0, autoPlay: false);
        vm.IsShuffleEnabled = true;
        vm.RepeatMode = RepeatMode.All;

        var played = GetField<HashSet<string>>(vm, "_playedMd5Hashes");
        played.Clear();
        played.Add(only.MD5Hash);

        var selectedIndex = -1;
        vm.SetNavigateCallback(i =>
        {
            selectedIndex = i;
            vm.PlayBeatmap(beatmaps[i], i, autoPlay: false);
        });

        vm.NextTrackCommand.Execute(null);

        Assert.Equal(0, selectedIndex);
        Assert.Single(played);
        Assert.Contains("md5-only", played);

        vm.Dispose();
    }

    private static AudioPlayerPanelViewModel CreateSut()
    {
        return CreateSutWithAudioService().ViewModel;
    }

    private static (AudioPlayerPanelViewModel ViewModel, StubAudioPlayerService AudioService) CreateSutWithAudioService()
    {
        var audioService = new StubAudioPlayerService();
        var settings = new SettingsData
        {
            OsuFolderPath = @"C:\\osu",
            AudioVolume = 50,
            AutoPlayOnSelect = true,
            PreferUnicode = false,
        };

        var settingsService = new StubSettingsService(settings);
        var audioVm = new AudioPlayerViewModel(audioService, settingsService);
        return (new AudioPlayerPanelViewModel(audioService, audioVm, settingsService), audioService);
    }

    private static Beatmap MakeBeatmap(string md5)
    {
        return new Beatmap
        {
            MD5Hash = md5,
            KeyCount = 7,
            Status = BeatmapStatus.Ranked,
            Title = "Title",
            Artist = "Artist",
            Version = "Insane",
            Creator = "Mapper",
            FolderName = "Folder",
            AudioFilename = "audio.mp3",
            OsuFileName = "map.osu",
            Grade = string.Empty,
        };
    }

    private static T GetField<T>(object target, string name)
    {
        var field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        var value = field!.GetValue(target);
        Assert.NotNull(value);
        return (T)value!;
    }

    private static void SetField<T>(object target, string name, T value)
    {
        var field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        field!.SetValue(target, value);
    }

    private sealed class StubAudioPlayerService : IAudioPlayerService
    {
        private readonly Subject<AudioPlayerState> _stateChanged = new();
        private readonly Subject<Unit> _playbackCompleted = new();

        public Observable<AudioPlayerState> StateChanged => _stateChanged;
        public AudioPlayerState CurrentState { get; private set; } = AudioPlayerState.Stopped;
        public int Volume { get; set; }
        public Observable<Unit> PlaybackCompleted => _playbackCompleted;

        public void Play(string filePath)
        {
            CurrentState = AudioPlayerState.Playing;
            _stateChanged.OnNext(CurrentState);
        }

        public void Pause()
        {
            CurrentState = AudioPlayerState.Paused;
            _stateChanged.OnNext(CurrentState);
        }

        public void Resume()
        {
            CurrentState = AudioPlayerState.Playing;
            _stateChanged.OnNext(CurrentState);
        }

        public void Stop()
        {
            CurrentState = AudioPlayerState.Stopped;
            _stateChanged.OnNext(CurrentState);
        }

        public void TogglePlayPause()
        {
            if (CurrentState == AudioPlayerState.Playing)
            {
                Pause();
            }
            else
            {
                Resume();
            }
        }

        public double GetPosition() => 0;
        public double GetDuration() => 120;
        public void Seek(double positionSeconds) { }
        public void CompletePlayback() => _playbackCompleted.OnNext(Unit.Default);

        public void Dispose()
        {
            _stateChanged.Dispose();
            _playbackCompleted.Dispose();
        }
    }

    private sealed class StubSettingsService : ISettingsService
    {
        public StubSettingsService(SettingsData settings)
        {
            SettingsData = settings;
        }

        public ISettingsData SettingsData { get; }
        public bool SaveSettings(SettingsData settings) => true;
        public bool CheckSettingsPath() => true;
        public string GetSettingsPath() => "settings.json";
        public void Dispose() { }
    }
}
