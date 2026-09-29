using CPonline.Launcher.Core.Services;
using CPonline.Launcher.Tests.Fakes;
using Xunit;

namespace CPonline.Launcher.Tests.Services;

public class GameInstallLocatorTests
{
    private static string ExePath(string root) => Path.Combine(root, "bin", "x64", "Cyberpunk2077.exe");

    [Fact]
    public void Locate_PrefersFirstValidSteamCandidate_OverGogAndEpic()
    {
        var fs = new FakeFileSystemProbe();
        fs.ExistingFiles.Add(ExePath(@"D:\SteamLibrary\steamapps\common\Cyberpunk 2077"));
        fs.ExistingFiles.Add(ExePath(@"E:\GOG Games\Cyberpunk 2077"));

        var steam = new FakeSteamGameLocator { CandidateRoots = [@"D:\SteamLibrary\steamapps\common\Cyberpunk 2077"] };
        var gog = new FakeGogGameLocator { InstallPath = @"E:\GOG Games\Cyberpunk 2077" };
        var epic = new FakeEpicGameLocator();

        var locator = new GameInstallLocator(steam, gog, epic, fs);
        var result = locator.Locate();

        Assert.NotNull(result);
        Assert.Equal(GameInstallSource.Steam, result!.Source);
        Assert.Equal(@"D:\SteamLibrary\steamapps\common\Cyberpunk 2077", result.RootPath);
    }

    [Fact]
    public void Locate_SkipsInvalidSteamCandidate_FallsBackToGog()
    {
        var fs = new FakeFileSystemProbe();
        // Steam candidate reported, but the exe isn't actually there (e.g. stale metadata).
        fs.ExistingFiles.Add(ExePath(@"E:\GOG Games\Cyberpunk 2077"));

        var steam = new FakeSteamGameLocator { CandidateRoots = [@"D:\SteamLibrary\steamapps\common\Cyberpunk 2077"] };
        var gog = new FakeGogGameLocator { InstallPath = @"E:\GOG Games\Cyberpunk 2077" };
        var epic = new FakeEpicGameLocator();

        var locator = new GameInstallLocator(steam, gog, epic, fs);
        var result = locator.Locate();

        Assert.NotNull(result);
        Assert.Equal(GameInstallSource.Gog, result!.Source);
    }

    [Fact]
    public void Locate_FallsBackToEpic_WhenSteamAndGogFail()
    {
        var fs = new FakeFileSystemProbe();
        fs.ExistingFiles.Add(ExePath(@"C:\Epic Games\Cyberpunk2077"));

        var locator = new GameInstallLocator(
            new FakeSteamGameLocator(),
            new FakeGogGameLocator(),
            new FakeEpicGameLocator { InstallPath = @"C:\Epic Games\Cyberpunk2077" },
            fs);

        var result = locator.Locate();

        Assert.NotNull(result);
        Assert.Equal(GameInstallSource.Epic, result!.Source);
    }

    [Fact]
    public void Locate_ReturnsNull_WhenNothingValidFound()
    {
        var locator = new GameInstallLocator(
            new FakeSteamGameLocator(),
            new FakeGogGameLocator(),
            new FakeEpicGameLocator(),
            new FakeFileSystemProbe());

        Assert.Null(locator.Locate());
    }

    [Fact]
    public void ValidateManualPath_TrueOnlyWhenExeExists()
    {
        var fs = new FakeFileSystemProbe();
        fs.ExistingFiles.Add(ExePath(@"C:\Games\Cyberpunk 2077"));

        var locator = new GameInstallLocator(new FakeSteamGameLocator(), new FakeGogGameLocator(), new FakeEpicGameLocator(), fs);

        Assert.True(locator.ValidateManualPath(@"C:\Games\Cyberpunk 2077"));
        Assert.False(locator.ValidateManualPath(@"C:\Wrong\Path"));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void ValidateManualPath_RejectsBlankPaths(string blank)
    {
        var locator = new GameInstallLocator(new FakeSteamGameLocator(), new FakeGogGameLocator(), new FakeEpicGameLocator(), new FakeFileSystemProbe());

        Assert.False(locator.ValidateManualPath(blank));
    }
}
