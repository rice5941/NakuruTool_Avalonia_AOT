using System;
using System.IO;
using NakuruTool_Avalonia_AOT.Features.OsuDatabase;
using Xunit;

namespace NakuruTool_Avalonia_AOT.Tests;

/// <summary>
/// Songs フォルダの場所（osu!.&lt;ユーザー名&gt;.cfg の BeatmapDirectory）のテスト。
/// </summary>
public sealed class OsuSongsFolderTests : IDisposable
{
    private readonly string _root;
    private readonly string _osu;

    public OsuSongsFolderTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "NakuruTool_SongsFolder_" + Guid.NewGuid().ToString("N"));
        _osu = Path.Combine(_root, "osu!");
        Directory.CreateDirectory(_osu);
    }

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch (IOException) { }
    }

    private void WriteConfig(string fileName, string beatmapDirectory, DateTime? modified = null)
    {
        var path = Path.Combine(_osu, fileName);
        File.WriteAllText(path,
            "# osu! configuration for \"user\"\r\n" +
            "# Last updated on Sunday, November 23, 2026\r\n\r\n" +
            "BeatmapSkinSounds = 1\r\n" +
            $"BeatmapDirectory = {beatmapDirectory}\r\n" +
            "Username = someone\r\n");
        if (modified is { } time)
            File.SetLastWriteTimeUtc(path, time);
    }

    [Fact]
    public void 設定がなければosuフォルダ直下のSongs()
    {
        Assert.Equal(Path.Combine(_osu, "Songs"), OsuSongsFolder.ResolveCore(_osu, "user"));
    }

    [Fact]
    public void 別ドライブなどの絶対パスのBeatmapDirectoryを使う()
    {
        var songs = Directory.CreateDirectory(Path.Combine(_root, "other drive", "Songs")).FullName;
        WriteConfig("osu!.user.cfg", songs);

        Assert.Equal(songs, OsuSongsFolder.ResolveCore(_osu, "user"));
    }

    [Fact]
    public void 相対パスのBeatmapDirectoryはosuフォルダから()
    {
        Directory.CreateDirectory(Path.Combine(_osu, "Beatmaps"));
        WriteConfig("osu!.user.cfg", "Beatmaps");

        Assert.Equal(Path.Combine(_osu, "Beatmaps"), OsuSongsFolder.ResolveCore(_osu, "user"));
    }

    [Fact]
    public void 既定値SongsのままならosuフォルダのSongs()
    {
        Directory.CreateDirectory(Path.Combine(_osu, "Songs"));
        WriteConfig("osu!.user.cfg", "Songs");

        Assert.Equal(Path.Combine(_osu, "Songs"), OsuSongsFolder.ResolveCore(_osu, "user"));
    }

    [Fact]
    public void BeatmapDirectoryのフォルダがなければosuフォルダ直下のSongs()
    {
        WriteConfig("osu!.user.cfg", Path.Combine(_root, "missing"));

        Assert.Equal(Path.Combine(_osu, "Songs"), OsuSongsFolder.ResolveCore(_osu, "user"));
    }

    [Fact]
    public void 今のユーザーの設定を他のユーザーの設定より優先する()
    {
        var mine = Directory.CreateDirectory(Path.Combine(_root, "mine")).FullName;
        var other = Directory.CreateDirectory(Path.Combine(_root, "other")).FullName;
        WriteConfig("osu!.user.cfg", mine, DateTime.UtcNow.AddDays(-10));
        WriteConfig("osu!.someone.cfg", other, DateTime.UtcNow);

        Assert.Equal(mine, OsuSongsFolder.ResolveCore(_osu, "user"));
    }

    [Fact]
    public void ユーザー名が違う設定しかなければ新しい設定を使う()
    {
        // 別の PC から osu! フォルダを移した場合など、設定ファイルのユーザー名が今の Windows のユーザー名と違う
        var older = Directory.CreateDirectory(Path.Combine(_root, "older")).FullName;
        var newer = Directory.CreateDirectory(Path.Combine(_root, "newer")).FullName;
        WriteConfig("osu!.old-pc.cfg", older, DateTime.UtcNow.AddDays(-10));
        WriteConfig("osu!.new-pc.cfg", newer, DateTime.UtcNow);

        Assert.Equal(newer, OsuSongsFolder.ResolveCore(_osu, "user"));
    }

    [Fact]
    public void osu全体の設定ファイルは読まない()
    {
        var songs = Directory.CreateDirectory(Path.Combine(_root, "global")).FullName;
        WriteConfig("osu!.cfg", songs);

        Assert.Equal(Path.Combine(_osu, "Songs"), OsuSongsFolder.ResolveCore(_osu, "user"));
    }

    [Fact]
    public void Resolveは結果を覚えRefreshで読み直す()
    {
        var first = Directory.CreateDirectory(Path.Combine(_root, "first")).FullName;
        var second = Directory.CreateDirectory(Path.Combine(_root, "second")).FullName;
        // 今の Windows のユーザー名の設定ファイル（Resolve は Environment.UserName を使う）
        var config = $"osu!.{Environment.UserName}.cfg";
        WriteConfig(config, first);
        Assert.Equal(first, OsuSongsFolder.Resolve(_osu));

        WriteConfig(config, second);
        Assert.Equal(first, OsuSongsFolder.Resolve(_osu));
        Assert.Equal(second, OsuSongsFolder.Refresh(_osu));
        Assert.Equal(second, OsuSongsFolder.Resolve(_osu));
    }

    [Theory]
    [InlineData("BeatmapDirectory = D:\\osu songs", "D:\\osu songs")]
    [InlineData("  beatmapdirectory=E:\\Songs  ", "E:\\Songs")]
    [InlineData("BeatmapDirectory = \"F:\\Songs\"", "F:\\Songs")]
    [InlineData("BeatmapDirectory =", null)]
    [InlineData("# BeatmapDirectory = G:\\Songs", null)]
    [InlineData("BeatmapDirectoryOld = H:\\Songs", null)]
    public void BeatmapDirectoryの行を読む(string line, string? expected)
    {
        Assert.Equal(expected, OsuSongsFolder.ParseBeatmapDirectory(["Username = a", line, "Language = ja"]));
    }

    [Theory]
    [InlineData("osu!.user.cfg", true)]
    [InlineData("OSU!.User Name.CFG", true)]
    [InlineData("osu!.cfg", false)]
    [InlineData("other.cfg", false)]
    [InlineData("osu!.user.cfg.bak", false)]
    public void ユーザーの設定ファイル名か(string fileName, bool expected)
    {
        Assert.Equal(expected, OsuSongsFolder.IsUserConfigFileName(fileName));
    }
}
