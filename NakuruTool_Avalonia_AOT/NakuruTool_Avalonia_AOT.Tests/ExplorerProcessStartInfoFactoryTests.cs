using NakuruTool_Avalonia_AOT.Features.Shared;
using Xunit;

namespace NakuruTool_Avalonia_AOT.Tests;

public class ExplorerProcessStartInfoFactoryTests
{
    [Fact]
    public void CreateOpenFolder_FileName_IsExplorerExe()
    {
        var psi = ExplorerProcessStartInfoFactory.CreateOpenFolder(@"C:\osu!\Songs\folder");

        Assert.Equal("explorer.exe", psi.FileName);
        Assert.False(psi.UseShellExecute);
    }

    [Fact]
    public void CreateOpenFolder_PassesPathAsSingleArgumentListEntry()
    {
        const string path = @"C:\osu!\Songs\1041540 CHIERU(Sakura Ayane), CHLOE(Tanezaki Atsumi), YUNI(Kohara Konomi) - Nak";

        var psi = ExplorerProcessStartInfoFactory.CreateOpenFolder(path);

        var arg = Assert.Single(psi.ArgumentList);
        Assert.Equal(path, arg);
    }

    [Theory]
    [InlineData(@"C:\osu!\Songs\1041540 CHIERU(Sakura Ayane), CHLOE(Tanezaki Atsumi), YUNI(Kohara Konomi) - Nak")]
    [InlineData(@"C:\path with spaces\foo")]
    [InlineData(@"C:\path,with,commas\bar")]
    [InlineData(@"C:\path(with)parens\baz")]
    [InlineData(@"C:\path with ""quotes""\qux")]
    public void CreateOpenFolder_DoesNotSplitOnSpecialCharacters(string path)
    {
        var psi = ExplorerProcessStartInfoFactory.CreateOpenFolder(path);

        Assert.Single(psi.ArgumentList);
        Assert.Equal(path, psi.ArgumentList[0]);
        // 文字列結合の Arguments には何も入れない (ArgumentList と併用すると例外になる)
        Assert.Equal(string.Empty, psi.Arguments);
    }
}
