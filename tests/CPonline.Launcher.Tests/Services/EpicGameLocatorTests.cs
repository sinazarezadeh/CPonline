using CPonline.Launcher.Core.Services;
using CPonline.Launcher.Tests.Fakes;
using Xunit;

namespace CPonline.Launcher.Tests.Services;

public class EpicGameLocatorTests
{
    [Fact]
    public void TryGetInstallLocation_MatchingDisplayName_ReturnsInstallLocation()
    {
        const string manifest = """
            {
              "DisplayName": "Cyberpunk 2077",
              "AppName": "Ophidian",
              "InstallLocation": "C:\\Program Files\\Epic Games\\Cyberpunk2077"
            }
            """;

        var found = EpicGameLocator.TryGetInstallLocation(manifest, out var location);

        Assert.True(found);
        Assert.Equal(@"C:\Program Files\Epic Games\Cyberpunk2077", location);
    }

    [Fact]
    public void TryGetInstallLocation_DifferentGame_ReturnsFalse()
    {
        const string manifest = """
            {
              "DisplayName": "Some Other Game",
              "InstallLocation": "C:\\Games\\Other"
            }
            """;

        var found = EpicGameLocator.TryGetInstallLocation(manifest, out var location);

        Assert.False(found);
        Assert.Null(location);
    }

    [Fact]
    public void TryGetInstallLocation_MalformedJson_ReturnsFalse()
    {
        var found = EpicGameLocator.TryGetInstallLocation("{ not json", out var location);

        Assert.False(found);
        Assert.Null(location);
    }

    [Fact]
    public void FindInstallPath_ScansAllManifestsUntilAMatchIsFound()
    {
        var fs = new FakeFileSystemProbe();
        fs.DirectoryFiles[EpicGameLocator.ManifestsDirectory] = ["other.item", "cyberpunk.item"];
        fs.FileContents["other.item"] = """{ "DisplayName": "Fortnite", "InstallLocation": "C:\\Fortnite" }""";
        fs.FileContents["cyberpunk.item"] = """{ "DisplayName": "Cyberpunk 2077", "InstallLocation": "C:\\Cyberpunk2077" }""";

        var locator = new EpicGameLocator(fs);
        var result = locator.FindInstallPath();

        Assert.Equal(@"C:\Cyberpunk2077", result);
    }

    [Fact]
    public void FindInstallPath_NoManifestsMatch_ReturnsNull()
    {
        var fs = new FakeFileSystemProbe();
        fs.DirectoryFiles[EpicGameLocator.ManifestsDirectory] = ["other.item"];
        fs.FileContents["other.item"] = """{ "DisplayName": "Fortnite", "InstallLocation": "C:\\Fortnite" }""";

        var locator = new EpicGameLocator(fs);

        Assert.Null(locator.FindInstallPath());
    }
}
